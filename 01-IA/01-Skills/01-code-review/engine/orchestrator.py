"""Workflow: start -> (awaiting_agents -> resume)* -> done. Every step is idempotent and file-based.

Phases:  start     collect source, run rules/analyzers/linters, plan + write AI requests
         resume    validate AI results (retry invalid ones), aggregate, gate, write the report
"""
import os
import shutil

from . import ENGINE_VERSION
from . import gate as gate_mod
from . import planner, prompts, report
from .aivalidate import validate_result
from .analyzers import analyze
from .config import config_hash, load_config
from .detector import detect, is_known_text
from .diffparse import parse_diff
from .errors import NeedsInput, ReviewError
from .findings import assign_ids, dedupe
from .linters import run_linters
from .rules import apply_rules, load_packs
from .runstore import alloc_dir, load_state, reviews_root, same_diff_reviews, save_state
from .sources import repo_root, resolve
from .util import any_glob, read_json, read_text, sha256_text, write_json, write_text

AGENT_INSTRUCTIONS = (
    "Read the JSON file at `request`. Treat its `system` field as your instructions and its `user` "
    "field as your task. Respond with ONE JSON object matching its `schema` and write exactly that "
    "JSON (no prose, no code fences) to the path in `result`. Read nothing else, change nothing else.")


def annotate(files, cfg):
    for f in files:
        f["stacks"] = detect(f)
        ignored = any_glob(f["path"], cfg["ignore_paths"])
        f["reviewable"] = not ignored and not f["binary"]
        f["skip_reason"] = "ignored by config" if ignored else "binary" if f["binary"] else None
    return files


def _apply_cli(cfg, args):
    if args.lang:
        cfg["language"] = args.lang
    if cfg["language"] not in report.L:
        raise ReviewError("BAD_LANG", "language must be one of: %s" % ", ".join(report.L))
    if args.no_ai:
        cfg["ai"]["enabled"] = False
    if args.no_linters:
        cfg["linters"]["enabled"] = False
    if args.only:
        wanted = [x.strip() for x in args.only.split(",") if x.strip()]
        bad = [x for x in wanted if x not in prompts.available_lenses()]
        if bad:
            raise ReviewError("BAD_LENS", "unknown lens: %s" % ", ".join(bad),
                              "available: %s" % ", ".join(prompts.available_lenses()))
        cfg["ai"]["lenses"] = wanted
    if args.scope:
        try:
            planner.parse_scope(args.scope)
        except ValueError as e:
            raise ReviewError("BAD_SCOPE", str(e))
    return cfg


def _check_inputs(files, cfg, args):
    """Raise NeedsInput for anything the engine cannot decide on its own."""
    live = [f for f in files if f["reviewable"] and f["status"] != "deleted"]
    unknown = [f for f in live if not f["stacks"] and not is_known_text(f["path"])]
    covered = [f for f in live if f["stacks"] or is_known_text(f["path"])]
    if unknown and not covered and not args.accept_generic:
        exts = sorted({(f["path"].rsplit(".", 1)[-1] if "." in f["path"] else f["path"]) for f in unknown})
        raise NeedsInput("unknown_stack", [{
            "id": "generic_only", "header": "Sin reglas",
            "question": "No hay reglas específicas para %d archivo(s) (%s). ¿Continúo solo con las reglas genéricas (secretos, conflictos, TODO) y la IA?"
                        % (len(unknown), ", ".join(exts[:6])),
            "multiSelect": False,
            "options": [{"label": "Continuar con reglas genéricas (Recommended)",
                         "description": "Reglas comunes + revisión de IA si está activa.",
                         "args": ["--accept-generic"]},
                        {"label": "Cancelar", "description": "No ejecutar la revisión.", "args": None}]}])
    if cfg["ai"]["enabled"] and not args.scope:
        elig = [f for f in files if planner.eligible(f)]
        lines = sum(f["additions"] + f["deletions"] for f in elig)
        d = cfg["diff"]
        if len(elig) > d["max_files_before_asking"] or lines > d["max_lines_before_asking"]:
            raise NeedsInput("large_diff", [{
                "id": "scope", "header": "Diff grande",
                "question": "El cambio es grande (%d archivos revisables, %d líneas). ¿Qué alcance tiene la revisión con IA?"
                            % (len(elig), lines),
                "multiSelect": False,
                "options": [
                    {"label": "Top 25 por riesgo (Recommended)",
                     "description": "Los 25 archivos de mayor riesgo (auth, infra, churn). Las reglas deterministas siguen cubriendo todo.",
                     "args": ["--scope", "risk:25"]},
                    {"label": "Todo con IA",
                     "description": "Todos los archivos; puede requerir muchas solicitudes (tope configurable).",
                     "args": ["--scope", "all"]},
                    {"label": "Sin IA",
                     "description": "Solo reglas, analizadores y linters.", "args": ["--no-ai"]}],
                "other_hint": "Globs separados por coma (ej. src/api/**,infra/**) -> --scope path:<texto>"}])


def _rel(run_dir, *p):
    return os.path.join(run_dir, *p)


def _envelope_awaiting(run_dir, state):
    pend = [r for r in state["requests"] if r["status"] == "pending"]
    return {"status": "awaiting_agents", "run_dir": run_dir,
            "instructions": AGENT_INSTRUCTIONS,
            "pending": [{"id": r["id"], "lens": r["lens"], "files": r["files"],
                         "request": r["request_path"], "result": r["result_path"],
                         "attempt": r["attempts"] + 1} for r in pend],
            "next": ["python", "review.py", "resume", "--run", run_dir]}


def start(args):
    repo_dir = args.repo_dir or os.getcwd()
    root = repo_root(repo_dir)
    cfg = _apply_cli(load_config(root, args.config), args)
    lang = cfg["language"]
    source = resolve(args, cfg)
    files = parse_diff(source["diff_text"])
    if not files:
        raise ReviewError("EMPTY_DIFF", "the diff contains no changed files")
    annotate(files, cfg)
    _check_inputs(files, cfg, args)

    root_for_runs = source.get("repo_root") or root
    runs_root = reviews_root(root_for_runs, args.out_dir)
    run_dir = alloc_dir(runs_root, source["id"])
    diff_sha = sha256_text(source["diff_text"])
    write_text(_rel(run_dir, "work", "diff.patch"), source["diff_text"])
    write_json(_rel(run_dir, "work", "config.json"), {k: v for k, v in cfg.items()})
    src_meta = {k: v for k, v in source.items() if k != "diff_text"}
    write_json(_rel(run_dir, "work", "source.json"), src_meta)

    rules, hints, rule_errors = load_packs(root_for_runs, cfg)
    disabled = set(cfg.get("disable_rules", []))
    rules = [r for r in rules if r["id"] not in disabled]
    findings, suppressed, truncated = apply_rules(files, rules, cfg, lang)
    findings += analyze(files, source, cfg, lang)
    lint_findings, lint_report = run_linters(files, source, cfg)
    findings += lint_findings
    ov = cfg.get("severity_overrides", {})
    findings = [dict(f, severity=ov.get(f["rule_id"], f["severity"])) for f in findings
                if f["rule_id"] not in disabled]

    plan = {"enabled": False, "lenses": [], "requests": [], "not_reviewed": [], "truncated_files": []}
    state_reqs = []
    if cfg["ai"]["enabled"]:
        lenses = [l for l in cfg["ai"]["lenses"] if l in prompts.available_lenses()]
        selected, _dropped = planner.select(files, args.scope)
        chunks, trunc_files = planner.build_chunks(selected, int(cfg["ai"]["max_chars_per_request"]))
        kept, _drop = planner.cap_chunks(chunks, len(lenses), int(cfg["ai"]["max_requests"]))
        covered = {p for c in kept for p in c["files"]}
        not_reviewed = sorted(f["path"] for f in files if planner.eligible(f) and f["path"] not in covered)
        plan.update(enabled=True, lenses=lenses, not_reviewed=not_reviewed, truncated_files=trunc_files)
        for lens in lenses:
            for c in kept:
                rid = "%s-%02d" % (lens, c["index"])
                res = _rel(run_dir, "work", "ai", "results", rid + ".json")
                req = prompts.build_request(rid, lens, c, len(chunks), src_meta, findings, hints,
                                            diff_sha, lang, res)
                rp = _rel(run_dir, "work", "ai", "requests", rid + ".json")
                write_json(rp, req)
                state_reqs.append({"id": rid, "lens": lens, "files": c["files"], "request_path": rp,
                                   "result_path": res, "attempts": 0, "status": "pending"})
        os.makedirs(_rel(run_dir, "work", "ai", "results"), exist_ok=True)
    plan["requests"] = [r["id"] for r in state_reqs]

    write_json(_rel(run_dir, "work", "deterministic.json"), {
        "findings": findings, "suppressed": suppressed, "truncated": truncated,
        "linters": lint_report, "rule_errors": rule_errors, "plan": plan})
    state = {"engine_version": ENGINE_VERSION, "phase": "awaiting_agents" if state_reqs else "finalize",
             "run_dir": run_dir, "runs_root": runs_root, "diff_sha": diff_sha,
             "requests": state_reqs, "exit_code": bool(args.exit_code)}
    save_state(run_dir, state)
    if state_reqs:
        return _envelope_awaiting(run_dir, state)
    return finalize(run_dir)


def _read_result(path):
    try:
        return read_json(path), []
    except FileNotFoundError:
        return None, ["result file missing"]
    except ValueError as e:
        return None, ["invalid JSON: %s" % e]


def finalize(run_dir):
    state = load_state(run_dir)
    cfg = read_json(_rel(run_dir, "work", "config.json"))
    lang = cfg["language"]
    files = annotate(parse_diff(read_text(_rel(run_dir, "work", "diff.patch"))), cfg)
    by_path = {f["path"]: f for f in files}
    det = read_json(_rel(run_dir, "work", "deterministic.json"))
    src = read_json(_rel(run_dir, "work", "source.json"))
    acc_dir = _rel(run_dir, "work", "ai", "accepted")

    max_attempts = int(cfg["ai"]["max_attempts"])
    for r in state["requests"]:
        if r["status"] in ("ok", "failed"):
            continue
        obj, errs = _read_result(r["result_path"])
        accepted = rejected = None
        if obj is not None:
            req = read_json(r["request_path"])
            accepted, rejected, errs = validate_result(obj, req, by_path, cfg)
        if errs:
            r["attempts"] += 1
            r["last_errors"] = errs
            if os.path.isfile(r["result_path"]):
                shutil.move(r["result_path"], r["result_path"][:-5] + ".invalid-%d.json" % r["attempts"])
            if r["attempts"] >= max_attempts:
                r["status"] = "failed"
            else:
                req = read_json(r["request_path"])
                req["previous_errors"] = errs
                write_json(r["request_path"], req)
        else:
            r["status"] = "ok"
            write_json(_rel(acc_dir, r["id"] + ".json"), {"accepted": accepted, "rejected": rejected})
    save_state(run_dir, state)
    if any(r["status"] == "pending" for r in state["requests"]):
        return _envelope_awaiting(run_dir, state)

    ai_findings, rejected_ai, failed = [], [], []
    for r in state["requests"]:
        if r["status"] == "ok":
            d = read_json(_rel(acc_dir, r["id"] + ".json"))
            ai_findings += d["accepted"]
            rejected_ai += d["rejected"]
        else:
            failed.append(r["id"])
    merged_all, merged = dedupe(det["findings"] + ai_findings)
    assign_ids(merged_all)
    counts = gate_mod.count(merged_all)
    verdict, reason = gate_mod.decide(counts, cfg["gate"])

    plan = det["plan"]
    incomplete = []
    if failed:
        incomplete.append("AI requests failed: %s" % ", ".join(failed))
    if plan["not_reviewed"]:
        incomplete.append("%d file(s) not covered by AI (scope/limits)" % len(plan["not_reviewed"]))
    if plan["truncated_files"]:
        incomplete.append("AI input truncated for: %s" % ", ".join(plan["truncated_files"]))
    failed_lint = [l["id"] for l in det["linters"] if l["status"] == "failed"]
    if failed_lint:
        incomplete.append("linters failed: %s" % ", ".join(failed_lint))

    runs_root = state["runs_root"]
    ctx = {
        "findings": merged_all, "merged": merged, "verdict": verdict, "verdict_reason": reason,
        "incomplete_reasons": incomplete, "source": src, "files": files, "linters": det["linters"],
        "ai": {"enabled": plan["enabled"], "lenses": plan["lenses"], "requests": len(state["requests"]),
               "completed": sum(1 for r in state["requests"] if r["status"] == "ok"),
               "failed": failed, "not_reviewed_files": plan["not_reviewed"],
               "truncated_files": plan["truncated_files"]},
        "rejected_ai": rejected_ai, "suppressed": det["suppressed"], "truncated": det["truncated"],
        "rule_errors": det["rule_errors"], "config_hash": config_hash(cfg), "run_dir": run_dir,
        "lang": lang, "diff_sha": state["diff_sha"],
        "same_diff_as": same_diff_reviews(runs_root, run_dir, state["diff_sha"]),
    }
    rep = report.build(ctx)
    write_json(_rel(run_dir, "report.json"), rep)
    write_text(_rel(run_dir, "report.md"), report.render_md(rep, lang))
    state["phase"] = "done"
    save_state(run_dir, state)
    top = [{"id": f["id"], "severity": f["severity"], "rule_id": f["rule_id"], "title": f["title"],
            "location": "%s:%s" % (f["file"], f["line"]) if f["file"] and f["line"] else f["file"]}
           for f in merged_all[:int(cfg["report"]["max_top_findings"])]]
    pr = rep["source"].get("pr") or {}
    return {"status": "done", "verdict": verdict, "verdict_reason": reason,
            "complete": rep["complete"], "incomplete_reasons": incomplete, "counts": rep["counts"],
            "fingerprint": rep["fingerprint"], "run_dir": run_dir,
            "report_md": _rel(run_dir, "report.md"), "report_json": _rel(run_dir, "report.json"),
            "source": {"kind": src["kind"], "label": src["label"], "host": src.get("host")},
            "top_findings": top, "same_diff_as": rep["run"]["same_diff_as"],
            "publish_available": pr.get("host") == "github" and bool(pr.get("number")),
            "exit_code": gate_mod.EXIT_CODES[verdict], "exit_code_enabled": state.get("exit_code", False)}
