"""Rule packs: JSON files under rules/ (+ <repo>/.code-review/rules + config.rule_dirs).

Pack file: {"pack": "name", "ai_hints": "...", "rules": [rule, ...]}
Rule (kind=line):  id, severity, category, confidence, stacks, pattern, [flags],
                   [exclude_pattern], [paths], [exclude_paths], title{es,en}, message{es,en},
                   examples{match[], nomatch[]}
Rule (kind=path):  same but `path_patterns` / `exclude_path_patterns` instead of pattern.
Every rule must carry examples; `validate` runs them, so rules are unit-tested data.
"""
import glob
import os
import re

from .config import SKILL_DIR
from .diffparse import added_lines, new_side_lines
from .findings import make
from .util import SEVERITIES, any_glob, glob_match, read_json, tr

CATEGORIES = {"security", "correctness", "reliability", "maintainability", "performance",
              "testing", "hygiene", "infra"}
ID_RE = re.compile(r"^[A-Z][A-Z0-9]{1,4}-[A-Z]{2,6}-\d{3}$")
SUPPRESS = re.compile(r"code-review\s*:\s*(?:ignore|disable)\s*[:=]?\s*([A-Za-z0-9_,\s*-]+)", re.I)


def _flags(rule):
    return re.I if "i" in rule.get("flags", "") else 0


def validate_rule(r):
    errs = []
    rid = r.get("id", "?")
    if not ID_RE.match(str(r.get("id", ""))):
        errs.append("%s: id must look like ABC-SEC-001" % rid)
    if r.get("severity") not in SEVERITIES:
        errs.append("%s: bad severity" % rid)
    if r.get("category") not in CATEGORIES:
        errs.append("%s: bad category" % rid)
    if r.get("confidence") not in ("high", "medium", "low"):
        errs.append("%s: confidence must be high|medium|low" % rid)
    if not isinstance(r.get("stacks"), list) or not r.get("stacks"):
        errs.append("%s: stacks must be a non-empty list" % rid)
    for k in ("title", "message"):
        v = r.get(k)
        if not isinstance(v, dict) or not v.get("es") or not v.get("en"):
            errs.append("%s: %s needs es+en" % (rid, k))
    kind = r.get("kind", "line")
    if kind == "line":
        try:
            re.compile(r.get("pattern", ""), _flags(r))
            if r.get("exclude_pattern"):
                re.compile(r["exclude_pattern"], _flags(r))
        except re.error as e:
            errs.append("%s: invalid regex: %s" % (rid, e))
        if not r.get("pattern"):
            errs.append("%s: missing pattern" % rid)
        ex = r.get("examples") or {}
        if not ex.get("match") or not ex.get("nomatch"):
            errs.append("%s: examples.match and examples.nomatch are required" % rid)
        elif not errs:
            c = compile_rule(r)
            for s in ex["match"]:
                if not line_hits(c, s):
                    errs.append("%s: example should match but does not: %r" % (rid, s))
            for s in ex["nomatch"]:
                if line_hits(c, s):
                    errs.append("%s: example should NOT match but does: %r" % (rid, s))
    elif kind == "path":
        if not r.get("path_patterns"):
            errs.append("%s: missing path_patterns" % rid)
        ex = r.get("examples") or {}
        if not ex.get("match") or not ex.get("nomatch"):
            errs.append("%s: examples.match and examples.nomatch are required" % rid)
        else:
            for s in ex["match"]:
                if not path_hits(r, s):
                    errs.append("%s: path example should match: %r" % (rid, s))
            for s in ex["nomatch"]:
                if path_hits(r, s):
                    errs.append("%s: path example should NOT match: %r" % (rid, s))
    else:
        errs.append("%s: unknown kind %r" % (rid, kind))
    return errs


def compile_rule(r):
    c = dict(r)
    c["_re"] = re.compile(r["pattern"], _flags(r)) if r.get("pattern") else None
    c["_ex"] = re.compile(r["exclude_pattern"], _flags(r)) if r.get("exclude_pattern") else None
    return c


def line_hits(c, text):
    if not c["_re"] or not c["_re"].search(text):
        return False
    return not (c["_ex"] and c["_ex"].search(text))


def path_hits(r, path):
    return any_glob(path, r.get("path_patterns")) and not any_glob(
        path, r.get("exclude_path_patterns"))


def pack_files(repo_root, cfg):
    dirs = [os.path.join(SKILL_DIR, "rules")]
    if repo_root:
        dirs.append(os.path.join(repo_root, ".code-review", "rules"))
    for d in cfg.get("rule_dirs", []):
        dirs.append(d if os.path.isabs(d) or not repo_root else os.path.join(repo_root, d))
    out = []
    for d in dirs:
        out += sorted(glob.glob(os.path.join(d, "*.json")))
    return out


def load_packs(repo_root, cfg):
    """Returns (rules[compiled], hints{stack: text}, errors[]). Deterministic order."""
    rules, hints, errors, seen = [], {}, [], {}
    for p in pack_files(repo_root, cfg):
        try:
            data = read_json(p)
        except ValueError as e:
            errors.append("%s: invalid JSON: %s" % (p, e))
            continue
        pack = data.get("pack", os.path.basename(p))
        if data.get("ai_hints"):
            hints[pack] = data["ai_hints"]
        for r in data.get("rules", []):
            errs = validate_rule(r)
            if errs:
                errors += ["%s: %s" % (os.path.basename(p), e) for e in errs]
                continue
            if r["id"] in seen:
                errors.append("%s: duplicate rule id %s (also in %s)" % (
                    os.path.basename(p), r["id"], seen[r["id"]]))
                continue
            seen[r["id"]] = os.path.basename(p)
            c = compile_rule(r)
            c["_pack"] = pack
            rules.append(c)
    rules.sort(key=lambda r: r["id"])
    return rules, hints, errors


def _applies(rule, f):
    if "*" not in rule["stacks"] and not set(rule["stacks"]) & set(f["stacks"]):
        return False
    if rule.get("paths") and not any_glob(f["path"], rule["paths"]):
        return False
    if rule.get("exclude_paths") and any_glob(f["path"], rule["exclude_paths"]):
        return False
    return True


def _suppressed(rule_id, text, prev_text):
    for t in (text, prev_text):
        if not t:
            continue
        m = SUPPRESS.search(t)
        if m:
            ids = {x.upper() for x in re.split(r"[,\s]+", m.group(1)) if x}
            if rule_id.upper() in ids or "ALL" in ids or "*" in ids:
                return True
    return False


def apply_rules(files, rules, cfg, lang):
    """-> (findings, suppressed[{rule_id,file,line}], truncated[{rule_id,file,count}])"""
    out, supp, trunc = [], [], []
    cap = int(cfg.get("max_findings_per_rule_per_file", 10))
    for f in files:
        if not f["reviewable"] or f["binary"]:
            continue
        side = new_side_lines(f)
        added = added_lines(f)
        for r in rules:
            if not _applies(r, f):
                continue
            sev = cfg.get("severity_overrides", {}).get(r["id"], r["severity"])
            title, msg = tr(r["title"], lang), tr(r["message"], lang)
            if r.get("kind", "line") == "path":
                if f["status"] != "deleted" and path_hits(r, f["path"]):
                    out.append(make(r["id"], "rule", sev, r["category"], r["confidence"],
                                    f["path"], None, title, msg, f["path"]))
                continue
            n = 0
            for no, text in added:
                if not line_hits(r, text):
                    continue
                if _suppressed(r["id"], text, side.get(no - 1)):
                    supp.append({"rule_id": r["id"], "file": f["path"], "line": no})
                    continue
                n += 1
                if n > cap:
                    continue
                out.append(make(r["id"], "rule", sev, r["category"], r["confidence"],
                                f["path"], no, title, msg, text))
            if n > cap:
                trunc.append({"rule_id": r["id"], "file": f["path"], "count": n - cap})
    return out, supp, trunc
