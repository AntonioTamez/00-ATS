"""Resolve the review input (GitHub/Azure PR, commit, range, local diff, patch) to a Source.

Source dict: kind, id, label, diff_text, base, head, base_sha, head_sha, repo_root,
             host, pr, linters_ok, notes, auto_selected
Anything the engine cannot decide deterministically raises NeedsInput.
"""
import os
import re
import shutil
import sys

from .errors import NeedsInput, ReviewError
from .util import run

GIT_BASE = ["git", "-c", "core.quotepath=false", "-c", "color.ui=false",
            "-c", "core.autocrlf=false"]
EMPTY_TREE = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"
MAX_UNTRACKED_BYTES = 1_000_000


def git(repo, *args, timeout=300):
    return run(GIT_BASE + ["-C", repo] + list(args), timeout=timeout)


def _diff_cmd(ctx):
    return ["diff", "--no-color", "--no-ext-diff", "--no-textconv", "-M",
            "--src-prefix=a/", "--dst-prefix=b/", "--unified=%d" % ctx]


def repo_root(start):
    rc, out, _ = git(start or ".", "rev-parse", "--show-toplevel")
    return out.strip() if rc == 0 and out.strip() else None


def _rev(repo, ref):
    rc, out, _ = git(repo, "rev-parse", "--verify", "-q", ref + "^{commit}")
    return out.strip() if rc == 0 and out.strip() else None


def _need_rev(repo, ref):
    sha = _rev(repo, ref)
    if not sha:
        raise ReviewError("BAD_REF", "git reference not found: %s" % ref,
                          "Check the name, or `git fetch` first.")
    return sha


def current_branch(repo):
    rc, out, _ = git(repo, "rev-parse", "--abbrev-ref", "HEAD")
    b = out.strip() if rc == 0 else "HEAD"
    return b if b != "HEAD" else "detached"


def default_base(repo):
    """First existing ref among upstream / origin default / main / master / develop."""
    for ref in ("@{upstream}", "origin/HEAD", "origin/main", "origin/master",
                "origin/develop", "main", "master", "develop"):
        sha = _rev(repo, ref)
        if sha:
            return ref, sha
    return None, None


def _merge_base(repo, a, b):
    rc, out, _ = git(repo, "merge-base", a, b)
    return out.strip() if rc == 0 and out.strip() else None


def _git_diff(repo, ctx, *rest):
    rc, out, err = git(repo, *(_diff_cmd(ctx) + list(rest)))
    if rc != 0:
        raise ReviewError("GIT_DIFF_FAILED", "git diff failed: %s" % err.strip()[:300])
    return out


def _synthetic_added(relpath, data):
    """Unified diff for an untracked file (no subprocess, fully deterministic)."""
    head = "diff --git a/%s b/%s\nnew file mode 100644\n" % (relpath, relpath)
    if b"\0" in data[:8000]:
        return head + "Binary files /dev/null and b/%s differ\n" % relpath
    text = data.decode("utf-8", "replace")
    if text == "":
        return head
    ends_nl = text.endswith("\n")
    lines = text.replace("\r\n", "\n").split("\n")
    if ends_nl:
        lines.pop()
    body = "".join("+%s\n" % l for l in lines)
    tail = "" if ends_nl else "\\ No newline at end of file\n"
    return head + "--- /dev/null\n+++ b/%s\n@@ -0,0 +1,%d @@\n%s%s" % (
        relpath, len(lines), body, tail)


def _untracked(repo, notes):
    rc, out, _ = git(repo, "ls-files", "--others", "--exclude-standard", "-z")
    parts = []
    for rel in sorted(p for p in out.split("\0") if p):
        full = os.path.join(repo, rel)
        try:
            if os.path.getsize(full) > MAX_UNTRACKED_BYTES:
                notes.append("untracked file skipped (>1MB): %s" % rel)
                continue
            with open(full, "rb") as fh:
                parts.append(_synthetic_added(rel.replace("\\", "/"), fh.read()))
        except OSError:
            notes.append("untracked file unreadable: %s" % rel)
    return "".join(parts)


def _clean_tree(repo):
    rc, out, _ = git(repo, "status", "--porcelain")
    return rc == 0 and out.strip() == ""


def _linters_ok(repo, head_sha):
    return bool(head_sha) and head_sha == _rev(repo, "HEAD") and _clean_tree(repo)


def _safe(s, n=40):
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s).strip("_")[:n] or "x"


def _base_source(kind, sid, label, diff, root, **kw):
    s = {"kind": kind, "id": sid, "label": label, "diff_text": diff, "base": None,
         "head": None, "base_sha": None, "head_sha": None, "repo_root": root,
         "host": "local", "pr": None, "linters_ok": False, "notes": [], "auto_selected": False}
    s.update(kw)
    return s


# ---------------------------------------------------------------- local modes
def _staged(repo, ctx):
    head = _rev(repo, "HEAD")
    diff = _git_diff(repo, ctx, "--cached")
    return _base_source("staged", "staged-" + _safe(current_branch(repo)),
                        "staged changes", diff, repo, head=current_branch(repo),
                        head_sha=head, linters_ok=True)


def _working(repo, ctx):
    if not _rev(repo, "HEAD"):
        raise ReviewError("NO_COMMITS", "repository has no commits yet")
    notes = []
    diff = _git_diff(repo, ctx, "HEAD") + _untracked(repo, notes)
    return _base_source("working", "working-" + _safe(current_branch(repo)),
                        "uncommitted changes (staged + unstaged + untracked)", diff, repo,
                        head=current_branch(repo), head_sha=_rev(repo, "HEAD"),
                        linters_ok=True, notes=notes)


def _pending(repo, ctx):
    ref, _sha = default_base(repo)
    if not ref:
        raise ReviewError("NO_BASE", "cannot determine a base branch for --pending",
                          "Use --base <ref> explicitly.")
    mb = _merge_base(repo, ref, "HEAD")
    if not mb:
        raise ReviewError("NO_MERGE_BASE", "no merge-base between %s and HEAD" % ref)
    notes = []
    diff = _git_diff(repo, ctx, mb) + _untracked(repo, notes)
    return _base_source("pending", "pending-" + _safe(current_branch(repo)),
                        "everything not on %s yet (commits + working tree)" % ref, diff, repo,
                        base=ref, head=current_branch(repo), base_sha=mb,
                        head_sha=_rev(repo, "HEAD"), linters_ok=True, notes=notes)


def _branch(repo, ctx, base, head):
    base_sha = _need_rev(repo, base)
    head_ref = head or "HEAD"
    head_sha = _need_rev(repo, head_ref)
    mb = _merge_base(repo, base_sha, head_sha)
    if not mb:
        raise ReviewError("NO_MERGE_BASE", "no merge-base between %s and %s" % (base, head_ref))
    diff = _git_diff(repo, ctx, mb, head_sha)
    name = head if head else current_branch(repo)
    return _base_source("branch", "branch-" + _safe(name),
                        "%s vs %s (merge-base)" % (head_ref, base), diff, repo,
                        base=base, head=head_ref, base_sha=mb, head_sha=head_sha,
                        linters_ok=_linters_ok(repo, head_sha))


def _commit(repo, ctx, ref):
    sha = _need_rev(repo, ref)
    parent = _rev(repo, sha + "^1") or EMPTY_TREE
    diff = _git_diff(repo, ctx, parent, sha)
    rc, out, _ = git(repo, "log", "-1", "--format=%an%x00%s%x00%b", sha)
    author, subject, body = (out.split("\0", 2) + ["", "", ""])[:3] if rc == 0 else ("", "", "")
    return _base_source("commit", "commit-" + sha[:8], "commit %s" % sha[:12], diff, repo,
                        base=parent[:12], head=sha[:12], base_sha=parent, head_sha=sha,
                        linters_ok=_linters_ok(repo, sha),
                        pr={"title": subject.strip(), "body": body.strip(),
                            "author": author.strip(), "host": "local"})


def _range(repo, ctx, spec):
    three = "..." in spec
    a, _, b = spec.partition("..." if three else "..")
    if not a or not b:
        raise ReviewError("BAD_RANGE", "range must look like A..B or A...B: %s" % spec)
    a_sha, b_sha = _need_rev(repo, a), _need_rev(repo, b)
    start = (_merge_base(repo, a_sha, b_sha) or a_sha) if three else a_sha
    diff = _git_diff(repo, ctx, start, b_sha)
    return _base_source("range", "range-%s_%s" % (a_sha[:8], b_sha[:8]),
                        "range %s" % spec, diff, repo, base=a, head=b,
                        base_sha=start, head_sha=b_sha, linters_ok=_linters_ok(repo, b_sha))


def _patch(path):
    if not os.path.isfile(path):
        raise ReviewError("PATCH_NOT_FOUND", "patch file not found: %s" % path)
    with open(path, "rb") as fh:
        text = fh.read().decode("utf-8-sig", "replace")
    base = os.path.splitext(os.path.basename(path))[0]
    return _base_source("patch", "patch-" + _safe(base), "patch file %s" % os.path.basename(path),
                        text, None, host=None)


def _stdin():
    text = sys.stdin.buffer.read().decode("utf-8-sig", "replace")
    return _base_source("patch", "patch-stdin", "diff from stdin", text, None, host=None)


# ----------------------------------------------------------------- PR sources
_GH_URL = re.compile(r"^https?://github\.com/([^/]+)/([^/]+)/pull/(\d+)", re.I)
_AZ_URL = re.compile(r"^https?://dev\.azure\.com/([^/]+)/([^/]+)/_git/([^/]+)/pullrequest/(\d+)", re.I)
_VS_URL = re.compile(r"^https?://([^./]+)\.visualstudio\.com/(?:DefaultCollection/)?([^/]+)/_git/([^/]+)/pullrequest/(\d+)", re.I)


def parse_pr_ref(ref, repo_flag=None):
    ref = ref.strip()
    m = _GH_URL.match(ref)
    if m:
        return {"host": "github", "owner": m.group(1), "repo": m.group(2),
                "number": int(m.group(3)), "url": ref}
    m = _AZ_URL.match(ref)
    if m:
        return {"host": "azure", "org": m.group(1), "project": m.group(2),
                "repo": m.group(3), "number": int(m.group(4)), "url": ref}
    m = _VS_URL.match(ref)
    if m:
        return {"host": "azure", "org": m.group(1), "project": m.group(2),
                "repo": m.group(3), "number": int(m.group(4)), "url": ref}
    m = re.match(r"^#?(\d+)$", ref)
    if m:
        d = {"host": "github", "number": int(m.group(1)), "url": None}
        if repo_flag:
            if "/" not in repo_flag:
                raise ReviewError("BAD_REPO", "--repo must be owner/repo")
            d["owner"], d["repo"] = repo_flag.split("/", 1)
        return d
    raise ReviewError("BAD_PR", "cannot understand PR reference: %s" % ref,
                      "Use a number, a GitHub PR URL or an Azure DevOps PR URL.")


def _github_pr(info, repo, ctx):
    if not shutil.which("gh"):
        raise ReviewError("GH_MISSING", "GitHub CLI (gh) is not installed",
                          "Install gh and run `gh auth login`, or use --patch/--commit/--base.")
    repo_args = ["--repo", "%s/%s" % (info["owner"], info["repo"])] if info.get("owner") else []
    n = str(info["number"])
    fields = "number,title,body,author,baseRefName,headRefName,baseRefOid,headRefOid,url,isDraft"
    rc, out, err = run(["gh", "pr", "view", n] + repo_args + ["--json", fields],
                       cwd=repo or None)
    if rc != 0:
        raise ReviewError("GH_FAILED", "gh pr view failed: %s" % err.strip()[:300],
                          "Check `gh auth status` and the PR number/repo.")
    import json
    meta = json.loads(out)
    rc, diff, err = run(["gh", "pr", "diff", n] + repo_args + ["--color", "never"],
                        cwd=repo or None)
    if rc != 0:
        raise ReviewError("GH_DIFF_FAILED", "gh pr diff failed: %s" % err.strip()[:300],
                          "Very large PRs (>20k lines) are rejected by GitHub; "
                          "use --base/--head on a local clone.")
    owner = info.get("owner")
    reponame = info.get("repo")
    if not owner:
        m = re.match(r"https?://github\.com/([^/]+)/([^/]+)/pull/", meta.get("url", ""))
        if m:
            owner, reponame = m.group(1), m.group(2)
    pr = {"host": "github", "owner": owner, "repo": reponame, "number": meta["number"],
          "url": meta.get("url"), "title": meta.get("title") or "", "body": meta.get("body") or "",
          "author": (meta.get("author") or {}).get("login", ""), "base": meta.get("baseRefName"),
          "head": meta.get("headRefName"), "base_sha": meta.get("baseRefOid"),
          "head_sha": meta.get("headRefOid"), "draft": bool(meta.get("isDraft"))}
    src = _base_source("pr", "pr-%s" % meta["number"],
                       "GitHub PR #%s: %s" % (meta["number"], pr["title"]), diff, repo,
                       base=pr["base"], head=pr["head"], base_sha=pr["base_sha"],
                       head_sha=pr["head_sha"], host="github", pr=pr)
    src["linters_ok"] = bool(repo) and _linters_ok(repo, pr["head_sha"])
    return src


def _azure_pr(info, repo, ctx):
    if not repo:
        raise ReviewError("NEEDS_CLONE", "Azure DevOps PRs need a local clone of the repo",
                          "Run from inside the clone, or use --repo-dir.")
    rc, out, _ = git(repo, "remote", "-v")
    remote = None
    for line in out.splitlines():
        parts = line.split()
        if len(parts) >= 2 and ("/_git/%s" % info["repo"]).lower() in parts[1].lower():
            remote = parts[0]
            break
    remote = remote or "origin"
    n = info["number"]
    rc, _o, err = git(repo, "fetch", "--quiet", remote, "refs/pull/%d/merge" % n)
    sha = _rev(repo, "FETCH_HEAD") if rc == 0 else None
    p1 = _rev(repo, "FETCH_HEAD^1") if sha else None
    p2 = _rev(repo, "FETCH_HEAD^2") if sha else None
    if not (sha and p1 and p2):
        ref, _s = default_base(repo)
        opts = []
        if ref:
            opts.append({"label": "Rama actual vs %s" % ref,
                         "description": "Revisar HEAD contra la rama base (merge-base).",
                         "args": ["--base", ref]})
        opts.append({"label": "Cambios locales pendientes",
                     "description": "Commits sin publicar + working tree.", "args": ["--pending"]})
        raise NeedsInput("azure_pr_unavailable", [{
            "id": "azure_fallback", "header": "Azure PR",
            "question": "No pude obtener refs/pull/%d/merge de %s (%s). ¿Qué reviso en su lugar?"
                        % (n, remote, (err.strip().splitlines() or ["sin detalle"])[-1][:120]),
            "multiSelect": False, "options": opts,
            "other_hint": "Texto libre 'base..head' (ej. origin/main..origin/feature/x) -> --range <texto>"}])
    diff = _git_diff(repo, ctx, p1, sha)
    pr = {"host": "azure", "number": n, "url": info["url"], "org": info["org"],
          "project": info["project"], "repo": info["repo"], "base_sha": p1, "head_sha": p2}
    return _base_source("pr", "pr-%d" % n, "Azure DevOps PR #%d" % n, diff, repo,
                        base=p1[:12], head=p2[:12], base_sha=p1, head_sha=p2, host="azure",
                        pr=pr, linters_ok=_linters_ok(repo, p2))


# ------------------------------------------------------------------- dispatch
def _state_candidates(repo):
    rc, out, _ = git(repo, "diff", "--cached", "--name-only")
    staged = bool(out.strip())
    rc, out, _ = git(repo, "diff", "--name-only")
    unstaged = bool(out.strip())
    rc, out, _ = git(repo, "ls-files", "--others", "--exclude-standard")
    untracked = bool(out.strip())
    ref, _sha = default_base(repo)
    unpushed = 0
    if ref:
        mb = _merge_base(repo, ref, "HEAD")
        if mb:
            rc, out, _ = git(repo, "rev-list", "--count", "%s..HEAD" % mb)
            unpushed = int(out.strip() or 0) if rc == 0 else 0
    return {"staged": staged, "uncommitted": staged or unstaged or untracked,
            "unpushed": unpushed, "ref": ref}


def resolve(args, cfg):
    ctx = int(cfg["diff"]["context_lines"])
    explicit = [n for n in ("pr", "commit", "range", "base", "staged", "working", "pending",
                            "patch", "stdin") if getattr(args, n, None)]
    if len(explicit) > 1:
        raise ReviewError("MULTIPLE_SOURCES",
                          "choose one source, got: %s" % ", ".join("--" + e for e in explicit))
    start = getattr(args, "repo_dir", None) or os.getcwd()
    root = repo_root(start)

    if args.patch:
        return _patch(args.patch)
    if args.stdin:
        return _stdin()
    if args.pr:
        info = parse_pr_ref(args.pr, getattr(args, "repo", None))
        if info["host"] == "github":
            return _github_pr(info, root, ctx)
        return _azure_pr(info, root, ctx)

    if not root:
        raise ReviewError("NOT_A_REPO", "not inside a git repository: %s" % start,
                          "Use --patch/--stdin or --repo-dir.")
    if args.commit:
        return _commit(root, ctx, args.commit)
    if args.range:
        return _range(root, ctx, args.range)
    if args.base:
        return _branch(root, ctx, args.base, getattr(args, "head", None))
    if args.staged:
        return _staged(root, ctx)
    if args.working:
        return _working(root, ctx)
    if args.pending:
        return _pending(root, ctx)

    st = _state_candidates(root)
    unc, unp = st["uncommitted"], st["unpushed"] > 0
    if not unc and not unp:
        raise ReviewError("NO_CHANGES", "nothing to review: no uncommitted changes and no "
                          "unpushed commits", "Pass --pr, --commit, --range, --base or --patch.")
    if unc and not unp:
        s = _working(root, ctx)
        s["auto_selected"] = True
        return s
    if unp and not unc:
        s = _branch(root, ctx, st["ref"], None)
        s["auto_selected"] = True
        return s
    opts = [
        {"label": "Todo lo pendiente (Recommended)",
         "description": "%d commit(s) sin publicar + cambios locales." % st["unpushed"],
         "args": ["--pending"]},
        {"label": "Solo commits sin publicar",
         "description": "HEAD contra %s." % st["ref"], "args": ["--base", st["ref"]]},
        {"label": "Solo cambios locales sin commit",
         "description": "Staged + unstaged + archivos nuevos.", "args": ["--working"]},
    ]
    if st["staged"]:
        opts.append({"label": "Solo lo staged", "description": "Solo el index.",
                     "args": ["--staged"]})
    raise NeedsInput("ambiguous_source", [{
        "id": "source", "header": "Qué revisar",
        "question": "Hay commits sin publicar y cambios locales. ¿Qué reviso?",
        "multiSelect": False, "options": opts[:4],
        "other_hint": "PR (número/URL) -> --pr <x>; commit -> --commit <sha>; "
                      "rango -> --range A..B; patch -> --patch <ruta>"}])
