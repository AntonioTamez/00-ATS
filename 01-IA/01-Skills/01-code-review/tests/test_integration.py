import json
import os
import unittest

from helpers import RepoCase, git, jload, review, rtext, write

BAD = "import os\nDEBUG = True\n\ndef f(items=[]):\n    return eval(input())\n"


class Sources(RepoCase):
    def test_no_changes_is_error(self):
        code, env = self.run_review("--no-ai")
        self.assertEqual(code, 3)
        self.assertEqual(env["code"], "NO_CHANGES")

    def test_working_auto_selected_with_untracked(self):
        write(self.repo, "src/a.py", BAD)
        code, env = self.run_review("--no-ai")
        self.assertEqual(code, 0, env)
        self.assertEqual(env["source"]["kind"], "working")
        ids = {f["rule_id"] for f in jload(env["report_json"])["findings"]}
        self.assertIn("PY-SEC-001", ids)
        self.assertIn("PY-COR-001", ids)

    def test_commit_and_range_and_base(self):
        sha = self.commit("src/a.py", BAD)
        code, env = self.run_review("--no-ai", "--commit", sha)
        self.assertEqual(env["source"]["kind"], "commit")
        self.assertEqual(env["counts"]["high"], 1)
        code, env = self.run_review("--no-ai", "--range", "HEAD~1..HEAD")
        self.assertEqual(env["source"]["kind"], "range")
        self.assertEqual(env["counts"]["high"], 1)
        code, env = self.run_review("--no-ai", "--base", "HEAD~1")
        self.assertEqual(env["source"]["kind"], "branch")
        code, env = self.run_review("--no-ai", "--commit", "doesnotexist")
        self.assertEqual(env["code"], "BAD_REF")

    def test_staged_only(self):
        write(self.repo, "a.py", "eval(x)\n")
        git(self.repo, "add", "a.py")
        write(self.repo, "b.py", "eval(y)\n")
        code, env = self.run_review("--no-ai", "--staged")
        rep = jload(env["report_json"])
        self.assertEqual({f["file"] for f in rep["findings"]}, {"a.py"})

    def test_patch_file_and_outside_repo(self):
        sha = self.commit("src/a.py", BAD)
        patch = git(self.repo, "diff", "HEAD~1", "HEAD")
        p = os.path.join(self.repo, "..", "x.patch")
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(patch)
        code, env = self.run_review("--no-ai", "--patch", p, "--out-dir", os.path.join(self.repo, "out"))
        self.assertEqual(code, 0, env)
        self.assertEqual(env["source"]["kind"], "patch")
        self.assertIn(os.path.join(self.repo, "out"), env["run_dir"])

    def test_ambiguous_asks_user(self):
        git(self.repo, "checkout", "-qb", "feature")
        self.commit("src/a.py", BAD)
        write(self.repo, "b.py", "x = 1\n")
        code, env = self.run_review("--no-ai")
        self.assertEqual(code, 20, env)
        self.assertEqual(env["status"], "needs_input")
        q = env["questions"][0]
        self.assertGreaterEqual(len(q["options"]), 2)
        self.assertEqual(q["options"][0]["args"], ["--pending"])
        # answering with the first option works
        code, env = self.run_review("--no-ai", *q["options"][0]["args"])
        self.assertEqual(code, 0, env)
        rep = jload(env["report_json"])
        self.assertEqual({f["path"] for f in rep["files"]}, {"src/a.py", "b.py"})

    def test_multiple_sources_rejected(self):
        code, env = self.run_review("--no-ai", "--staged", "--working")
        self.assertEqual(env["code"], "MULTIPLE_SOURCES")

    def test_bad_pr_ref(self):
        code, env = self.run_review("--no-ai", "--pr", "not-a-pr")
        self.assertEqual(env["code"], "BAD_PR")


class Determinism(RepoCase):
    def test_same_input_same_result_new_folder(self):
        self.commit("src/a.py", BAD)
        c1, e1 = self.run_review("--no-ai", "--commit", "HEAD")
        c2, e2 = self.run_review("--no-ai", "--commit", "HEAD")
        self.assertEqual(e1["fingerprint"], e2["fingerprint"])
        self.assertNotEqual(e1["run_dir"], e2["run_dir"])
        self.assertTrue(e2["run_dir"].endswith("-2"))
        r1 = jload(e1["report_json"])
        r2 = jload(e2["report_json"])
        self.assertEqual(r1["findings"], r2["findings"])
        self.assertEqual(r2["run"]["same_diff_as"][0]["fingerprint"], r1["fingerprint"])
        # reviews root is stable and self-ignored
        root = os.path.dirname(e1["run_dir"])
        self.assertTrue(root.endswith(os.path.join(".code-review", "reviews")))
        self.assertTrue(os.path.isfile(os.path.join(root, ".gitignore")))
        self.assertEqual(git(self.repo, "status", "--porcelain").strip(), "")

    def test_language_and_exit_code(self):
        self.commit("src/a.py", BAD)
        code, env = self.run_review("--no-ai", "--commit", "HEAD", "--lang", "en", "--exit-code")
        self.assertEqual(code, 1)
        self.assertIn("# Code review", rtext(env["report_md"]))
        code, env = self.run_review("--no-ai", "--commit", "HEAD", "--lang", "es")
        self.assertEqual(code, 0)
        self.assertIn("# Revisión de código", rtext(env["report_md"]))

    def test_blocked_exit_code(self):
        self.commit("k.txt", "-----BEGIN RSA PRIVATE KEY-----\n")
        code, env = self.run_review("--no-ai", "--commit", "HEAD", "--exit-code")
        self.assertEqual((env["verdict"], code), ("BLOCKED", 2))

    def test_repo_config_overrides(self):
        self.commit("src/a.py", BAD)
        write(self.repo, ".code-review/config.json", json.dumps(
            {"disable_rules": ["PY-SEC-001"], "severity_overrides": {"PY-COR-001": "info"}}))
        git(self.repo, "add", "-f", ".code-review/config.json")
        git(self.repo, "commit", "-qm", "cfg")
        code, env = self.run_review("--no-ai", "--commit", "HEAD~1")
        rep = jload(env["report_json"])
        by = {f["rule_id"]: f["severity"] for f in rep["findings"]}
        self.assertNotIn("PY-SEC-001", by)
        self.assertEqual(by["PY-COR-001"], "info")

    def test_team_rules_from_repo(self):
        write(self.repo, ".code-review/rules/team.json", json.dumps({"pack": "team", "rules": [{
            "id": "TEAM-SEC-001", "severity": "medium", "category": "security", "confidence": "high",
            "stacks": ["*"], "pattern": "forbidden_call\\(",
            "title": {"es": "Llamada prohibida", "en": "Forbidden call"},
            "message": {"es": "No usar", "en": "Do not use"},
            "examples": {"match": ["forbidden_call(1)"], "nomatch": ["allowed_call(1)"]}}]}))
        self.commit("x.py", "forbidden_call(1)\n")
        code, env = self.run_review("--no-ai", "--commit", "HEAD")
        rep = jload(env["report_json"])
        self.assertIn("TEAM-SEC-001", {f["rule_id"] for f in rep["findings"]})


class NeedsInputFlows(RepoCase):
    def test_unknown_stack_asks(self):
        self.commit("data.zzz", "hello\n")
        code, env = self.run_review("--no-ai", "--commit", "HEAD")
        self.assertEqual(code, 20)
        self.assertEqual(env["reason"], "unknown_stack")
        self.assertIsNone(env["questions"][0]["options"][1]["args"])
        code, env = self.run_review("--no-ai", "--commit", "HEAD", "--accept-generic")
        self.assertEqual(env["status"], "done")

    def test_large_diff_asks_scope(self):
        for i in range(45):
            write(self.repo, "src/f%02d.py" % i, "a = 1\n")
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "many")
        code, env = self.run_review("--commit", "HEAD")
        self.assertEqual(code, 20)
        self.assertEqual(env["reason"], "large_diff")
        opts = {o["label"].split(" (")[0]: o["args"] for o in env["questions"][0]["options"]}
        self.assertEqual(opts["Top 25 por riesgo"], ["--scope", "risk:25"])
        code, env = self.run_review("--commit", "HEAD", *opts["Top 25 por riesgo"])
        self.assertEqual(code, 10)
        self.assertEqual(len({p["lens"] for p in env["pending"]}), 4)
        covered = {f for p in env["pending"] for f in p["files"]}
        self.assertEqual(len(covered), 25)


class AiFlow(RepoCase):
    def setUp(self):
        super().setUp()
        self.sha = self.commit("src/app.py", "def div(x):\n    return x / 0\n\ndef g():\n    return eval(input())\n")

    def finding(self, **kw):
        d = {"file": "src/app.py", "line": 2, "severity": "high", "category": "correctness",
             "title": "Division by zero", "explanation": "Always raises ZeroDivisionError on call.",
             "evidence": "return x / 0", "confidence": 0.9}
        d.update(kw)
        return d

    def answer(self, p, findings, lens=None):
        with open(p["result"], "w", encoding="utf-8") as f:
            json.dump({"lens": lens or p["lens"], "findings": findings}, f)

    def test_full_cycle_with_validation_and_retry(self):
        code, env = self.run_review("--commit", self.sha, "--only", "correctness,security")
        self.assertEqual((code, env["status"]), (10, "awaiting_agents"))
        pend = {p["lens"]: p for p in env["pending"]}
        req = jload(pend["correctness"]["request"])
        self.assertIn("UNTRUSTED", req["user"])
        self.assertIn("L2 | +", req["user"])
        self.assertIn("Spanish", req["system"])
        self.assertIn("PY-SEC-001", req["user"])  # deterministic finding listed as already reported
        # correctness: one real finding + one hallucinated; security: invalid output
        self.answer(pend["correctness"], [self.finding(), self.finding(line=1, evidence="return x / 0", title="Wrong line")])
        with open(pend["security"]["result"], "w") as f:
            f.write("not json")
        code, env = self.run_review_resume(env["run_dir"])
        self.assertEqual((code, env["status"]), (10, "awaiting_agents"))
        self.assertEqual([p["lens"] for p in env["pending"]], ["security"])
        self.assertEqual(env["pending"][0]["attempt"], 2)
        req = jload(env["pending"][0]["request"])
        self.assertTrue(req["previous_errors"])
        self.answer(env["pending"][0], [])
        code, env = self.run_review_resume(env["run_dir"])
        self.assertEqual(env["status"], "done", env)
        rep = jload(env["report_json"])
        ai = [f for f in rep["findings"] if f["origin"] == "ai"]
        self.assertEqual([f["line"] for f in ai], [2])
        self.assertEqual(rep["ai"]["completed"], 2)
        self.assertEqual(len(rep["rejected_ai"]), 1)
        self.assertEqual(rep["rejected_ai"][0]["reason"], "evidence_not_found")
        self.assertTrue(rep["complete"])
        md = rtext(env["report_md"])
        self.assertIn("Division by zero", md)

    def test_failed_agent_marks_incomplete(self):
        code, env = self.run_review("--commit", self.sha, "--only", "security")
        run_dir = env["run_dir"]
        for _ in range(2):  # never answers -> 2 attempts -> failed
            code, env = self.run_review_resume(run_dir)
        self.assertEqual(env["status"], "done")
        self.assertFalse(env["complete"])
        self.assertIn("security-01", env["incomplete_reasons"][0])

    def run_review_resume(self, run_dir):
        return review("resume", "--run", run_dir)

    def test_no_ai_never_waits(self):
        code, env = self.run_review("--commit", self.sha, "--no-ai")
        self.assertEqual(env["status"], "done")

    def test_bad_lens(self):
        code, env = self.run_review("--commit", self.sha, "--only", "nope")
        self.assertEqual(env["code"], "BAD_LENS")


if __name__ == "__main__":
    unittest.main()


class AzurePr(RepoCase):
    """Simulates Azure Repos: a remote whose URL contains /_git/<repo> exposing refs/pull/N/merge."""

    def test_merge_ref_fetch_and_fallback(self):
        import tempfile
        with tempfile.TemporaryDirectory() as t:
            bare = os.path.join(os.path.realpath(t), "_git", "myrepo")
            os.makedirs(bare)
            git(bare, "init", "-q", "--bare")
            git(self.repo, "remote", "add", "origin", bare)
            git(self.repo, "push", "-q", "origin", "main")
            git(self.repo, "checkout", "-qb", "feature")
            self.commit("src/a.py", BAD)
            git(self.repo, "checkout", "-q", "main")
            git(self.repo, "merge", "-q", "--no-ff", "-m", "pr merge", "feature")
            git(self.repo, "push", "-q", "origin", "HEAD:refs/pull/5/merge")
            git(self.repo, "reset", "-q", "--hard", "HEAD~1")
            url = "https://dev.azure.com/org/proj/_git/myrepo/pullrequest/5"
            code, env = self.run_review("--no-ai", "--pr", url)
            self.assertEqual(code, 0, env)
            self.assertEqual(env["source"]["host"], "azure")
            rep = jload(env["report_json"])
            self.assertEqual([f["path"] for f in rep["files"]], ["src/a.py"])
            self.assertEqual(rep["source"]["pr"]["number"], 5)
            self.assertFalse(env["publish_available"])
            # PR 99 has no merge ref -> deterministic fallback question
            code, env = self.run_review("--no-ai", "--pr", url.replace("/5", "/99"))
            self.assertEqual(code, 20)
            self.assertEqual(env["reason"], "azure_pr_unavailable")
            self.assertIn("--range", env["questions"][0]["other_hint"])


class PrRefParsing(unittest.TestCase):
    def test_parse(self):
        from engine.sources import parse_pr_ref
        self.assertEqual(parse_pr_ref("https://github.com/o/r/pull/12")["number"], 12)
        self.assertEqual(parse_pr_ref("#7", "o/r")["owner"], "o")
        self.assertEqual(parse_pr_ref("42")["host"], "github")
        a = parse_pr_ref("https://acme.visualstudio.com/DefaultCollection/P/_git/R/pullrequest/3")
        self.assertEqual((a["host"], a["org"], a["repo"], a["number"]), ("azure", "acme", "R", 3))
