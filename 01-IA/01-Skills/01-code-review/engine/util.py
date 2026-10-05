import hashlib
import json
import os
import re
import subprocess
from functools import lru_cache

SEVERITIES = ["blocker", "high", "medium", "low", "info"]
SEV_RANK = {s: i for i, s in enumerate(SEVERITIES)}


def run(cmd, cwd=None, input_bytes=None, timeout=300):
    """Run a command. Returns (rc, stdout:str, stderr:str). Never raises on rc != 0."""
    env = dict(os.environ)
    env["GIT_TERMINAL_PROMPT"] = "0"
    env["GH_PROMPT_DISABLED"] = "1"
    try:
        p = subprocess.run(cmd, cwd=cwd, input=input_bytes, capture_output=True,
                           timeout=timeout, env=env)
    except FileNotFoundError:
        return 127, "", "command not found: %s" % cmd[0]
    except subprocess.TimeoutExpired:
        return 124, "", "timeout after %ss" % timeout
    return (p.returncode, p.stdout.decode("utf-8", "replace"),
            p.stderr.decode("utf-8", "replace"))


def sha256_text(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sha1_short(text, n=10):
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:n]


def canonical_json(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def write_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(obj, f, indent=2, sort_keys=True, ensure_ascii=False)
        f.write("\n")


def read_json(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)


def write_text(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def read_text(path):
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        return f.read()


@lru_cache(maxsize=1024)
def glob_regex(pattern):
    """Glob -> regex. `**/` = any dirs, `*` = within a segment. Case-insensitive.
    A pattern without '/' matches the basename at any depth."""
    pat = pattern.replace("\\", "/")
    if "/" not in pat:
        pat = "**/" + pat
    out, i = [], 0
    while i < len(pat):
        if pat.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif pat.startswith("**", i):
            out.append(".*")
            i += 2
        elif pat[i] == "*":
            out.append("[^/]*")
            i += 1
        elif pat[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(pat[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$", re.I)


def glob_match(path, pattern):
    return bool(glob_regex(pattern).match(path.replace("\\", "/")))


def any_glob(path, patterns):
    return any(glob_match(path, p) for p in patterns or [])


def deep_merge(base, over):
    out = dict(base)
    for k, v in over.items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k] = deep_merge(out[k], v)
        else:
            out[k] = v
    return out


def tr(value, lang):
    """Pick a translation from {'es':..,'en':..}; falls back to en, then any."""
    if isinstance(value, dict):
        return value.get(lang) or value.get("en") or next(iter(value.values()), "")
    return value or ""


def norm_ws(s):
    return re.sub(r"\s+", " ", s or "").strip()
