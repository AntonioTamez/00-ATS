"""Hallucination guard: an AI finding is accepted only if it can be verified against the diff."""
from . import schema as jschema
from .diffparse import added_line_numbers, new_side_lines
from .findings import make
from .prompts import schema as ai_schema
from .util import SEV_RANK, norm_ws

MAX_SPAN = 30


def _cap(sev, maxsev):
    return maxsev if SEV_RANK[sev] < SEV_RANK[maxsev] else sev


def validate_result(obj, req, files_by_path, cfg):
    """-> (accepted findings[], rejected[{reason,file,line,title}], schema_errors[])"""
    errs = jschema.validate(obj, ai_schema())
    if errs:
        return [], [], errs
    if obj["lens"] != req["lens"]:
        return [], [], ["$.lens: expected '%s', got '%s'" % (req["lens"], obj["lens"])]
    ai = cfg["ai"]
    accepted, rejected = [], []
    for it in obj["findings"]:
        def rej(reason, it=it):
            rejected.append({"request": req["id"], "reason": reason, "file": it["file"],
                             "line": it["line"], "title": it["title"]})
        f = files_by_path.get(it["file"])
        if f is None:
            rej("file_not_in_diff")
            continue
        if it["file"] not in req["files"]:
            rej("file_not_in_request")
            continue
        end = it.get("end_line") or it["line"]
        if end < it["line"] or end - it["line"] > MAX_SPAN:
            rej("bad_line_span")
            continue
        valid = new_side_lines(f).keys() if ai.get("allow_context_lines") else added_line_numbers(f)
        if it["line"] not in valid:
            rej("line_not_changed")
            continue
        side = new_side_lines(f)
        window = norm_ws(" ".join(side.get(n, "") for n in range(it["line"], end + 1)))
        if norm_ws(it["evidence"]) not in window:
            rej("evidence_not_found")
            continue
        if it["confidence"] < ai["min_confidence"]:
            rej("low_confidence")
            continue
        msg = it["explanation"].strip()
        if it.get("suggestion"):
            msg += "\n\n" + it["suggestion"].strip()
        accepted.append(make("AI-%s" % req["lens"].upper(), "ai",
                             _cap(it["severity"], ai.get("max_severity", "high")),
                             it["category"], round(float(it["confidence"]), 2), it["file"],
                             it["line"], it["title"].strip(), msg, it["evidence"], end_line=end,
                             tool="ai:%s" % req["lens"]))
    accepted.sort(key=lambda x: (SEV_RANK[x["severity"]], -x["confidence"], x["file"], x["line"]))
    return accepted[:int(ai["max_findings_per_request"])], rejected, []
