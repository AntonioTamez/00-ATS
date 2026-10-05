"""Finding model + deterministic dedupe/ordering."""
from .util import SEV_RANK, norm_ws, sha1_short

CONF = {"high": 0.95, "medium": 0.75, "low": 0.5}
PRIORITY = {"rule": 0, "analyzer": 0, "linter": 1, "ai": 2}


def make(rule_id, origin, severity, category, confidence, file, line, title, message,
         evidence="", end_line=None, tool=None):
    """origin: rule | analyzer | linter | ai"""
    return {
        "id": "", "rule_id": rule_id, "origin": origin, "tool": tool or origin,
        "severity": severity, "category": category,
        "confidence": CONF.get(confidence, confidence) if isinstance(confidence, str) else confidence,
        "file": file, "line": line, "end_line": end_line or line,
        "title": title, "message": message, "evidence": (evidence or "").strip()[:300],
    }


def sort_key(f):
    return (SEV_RANK[f["severity"]], f["file"] or "", f["line"] or 0, f["rule_id"], f["evidence"])


def dedupe(findings):
    """Exact duplicates collapse; a lower-priority finding is dropped when a higher-priority
    one of the same category sits within 2 lines in the same file. Returns (kept, merged_count)."""
    ordered = sorted(findings, key=lambda f: (PRIORITY[f["origin"]], -f["confidence"]) + sort_key(f))
    kept, merged = [], 0
    for f in ordered:
        dup = False
        for k in kept:
            if k["file"] != f["file"]:
                continue
            if k["rule_id"] == f["rule_id"] and k["line"] == f["line"]:
                dup = True
            elif (PRIORITY[k["origin"]] < PRIORITY[f["origin"]] and f["file"] and f["line"]
                  and k["line"] and k["category"] == f["category"]
                  and abs(k["line"] - f["line"]) <= 2):
                dup = True
            if dup:
                break
        if dup:
            merged += 1
        else:
            kept.append(f)
    kept.sort(key=sort_key)
    return kept, merged


def assign_ids(findings):
    seen = {}
    for f in findings:
        base = sha1_short("%s|%s|%s" % (f["rule_id"], f["file"], norm_ws(f["evidence"])), 8)
        n = seen.get(base, 0)
        seen[base] = n + 1
        f["id"] = "F-%s%s" % (base, "" if n == 0 else "-%d" % (n + 1))
    return findings
