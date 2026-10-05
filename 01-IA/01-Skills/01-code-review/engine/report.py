"""Report assembly: report.json (machine) + report.md (human, es/en)."""
import datetime

from . import ENGINE_VERSION
from .gate import count
from .util import SEVERITIES, canonical_json, sha256_text

L = {
    "es": {
        "title": "Revisión de código", "verdict": "Veredicto", "reason": "motivo",
        "incomplete": "REVISIÓN INCOMPLETA", "sev": "Severidad", "n": "Cant.",
        "summary": "Resumen del cambio", "source": "Fuente", "range": "Rango", "files": "Archivos",
        "stacks": "Tecnologías", "diff": "Hash del diff", "fp": "Huella (fingerprint)",
        "cfg": "Hash de configuración", "findings": "Hallazgos", "none": "Sin hallazgos.",
        "origin": "Origen", "conf": "confianza", "linters": "Linters", "ai": "Revisión con IA",
        "skipped": "Archivos omitidos", "suppressed": "Hallazgos suprimidos (code-review: ignore)",
        "prior": "Revisiones previas del mismo diff", "status": "Estado",
        "lenses": "Lentes", "requests": "Solicitudes", "rejected": "Hallazgos de IA descartados por no verificables",
        "notrev": "Archivos sin revisión de IA", "trunc": "Hallazgos truncados por límite por regla/archivo",
        "ruleerr": "Problemas en packs de reglas", "reasons": "Motivos", "ran": "ejecutado",
        "skip": "omitido", "failed": "falló", "no_ai": "IA desactivada", "notes": "Notas",
        "gen": "Generado", "next": "Cómo reproducir",
    },
    "en": {
        "title": "Code review", "verdict": "Verdict", "reason": "reason",
        "incomplete": "INCOMPLETE REVIEW", "sev": "Severity", "n": "Count",
        "summary": "Change summary", "source": "Source", "range": "Range", "files": "Files",
        "stacks": "Technologies", "diff": "Diff hash", "fp": "Fingerprint",
        "cfg": "Config hash", "findings": "Findings", "none": "No findings.",
        "origin": "Origin", "conf": "confidence", "linters": "Linters", "ai": "AI review",
        "skipped": "Skipped files", "suppressed": "Suppressed findings (code-review: ignore)",
        "prior": "Previous reviews of the same diff", "status": "Status",
        "lenses": "Lenses", "requests": "Requests", "rejected": "AI findings discarded as unverifiable",
        "notrev": "Files without AI review", "trunc": "Findings truncated by per-rule/file cap",
        "ruleerr": "Rule pack problems", "reasons": "Reasons", "ran": "ran",
        "skip": "skipped", "failed": "failed", "no_ai": "AI disabled", "notes": "Notes",
        "gen": "Generated", "next": "How to reproduce",
    },
}


def fingerprint(verdict, findings):
    core = [(f["id"], f["rule_id"], f["severity"], f["file"], f["line"]) for f in findings]
    return sha256_text(canonical_json({"verdict": verdict, "findings": core}))[:16]


def build(ctx):
    """ctx: dict assembled by the orchestrator. Returns the report dict."""
    findings = ctx["findings"]
    counts = count(findings)
    src = ctx["source"]
    files = ctx["files"]
    stacks = {}
    for f in files:
        for s in f["stacks"]:
            stacks[s] = stacks.get(s, 0) + 1
    rep = {
        "schema_version": "1.0", "engine_version": ENGINE_VERSION,
        "verdict": ctx["verdict"], "verdict_reason": ctx["verdict_reason"],
        "complete": not ctx["incomplete_reasons"], "incomplete_reasons": ctx["incomplete_reasons"],
        "fingerprint": fingerprint(ctx["verdict"], findings),
        "counts": dict(counts, total=len(findings), suppressed=len(ctx["suppressed"]),
                       merged=ctx["merged"]),
        "source": {k: src.get(k) for k in ("kind", "id", "label", "base", "head", "base_sha",
                                           "head_sha", "host", "notes")},
        "stacks": dict(sorted(stacks.items())),
        "files": [{"path": f["path"], "status": f["status"], "additions": f["additions"],
                   "deletions": f["deletions"], "stacks": f["stacks"],
                   "skipped": f.get("skip_reason")} for f in sorted(files, key=lambda x: x["path"])],
        "linters": ctx["linters"], "ai": ctx["ai"], "findings": findings,
        "rejected_ai": ctx["rejected_ai"], "suppressed": ctx["suppressed"],
        "truncated": ctx["truncated"], "rule_errors": ctx["rule_errors"],
        "config_hash": ctx["config_hash"],
        "run": {"dir": ctx["run_dir"], "generated_at": datetime.datetime.now().astimezone().isoformat(timespec="seconds"),
                "lang": ctx["lang"], "same_diff_as": ctx["same_diff_as"]},
    }
    rep["source"]["diff_sha256"] = ctx["diff_sha"]
    rep["source"]["files_changed"] = len(files)
    rep["source"]["additions"] = sum(f["additions"] for f in files)
    rep["source"]["deletions"] = sum(f["deletions"] for f in files)
    pr = src.get("pr")
    if pr:
        rep["source"]["pr"] = {k: pr.get(k) for k in ("host", "owner", "repo", "number", "url", "title",
                                                      "author", "base", "head", "head_sha", "draft")}
    return rep


def _fence(s):
    return "```\n%s\n```" % s.replace("```", "'''")


def render_md(rep, lang):
    t = L.get(lang, L["en"])
    out = []
    a = out.append
    src = rep["source"]
    a("# %s - %s" % (t["title"], src["label"]))
    a("")
    a("**%s: `%s`** (%s: %s)" % (t["verdict"], rep["verdict"], t["reason"], rep["verdict_reason"]))
    if not rep["complete"]:
        a("")
        a("> **%s** - %s" % (t["incomplete"], "; ".join(rep["incomplete_reasons"])))
    a("")
    a("| %s | %s |" % (t["sev"], t["n"]))
    a("|---|---:|")
    for s in SEVERITIES:
        a("| %s | %d |" % (s, rep["counts"][s]))
    a("")
    a("## %s" % t["summary"])
    a("")
    a("- %s: %s (`%s`)" % (t["source"], src["label"], src["kind"]))
    if src.get("base") and src.get("head"):
        a("- %s: `%s` -> `%s`" % (t["range"], src.get("base"), src.get("head")))
    elif src.get("head"):
        a("- %s: `%s`" % (t["range"], src.get("head")))
    a("- %s: %d (+%d / -%d)" % (t["files"], src["files_changed"], src["additions"], src["deletions"]))
    a("- %s: %s" % (t["stacks"], ", ".join("%s (%d)" % kv for kv in rep["stacks"].items()) or "-"))
    a("- %s: `%s`" % (t["diff"], src["diff_sha256"][:16]))
    a("- %s: `%s`" % (t["fp"], rep["fingerprint"]))
    a("- %s: `%s`" % (t["cfg"], rep["config_hash"]))
    for n in src.get("notes") or []:
        a("- %s: %s" % (t["notes"], n))
    a("")
    a("## %s" % t["findings"])
    a("")
    if not rep["findings"]:
        a(t["none"])
    for sev in SEVERITIES:
        group = [f for f in rep["findings"] if f["severity"] == sev]
        if not group:
            continue
        a("### %s (%d)" % (sev.upper(), len(group)))
        a("")
        for f in group:
            loc = "%s:%s" % (f["file"], f["line"]) if f["file"] and f["line"] else (f["file"] or "-")
            a("**%s** `%s` - %s" % (f["id"], f["rule_id"], f["title"]))
            a("")
            a("`%s` - %s: %s - %s: %.2f" % (loc, t["origin"], f["tool"], t["conf"], f["confidence"]))
            a("")
            a(f["message"])
            if f["evidence"]:
                a("")
                a(_fence(f["evidence"]))
            a("")
    if rep["linters"]:
        a("## %s" % t["linters"])
        a("")
        for l in rep["linters"]:
            st = t.get(l["status"] if l["status"] != "skipped" else "skip", l["status"])
            extra = " - %s" % l["reason"] if l["reason"] else ""
            a("- `%s` %s: %s (%d)%s" % (l["id"], l["version"], st, l["count"], extra))
        a("")
    ai = rep["ai"]
    a("## %s" % t["ai"])
    a("")
    if not ai["enabled"]:
        a("- %s" % t["no_ai"])
    else:
        a("- %s: %s" % (t["lenses"], ", ".join(ai["lenses"]) or "-"))
        a("- %s: %d / %d" % (t["requests"], ai["completed"], ai["requests"]))
        if ai["failed"]:
            a("- %s: %s" % (t["failed"], ", ".join(ai["failed"])))
        if ai["not_reviewed_files"]:
            a("- %s: %s" % (t["notrev"], ", ".join(ai["not_reviewed_files"])))
    a("")
    if rep["rejected_ai"]:
        a("<details><summary>%s (%d)</summary>" % (t["rejected"], len(rep["rejected_ai"])))
        a("")
        for r in rep["rejected_ai"]:
            a("- `%s:%s` %s - %s (%s)" % (r["file"], r["line"], r["title"], r["reason"], r["request"]))
        a("")
        a("</details>")
        a("")
    skipped = [f for f in rep["files"] if f["skipped"]]
    if skipped:
        a("## %s" % t["skipped"])
        a("")
        for f in skipped:
            a("- `%s` - %s" % (f["path"], f["skipped"]))
        a("")
    if rep["suppressed"]:
        a("## %s" % t["suppressed"])
        a("")
        for s in rep["suppressed"]:
            a("- `%s` %s:%s" % (s["rule_id"], s["file"], s["line"]))
        a("")
    if rep["truncated"]:
        a("## %s" % t["trunc"])
        a("")
        for s in rep["truncated"]:
            a("- `%s` %s: +%d" % (s["rule_id"], s["file"], s["count"]))
        a("")
    if rep["rule_errors"]:
        a("## %s" % t["ruleerr"])
        a("")
        for e in rep["rule_errors"]:
            a("- %s" % e)
        a("")
    if rep["run"]["same_diff_as"]:
        a("## %s" % t["prior"])
        a("")
        for p in rep["run"]["same_diff_as"]:
            a("- `%s` - %s (`%s`)" % (p["name"], p["verdict"], p["fingerprint"]))
        a("")
    a("---")
    a("%s: %s - engine %s" % (t["gen"], rep["run"]["generated_at"], rep["engine_version"]))
    return "\n".join(out) + "\n"
