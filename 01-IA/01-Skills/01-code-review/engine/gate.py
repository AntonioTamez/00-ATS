"""Verdict from severity counts and configurable thresholds. Pure and deterministic."""
from .util import SEVERITIES

ORDER = ["blocked_if", "changes_requested_if", "warnings_if"]
VERDICTS = {"blocked_if": "BLOCKED", "changes_requested_if": "CHANGES_REQUESTED",
            "warnings_if": "PASS_WITH_WARNINGS"}
EXIT_CODES = {"PASS": 0, "PASS_WITH_WARNINGS": 0, "CHANGES_REQUESTED": 1, "BLOCKED": 2}


def count(findings):
    c = {s: 0 for s in SEVERITIES}
    for f in findings:
        c[f["severity"]] += 1
    return c


def decide(counts, gate):
    """-> (verdict, reason). Rules are checked from most to least severe; first hit wins."""
    for key in ORDER:
        for sev in SEVERITIES:
            n = gate.get(key, {}).get(sev)
            if n and counts.get(sev, 0) >= n:
                return VERDICTS[key], "%s >= %d (found %d)" % (sev, n, counts[sev])
    return "PASS", "no thresholds reached"
