"""CLI. stdout is always ONE JSON envelope; human logs go to stderr.

Exit codes: 0 done | 10 awaiting_agents | 20 needs_input | 3 error
            with --exit-code, `done` maps the verdict: 0 pass/warnings, 1 changes requested, 2 blocked
"""
import argparse
import json
import os
import sys

from . import ENGINE_VERSION, gate, orchestrator, publish as publish_mod
from .config import load_config
from .errors import NeedsInput, ReviewError
from .rules import load_packs
from .sources import repo_root
from .util import SEVERITIES

EXIT_AWAITING, EXIT_NEEDS_INPUT, EXIT_ERROR = 10, 20, 3


def build_parser():
    p = argparse.ArgumentParser(prog="review.py", description="Deterministic code review engine")
    p.add_argument("--version", action="version", version=ENGINE_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="start a review")
    g = r.add_argument_group("source (pick one; none = infer from local state)")
    g.add_argument("--pr", help="PR number, GitHub PR URL or Azure DevOps PR URL")
    g.add_argument("--repo", help="owner/repo when --pr is a bare number")
    g.add_argument("--commit", help="commit SHA/ref (diff against its first parent)")
    g.add_argument("--range", help="A..B (tips) or A...B (merge-base)")
    g.add_argument("--base", help="base ref: reviews <head> against merge-base(base, head)")
    g.add_argument("--head", help="head ref for --base (default HEAD)")
    g.add_argument("--staged", action="store_true", help="index only")
    g.add_argument("--working", action="store_true", help="all uncommitted changes + untracked")
    g.add_argument("--pending", action="store_true", help="unpushed commits + working tree")
    g.add_argument("--patch", help="path to a .diff/.patch file")
    g.add_argument("--stdin", action="store_true", help="read a unified diff from stdin")
    o = r.add_argument_group("options")
    o.add_argument("--repo-dir", help="repository to review (default: cwd)")
    o.add_argument("--lang", choices=["es", "en"], help="report language (default from config: es)")
    o.add_argument("--no-ai", action="store_true", help="deterministic checks only")
    o.add_argument("--no-linters", action="store_true", help="do not run external linters")
    o.add_argument("--only", help="comma-separated AI lenses (e.g. security,correctness)")
    o.add_argument("--scope", help="AI scope: all | risk:N | path:glob[,glob]")
    o.add_argument("--accept-generic", action="store_true", help="proceed with generic rules only")
    o.add_argument("--out-dir", help="reviews root (default <repo>/.code-review/reviews)")
    o.add_argument("--config", help="extra config JSON merged over defaults and repo config")
    o.add_argument("--exit-code", action="store_true", help="exit 1/2 on CHANGES_REQUESTED/BLOCKED")

    s = sub.add_parser("resume", help="continue after AI agents wrote their results")
    s.add_argument("--run", required=True, help="review folder")
    s.add_argument("--exit-code", action="store_true")

    u = sub.add_parser("publish", help="post findings as a GitHub PR review (dry-run unless --yes)")
    u.add_argument("--run", required=True)
    u.add_argument("--min-severity", default="medium", choices=SEVERITIES)
    u.add_argument("--yes", action="store_true", help="actually post (ask the user first!)")
    u.add_argument("--force", action="store_true", help="post again if already published")

    ru = sub.add_parser("rules", help="list active rules")
    ru.add_argument("--stack")
    ru.add_argument("--repo-dir")
    sub.add_parser("validate", help="self-test rules, schemas, agents and config")
    return p


def _out(obj):
    sys.stdout.write(json.dumps(obj, indent=2, ensure_ascii=False) + "\n")


def _done_exit(env, want_code):
    return env["exit_code"] if want_code else 0


def _cmd_rules(args):
    root = repo_root(args.repo_dir or os.getcwd())
    cfg = load_config(root)
    rules, _h, errs = load_packs(root, cfg)
    rows = [{"id": r["id"], "severity": r["severity"], "category": r["category"],
             "stacks": r["stacks"], "pack": r["_pack"], "title": r["title"]["en"]}
            for r in rules if not args.stack or args.stack in r["stacks"] or "*" in r["stacks"]]
    _out({"status": "ok", "count": len(rows), "rules": rows, "errors": errs})
    return 0


def _cmd_validate():
    from . import prompts
    from .config import SKILL_DIR
    problems = []
    cfg = load_config(None)
    rules, _h, errs = load_packs(None, cfg)
    problems += errs
    lenses = prompts.available_lenses()
    for l in cfg["ai"]["lenses"]:
        if l not in lenses:
            problems.append("config lens without agents/%s.md" % l)
    if not os.path.isfile(os.path.join(SKILL_DIR, "agents", "_contract.md")):
        problems.append("agents/_contract.md missing")
    try:
        prompts.schema()
    except (OSError, ValueError) as e:
        problems.append("schemas/ai-response.schema.json: %s" % e)
    _out({"status": "ok" if not problems else "invalid", "rules": len(rules), "lenses": lenses,
          "problems": problems})
    return 0 if not problems else 1


def main(argv=None):
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, OSError):
            pass
    args = build_parser().parse_args(argv)
    try:
        if args.cmd == "run":
            env = orchestrator.start(args)
        elif args.cmd == "resume":
            env = orchestrator.finalize(args.run)
        elif args.cmd == "publish":
            env = publish_mod.publish(args.run, args.min_severity, args.yes, args.force)
            _out(env)
            return 0
        elif args.cmd == "rules":
            return _cmd_rules(args)
        else:
            return _cmd_validate()
    except NeedsInput as e:
        _out({"status": "needs_input", "reason": e.reason, "questions": e.questions})
        return EXIT_NEEDS_INPUT
    except ReviewError as e:
        _out({"status": "error", "code": e.code, "message": e.message, "hint": e.hint})
        return EXIT_ERROR
    _out(env)
    if env["status"] == "awaiting_agents":
        return EXIT_AWAITING
    return _done_exit(env, getattr(args, "exit_code", False) or env.get("exit_code_enabled"))
