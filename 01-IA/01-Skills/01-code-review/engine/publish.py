"""Publish findings as ONE GitHub review with inline comments. Dry-run unless confirmed."""
import json
import os

from .errors import ReviewError
from .util import SEV_RANK, read_json, run, write_json

MAX_COMMENTS = 50


def build_payload(rep, min_severity):
    pr = rep["source"].get("pr") or {}
    if pr.get("host") != "github" or not pr.get("number") or not pr.get("owner"):
        raise ReviewError("NOT_GITHUB_PR", "publishing is only supported for GitHub PR reviews",
                          "Azure DevOps / local reviews cannot be posted by this skill.")
    limit = SEV_RANK[min_severity]
    eligible = [f for f in rep["findings"] if SEV_RANK[f["severity"]] <= limit]
    inline = [f for f in eligible if f["file"] and f["line"]][:MAX_COMMENTS]
    general = [f for f in eligible if not (f["file"] and f["line"])]
    comments = []
    for f in inline:
        body = "**[%s] %s** (`%s`)\n\n%s" % (f["severity"].upper(), f["title"], f["rule_id"], f["message"])
        comments.append({"path": f["file"], "line": f["line"], "side": "RIGHT", "body": body})
    c = rep["counts"]
    body = ["**Automated code review - %s**" % rep["verdict"],
            "", "blocker: %d, high: %d, medium: %d, low: %d" % (c["blocker"], c["high"], c["medium"], c["low"])]
    if not rep["complete"]:
        body += ["", "_Incomplete review: %s_" % "; ".join(rep["incomplete_reasons"])]
    if general:
        body += [""] + ["- **[%s] %s** (`%s`): %s" % (f["severity"].upper(), f["title"], f["rule_id"],
                                                    f["message"]) for f in general]
    omitted = len([f for f in eligible if f["file"] and f["line"]]) - len(inline)
    if omitted > 0:
        body += ["", "_%d more inline finding(s) omitted (limit %d); see the local report._" % (omitted, MAX_COMMENTS)]
    body += ["", "_fingerprint `%s`_" % rep["fingerprint"]]
    return {"commit_id": pr.get("head_sha"), "body": "\n".join(body), "event": "COMMENT",
            "comments": comments}, pr


def publish(run_dir, min_severity, confirmed, force=False):
    rp = os.path.join(run_dir, "report.json")
    if not os.path.isfile(rp):
        raise ReviewError("NO_REPORT", "report.json not found in %s" % run_dir)
    rep = read_json(rp)
    payload, pr = build_payload(rep, min_severity)
    ppath = os.path.join(run_dir, "publish-payload.json")
    write_json(ppath, payload)
    summary = {"repo": "%s/%s" % (pr["owner"], pr["repo"]), "pr": pr["number"],
               "inline_comments": len(payload["comments"]), "min_severity": min_severity,
               "payload": ppath}
    if not confirmed:
        return dict(summary, status="dry_run")
    marker = os.path.join(run_dir, "published.json")
    if os.path.isfile(marker) and not force:
        raise ReviewError("ALREADY_PUBLISHED", "this review was already published",
                          "Pass --force to post again.")
    rc, out, err = run(["gh", "api", "--method", "POST",
                        "repos/%s/%s/pulls/%s/reviews" % (pr["owner"], pr["repo"], pr["number"]),
                        "--input", "-"], input_bytes=json.dumps(payload).encode("utf-8"))
    if rc != 0:
        raise ReviewError("GH_PUBLISH_FAILED", "gh api failed: %s" % (err.strip() or out.strip())[:400],
                          "Lines must belong to the PR diff; check `gh auth status` and permissions.")
    try:
        url = json.loads(out).get("html_url")
    except ValueError:
        url = None
    write_json(marker, {"url": url, "min_severity": min_severity})
    return dict(summary, status="published", url=url)
