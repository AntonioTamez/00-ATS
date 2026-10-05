"""Optional external linters. Run only when installed AND the reviewed code is what is on disk.
Their output is filtered to lines added by the change, so they never flag legacy code."""
import json
import os
import shutil

from .diffparse import added_line_numbers, new_side_lines
from .findings import make
from .util import run


# --------------------------------------------------------------- parsers (pure)
def parse_ruff(out):
    return [{"file": d["filename"], "line": d["location"]["row"], "code": d["code"],
             "message": d["message"], "native": d["code"]} for d in json.loads(out or "[]")]


def parse_eslint(out):
    res = []
    for d in json.loads(out or "[]"):
        for m in d.get("messages", []):
            res.append({"file": d["filePath"], "line": m.get("line") or 1,
                        "code": m.get("ruleId") or "parse-error", "message": m["message"],
                        "native": m.get("severity", 1)})
    return res


def parse_shellcheck(out):
    d = json.loads(out or "{}")
    return [{"file": c["file"], "line": c["line"], "code": "SC%s" % c["code"],
             "message": c["message"], "native": c["level"]} for c in d.get("comments", [])]


def parse_hadolint(out):
    return [{"file": d["file"], "line": d["line"], "code": d["code"], "message": d["message"],
             "native": d["level"]} for d in json.loads(out or "[]")]


def parse_tflint(out):
    d = json.loads(out or "{}")
    return [{"file": i["range"]["filename"], "line": i["range"]["start"]["line"],
             "code": i["rule"]["name"], "message": i["message"], "native": i["rule"]["severity"]}
            for i in d.get("issues", [])]


# ------------------------------------------------------- severity / category
def sev_ruff(code, _n):
    if code.startswith("S"):
        return "medium"
    if code.startswith(("E9", "F63", "F7", "F82")):
        return "high"
    return "medium" if code.startswith("F") else "low"


def sev_eslint(_c, n):
    return "medium" if n == 2 else "low"


def sev_levels(_c, n):
    return {"error": "high", "warning": "medium"}.get(str(n).lower(), "low")


def cat_ruff(code):
    return "security" if code.startswith("S") else "correctness" if code.startswith("F") else "hygiene"


def cat_generic(_code):
    return "maintainability"


def _is_dockerfile(f):
    b = f["path"].rsplit("/", 1)[-1].lower()
    return b.startswith("dockerfile") or b.endswith(".dockerfile")


LINTERS = [
    {"id": "ruff", "bin": "ruff", "select": lambda f: "python" in f["stacks"],
     "cmd": lambda b, files: [b, "check", "--output-format", "json", "--no-cache", "--exit-zero"] + files,
     "parse": parse_ruff, "sev": sev_ruff, "cat": cat_ruff, "version": ["--version"]},
    {"id": "eslint", "bin": "eslint", "local": "node_modules/.bin/eslint",
     "select": lambda f: "typescript" in f["stacks"] and f["path"].rsplit(".", 1)[-1] in
     ("js", "jsx", "ts", "tsx", "mjs", "cjs"),
     "cmd": lambda b, files: [b, "-f", "json", "--no-error-on-unmatched-pattern"] + files,
     "parse": parse_eslint, "sev": sev_eslint, "cat": cat_generic, "version": ["--version"]},
    {"id": "shellcheck", "bin": "shellcheck", "select": lambda f: "shell" in f["stacks"],
     "cmd": lambda b, files: [b, "-f", "json1"] + files,
     "parse": parse_shellcheck, "sev": sev_levels, "cat": cat_generic, "version": ["--version"]},
    {"id": "hadolint", "bin": "hadolint", "select": _is_dockerfile,
     "cmd": lambda b, files: [b, "-f", "json"] + files,
     "parse": parse_hadolint, "sev": sev_levels, "cat": cat_generic, "version": ["--version"]},
    {"id": "tflint", "bin": "tflint", "select": lambda f: "terraform" in f["stacks"],
     "cmd": lambda b, files: [b, "--recursive", "--format", "json"],
     "parse": parse_tflint, "sev": sev_levels, "cat": cat_generic, "version": ["--version"]},
]


def _find_bin(spec, root):
    if spec.get("local") and root:
        for suffix in ("", ".cmd"):
            p = os.path.join(root, spec["local"] + suffix)
            if os.path.isfile(p):
                return p
    return shutil.which(spec["bin"])


def _rel(root, p):
    p = p.replace("\\", "/")
    if root and os.path.isabs(p):
        try:
            return os.path.relpath(p, root).replace("\\", "/")
        except ValueError:
            return p
    return p


def run_linters(files, source, cfg):
    """-> (findings, report[{id, status, reason, version, count}])"""
    lc = cfg["linters"]
    report, out = [], []
    root = source.get("repo_root")
    for spec in LINTERS:
        sel = [f for f in files if f["reviewable"] and not f["binary"] and f["status"] != "deleted"
               and spec["select"](f)]
        if not sel:
            continue
        entry = {"id": spec["id"], "status": "skipped", "reason": "", "version": "", "count": 0}
        report.append(entry)
        if not lc.get("enabled", True):
            entry["reason"] = "disabled by config"
            continue
        if not source.get("linters_ok"):
            entry["reason"] = "reviewed code is not what is checked out (head differs or tree is dirty)"
            continue
        binp = _find_bin(spec, root)
        if not binp:
            entry["reason"] = "not installed"
            continue
        on_disk = [f for f in sel if os.path.isfile(os.path.join(root, f["path"]))]
        if not on_disk:
            entry["reason"] = "files not found on disk"
            continue
        rc, so, se = run(spec["cmd"](binp, [f["path"] for f in on_disk]), cwd=root,
                         timeout=int(lc.get("timeout_seconds", 120)))
        vrc, vso, _ = run([binp] + spec["version"], cwd=root, timeout=20)
        entry["version"] = (vso.strip().splitlines() or [""])[0][:60] if vrc == 0 else ""
        try:
            raw = spec["parse"](so)
        except (ValueError, KeyError, TypeError) as e:
            entry["status"] = "failed"
            entry["reason"] = "unparseable output (rc=%s): %s" % (rc, (se.strip() or str(e))[:160])
            continue
        entry["status"] = "ran"
        by_path = {f["path"]: f for f in on_disk}
        for r in sorted(raw, key=lambda r: (r["file"], r["line"], str(r["code"]))):
            rel = _rel(root, r["file"])
            f = by_path.get(rel)
            if not f or r["line"] not in added_line_numbers(f):
                continue
            ev = new_side_lines(f).get(r["line"], "")
            code = str(r["code"])
            out.append(make("LINT-%s-%s" % (spec["id"].upper(), code), "linter",
                            spec["sev"](code, r["native"]), spec["cat"](code), 0.9, rel, r["line"],
                            "%s: %s" % (spec["id"], code), r["message"], ev, tool=spec["id"]))
            entry["count"] += 1
    return out, report
