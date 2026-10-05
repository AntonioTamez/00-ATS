"""Deterministic AI work planning: eligibility, scope selection, risk ranking, chunking."""
import re

from .detector import is_test_path
from .diffparse import render_hunk
from .util import any_glob

ELIGIBLE_EXT = (".json", ".xml", ".toml", ".config", ".html")
RISK_RE = re.compile(r"auth|login|passw|crypt|token|secret|secur|permission|payment|billing|"
                     r"migration|dockerfile|workflow|pipeline|deploy|\.tf$|iam|rbac", re.I)
INFRA = {"terraform", "docker", "kubernetes", "github-actions", "sql"}


def eligible(f):
    return bool(f["reviewable"] and not f["binary"] and f["status"] != "deleted" and f["hunks"]
                and (f["stacks"] or f["path"].lower().endswith(ELIGIBLE_EXT)))


def risk(f):
    s = min(f["additions"] + f["deletions"], 400)
    if RISK_RE.search(f["path"]):
        s += 200
    s += 100 if INFRA & set(f["stacks"]) else 50 if f["stacks"] else 0
    if is_test_path(f["path"]):
        s -= 100
    return s


def parse_scope(scope):
    """'all' | 'risk:N' | 'path:glob[,glob]' -> (kind, value)"""
    if not scope or scope == "all":
        return "all", None
    if scope.startswith("risk:") and scope[5:].isdigit() and int(scope[5:]) > 0:
        return "risk", int(scope[5:])
    if scope.startswith("path:") and scope[5:].strip():
        return "path", [g.strip() for g in scope[5:].split(",") if g.strip()]
    raise ValueError("--scope must be all | risk:N | path:glob[,glob]")


def select(files, scope):
    """-> (selected sorted by path, dropped sorted by path)"""
    kind, val = parse_scope(scope)
    cand = sorted((f for f in files if eligible(f)), key=lambda f: f["path"])
    if kind == "all":
        return cand, []
    if kind == "risk":
        ranked = sorted(cand, key=lambda f: (-risk(f), f["path"]))
        keep = {f["path"] for f in ranked[:val]}
    else:
        keep = {f["path"] for f in cand if any_glob(f["path"], val)}
    return ([f for f in cand if f["path"] in keep], [f for f in cand if f["path"] not in keep])


def _header(f, part=None):
    h = "=== FILE: %s [%s] +%d -%d stacks=%s" % (f["path"], f["status"], f["additions"],
                                               f["deletions"], ",".join(f["stacks"]) or "-")
    return h + (" part %s" % part if part else "") + " ==="


def _parts(f, budget):
    """Split one file into render parts that each fit `budget` chars (hunk-aligned)."""
    parts, cur, size, truncated = [], [], 0, False
    for h in f["hunks"]:
        t = render_hunk(h)
        if len(t) > budget:
            t = t[:budget] + "\n... [hunk truncated by engine]"
            truncated = True
        if cur and size + len(t) > budget:
            parts.append("\n".join(cur))
            cur, size = [], 0
        cur.append(t)
        size += len(t) + 1
    if cur:
        parts.append("\n".join(cur))
    return parts, truncated


def build_chunks(files, max_chars):
    """-> (chunks[{index, text, files, risk}], truncated_paths[])"""
    pieces, truncated = [], []
    for f in files:
        parts, tr_ = _parts(f, max_chars - 400)
        if tr_:
            truncated.append(f["path"])
        for i, body in enumerate(parts, 1):
            label = "%d/%d" % (i, len(parts)) if len(parts) > 1 else None
            pieces.append((f, _header(f, label) + "\n" + body))
    chunks, cur, size = [], [], 0
    for f, text in pieces:
        if cur and size + len(text) > max_chars:
            chunks.append(cur)
            cur, size = [], 0
        cur.append((f, text))
        size += len(text) + 2
    if cur:
        chunks.append(cur)
    out = []
    for i, c in enumerate(chunks, 1):
        paths = sorted({f["path"] for f, _t in c})
        out.append({"index": i, "text": "\n\n".join(t for _f, t in c), "files": paths,
                    "risk": max(risk(f) for f, _t in c)})
    return out, truncated


def cap_chunks(chunks, n_lenses, max_requests):
    """Keep the highest-risk chunks so lenses*chunks <= max_requests. -> (kept, dropped)"""
    limit = max(1, max_requests // max(1, n_lenses))
    if len(chunks) <= limit:
        return chunks, []
    ranked = sorted(chunks, key=lambda c: (-c["risk"], c["index"]))
    keep = {c["index"] for c in ranked[:limit]}
    return ([c for c in chunks if c["index"] in keep], [c for c in chunks if c["index"] not in keep])
