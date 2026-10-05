import datetime
import os
import tempfile
import unittest

from helpers import SKILL  # noqa: F401  (sets sys.path)
from engine import gate, planner, schema
from engine.aivalidate import validate_result
from engine.analyzers import analyze
from engine.config import load_config
from engine.detector import detect, is_test_path
from engine.diffparse import added_line_numbers, parse_diff
from engine.findings import assign_ids, dedupe, make
from engine.linters import (parse_eslint, parse_hadolint, parse_ruff, parse_shellcheck,
                            parse_tflint)
from engine.orchestrator import annotate
from engine.publish import build_payload
from engine.rules import apply_rules, load_packs
from engine.runstore import alloc_dir
from engine.util import glob_match

GIT_DIFF = """diff --git a/src/a.py b/src/a.py
index 111..222 100644
--- a/src/a.py
+++ b/src/a.py
@@ -1,3 +1,4 @@
 import os
-x = 1
+x = 2
+y = eval(z)
 print(x)
diff --git a/old.txt b/new.txt
similarity index 90%
rename from old.txt
rename to new.txt
--- a/old.txt
+++ b/new.txt
@@ -1 +1 @@
-a
+b
\\ No newline at end of file
diff --git a/img.png b/img.png
new file mode 100644
Binary files /dev/null and b/img.png differ
diff --git a/gone.cs b/gone.cs
deleted file mode 100644
--- a/gone.cs
+++ /dev/null
@@ -1,2 +0,0 @@
-class A {}
-class B {}
"""


def cfg():
    return load_config(None)


def files_for(diff):
    return annotate(parse_diff(diff), cfg())


class DiffParse(unittest.TestCase):
    def test_git_diff(self):
        fs = parse_diff(GIT_DIFF)
        self.assertEqual([f["path"] for f in fs], ["src/a.py", "new.txt", "img.png", "gone.cs"])
        a = fs[0]
        self.assertEqual((a["additions"], a["deletions"]), (2, 1))
        self.assertEqual(sorted(added_line_numbers(a)), [2, 3])
        self.assertEqual(fs[1]["status"], "renamed")
        self.assertEqual(fs[1]["old_path"], "old.txt")
        self.assertTrue(fs[2]["binary"])
        self.assertEqual(fs[3]["status"], "deleted")
        self.assertEqual(fs[3]["deletions"], 2)

    def test_plain_patch(self):
        fs = parse_diff("--- a/x.py\n+++ b/x.py\n@@ -1 +1,2 @@\n a\n+b\n--- /dev/null\n+++ b/n.py\n@@ -0,0 +1 @@\n+z\n")
        self.assertEqual([f["path"] for f in fs], ["x.py", "n.py"])
        self.assertEqual(fs[1]["status"], "added")

    def test_dashes_inside_hunk_are_content(self):
        d = "diff --git a/s.sql b/s.sql\n--- a/s.sql\n+++ b/s.sql\n@@ -1,2 +1,2 @@\n--- comment\n+-- changed\n ok\n"
        f = parse_diff(d)[0]
        self.assertEqual((f["additions"], f["deletions"]), (1, 1))

    def test_empty(self):
        self.assertEqual(parse_diff(""), [])


class Globs(unittest.TestCase):
    def test_globs(self):
        self.assertTrue(glob_match("a/b/c.min.js", "**/*.min.js"))
        self.assertTrue(glob_match("c.min.js", "**/*.min.js"))
        self.assertTrue(glob_match("x/tests/y.py", "**/tests/**"))
        self.assertTrue(glob_match("deep/dir/Program.cs", "**/Program.cs"))
        self.assertTrue(glob_match("a/b.cs", "*.cs"))
        self.assertFalse(glob_match("a/b.cs.txt", "*.cs"))
        self.assertFalse(glob_match("src/a.py", "**/tests/**"))


class Detector(unittest.TestCase):
    def test_stacks(self):
        def d(p, txt=None):
            f = {"path": p, "hunks": []}
            if txt:
                f["hunks"] = [{"lines": [("+", None, 1, l) for l in txt.split("\n")]}]
            return detect(f)
        self.assertEqual(d("Svc/A.cs"), ["dotnet"])
        self.assertEqual(d("Dockerfile"), ["docker"])
        self.assertEqual(d("x/Dockerfile.prod"), ["docker"])
        self.assertEqual(d(".github/workflows/ci.yml"), ["github-actions", "yaml"])
        self.assertEqual(d("m.yaml", "apiVersion: v1\nkind: Pod"), ["kubernetes", "yaml"])
        self.assertEqual(d("docker-compose.yml"), ["docker", "yaml"])
        self.assertEqual(d("notes.xyz"), [])

    def test_test_paths(self):
        for p in ("tests/a.py", "src/__tests__/a.ts", "a.test.ts", "Foo.Tests/B.cs", "test_x.py", "UserTests.cs"):
            self.assertTrue(is_test_path(p), p)
        for p in ("src/a.py", "latest/a.py", "contest.py"):
            self.assertFalse(is_test_path(p), p)


class RulePacks(unittest.TestCase):
    def test_all_rule_examples_pass(self):
        rules, hints, errs = load_packs(None, cfg())
        self.assertEqual(errs, [])
        self.assertGreater(len(rules), 60)
        self.assertEqual(len({r["id"] for r in rules}), len(rules))

    def _run(self, path, added, **kw):
        body = "".join("+%s\n" % l for l in added)
        diff = "diff --git a/%s b/%s\nnew file mode 100644\n--- /dev/null\n+++ b/%s\n@@ -0,0 +1,%d @@\n%s" % (
            path, path, path, len(added), body)
        fs = files_for(diff)
        rules, _h, _e = load_packs(None, cfg())
        return apply_rules(fs, rules, kw.get("cfg") or cfg(), kw.get("lang", "en"))

    def test_findings_have_lines_and_lang(self):
        f, _s, _t = self._run("a.py", ["x = 1", "r = eval(v)"])
        hit = [x for x in f if x["rule_id"] == "PY-SEC-001"]
        self.assertEqual(len(hit), 1)
        self.assertEqual(hit[0]["line"], 2)
        self.assertEqual(hit[0]["title"], "eval/exec")
        f, _s, _t = self._run("a.py", ["r = eval(v)"], lang="es")
        self.assertIn("eval", f[0]["title"])

    def test_inline_suppression(self):
        f, s, _t = self._run("a.py", ["r = eval(v)  # code-review: ignore PY-SEC-001 legacy"])
        self.assertFalse([x for x in f if x["rule_id"] == "PY-SEC-001"])
        self.assertEqual(s[0]["rule_id"], "PY-SEC-001")
        f, s, _t = self._run("a.py", ["# code-review: ignore all", "r = eval(v)"])
        self.assertFalse(f)

    def test_stack_isolation(self):
        f, _s, _t = self._run("a.cs", ["var r = eval(v);"])
        self.assertFalse([x for x in f if x["rule_id"].startswith("PY-")])

    def test_path_rule_and_ignored_files(self):
        f, _s, _t = self._run("infra/.env", ["A=1"])
        self.assertTrue([x for x in f if x["rule_id"] == "COM-SEC-009"])
        f, _s, _t = self._run("dist/app.js", ["eval(x)"])
        self.assertFalse(f)

    def test_overrides_and_cap(self):
        c = cfg()
        c["max_findings_per_rule_per_file"] = 2
        f, _s, t = self._run("a.py", ["eval(a)"] * 5, cfg=c)
        self.assertEqual(len([x for x in f if x["rule_id"] == "PY-SEC-001"]), 2)
        self.assertEqual(t[0]["count"], 3)
        c = cfg()
        c["severity_overrides"] = {"PY-SEC-001": "low"}
        f, _s, _t = self._run("a.py", ["eval(a)"], cfg=c)
        self.assertEqual(f[0]["severity"], "low")


class Analyzers(unittest.TestCase):
    def _src(self):
        return {"pr": None}

    def test_missing_tests(self):
        body = "".join("+x%d = %d\n" % (i, i) for i in range(25))
        d = "diff --git a/src/m.py b/src/m.py\nnew file mode 100644\n--- /dev/null\n+++ b/src/m.py\n@@ -0,0 +1,25 @@\n" + body
        fs = files_for(d)
        out = analyze(fs, self._src(), cfg(), "en")
        self.assertIn("ANA-TST-001", [f["rule_id"] for f in out])
        d2 = d + "diff --git a/tests/test_m.py b/tests/test_m.py\nnew file mode 100644\n--- /dev/null\n+++ b/tests/test_m.py\n@@ -0,0 +1 @@\n+pass\n"
        out = analyze(files_for(d2), self._src(), cfg(), "en")
        self.assertNotIn("ANA-TST-001", [f["rule_id"] for f in out])

    def test_package_json_without_lock(self):
        d = 'diff --git a/package.json b/package.json\n--- a/package.json\n+++ b/package.json\n@@ -1,1 +1,2 @@\n {\n+  "left-pad": "^1.3.0"\n'
        out = analyze(files_for(d), self._src(), cfg(), "en")
        self.assertIn("ANA-DEP-001", [f["rule_id"] for f in out])

    def test_empty_pr_body(self):
        d = "diff --git a/a.txt b/a.txt\nnew file mode 100644\n--- /dev/null\n+++ b/a.txt\n@@ -0,0 +1 @@\n+x\n"
        out = analyze(files_for(d), {"pr": {"host": "github", "body": " "}}, cfg(), "en")
        self.assertIn("ANA-PR-001", [f["rule_id"] for f in out])


class FindingsAndGate(unittest.TestCase):
    def mk(self, rid, sev, file, line, origin="rule", cat="security", ev="x"):
        return make(rid, origin, sev, cat, "high", file, line, "t", "m", ev)

    def test_dedupe_priority_and_ids(self):
        a = self.mk("PY-SEC-001", "high", "a.py", 10)
        b = self.mk("AI-SECURITY", "high", "a.py", 11, origin="ai")
        c = self.mk("AI-SECURITY", "medium", "a.py", 40, origin="ai")
        kept, merged = dedupe([b, c, a, dict(a)])
        self.assertEqual(merged, 2)
        self.assertEqual([k["rule_id"] for k in kept], ["PY-SEC-001", "AI-SECURITY"])
        assign_ids(kept)
        self.assertTrue(all(k["id"].startswith("F-") for k in kept))

    def test_order_is_input_independent(self):
        fs = [self.mk("R-AAA-001", "low", "b.py", 3), self.mk("R-AAA-002", "high", "a.py", 9),
              self.mk("R-AAA-003", "high", "a.py", 2)]
        k1, _ = dedupe(fs)
        k2, _ = dedupe(list(reversed(fs)))
        self.assertEqual([(f["rule_id"]) for f in k1], [f["rule_id"] for f in k2])
        self.assertEqual([f["line"] for f in k1], [2, 9, 3])

    def test_gate(self):
        g = cfg()["gate"]
        self.assertEqual(gate.decide(gate.count([]), g)[0], "PASS")
        self.assertEqual(gate.decide({"blocker": 0, "high": 0, "medium": 0, "low": 1, "info": 9}, g)[0], "PASS_WITH_WARNINGS")
        self.assertEqual(gate.decide({"blocker": 0, "high": 0, "medium": 6, "low": 0, "info": 0}, g)[0], "CHANGES_REQUESTED")
        self.assertEqual(gate.decide({"blocker": 0, "high": 1, "medium": 0, "low": 0, "info": 0}, g)[0], "CHANGES_REQUESTED")
        self.assertEqual(gate.decide({"blocker": 1, "high": 5, "medium": 0, "low": 0, "info": 0}, g)[0], "BLOCKED")
        self.assertEqual(gate.decide({"blocker": 0, "high": 0, "medium": 0, "low": 0, "info": 5}, g)[0], "PASS")


class RunStore(unittest.TestCase):
    def test_unique_dirs(self):
        with tempfile.TemporaryDirectory() as t:
            day = datetime.date(2026, 1, 2)
            names = [os.path.basename(alloc_dir(t, "pr-7", day)) for _ in range(3)]
            self.assertEqual(names, ["pr-7-20260102", "pr-7-20260102-2", "pr-7-20260102-3"])
            self.assertTrue(os.path.isfile(os.path.join(t, ".gitignore")))


class Schema(unittest.TestCase):
    def test_validator(self):
        s = {"type": "object", "required": ["a"], "properties": {
            "a": {"type": "integer", "minimum": 1}, "b": {"type": "string", "enum": ["x", "y"]},
            "c": {"type": "array", "items": {"type": "number"}, "maxItems": 2}}}
        self.assertEqual(schema.validate({"a": 1, "b": "x", "c": [1.5]}, s), [])
        self.assertTrue(schema.validate({}, s))
        self.assertTrue(schema.validate({"a": True}, s))
        self.assertTrue(schema.validate({"a": 0}, s))
        self.assertTrue(schema.validate({"a": 1, "b": "z"}, s))
        self.assertTrue(schema.validate({"a": 1, "c": [1, 2, 3]}, s))
        self.assertTrue(schema.validate({"a": 1, "c": ["q"]}, s))


class AiValidation(unittest.TestCase):
    DIFF = "diff --git a/a.py b/a.py\nnew file mode 100644\n--- /dev/null\n+++ b/a.py\n@@ -0,0 +1,3 @@\n+def f(x):\n+    return x / 0\n+print('ok')\n"

    def item(self, **kw):
        d = {"file": "a.py", "line": 2, "severity": "high", "category": "correctness",
             "title": "Division by zero", "explanation": "Always raises ZeroDivisionError.",
             "evidence": "return x / 0", "confidence": 0.9}
        d.update(kw)
        return d

    def run_one(self, *items, **cfgover):
        c = cfg()
        c["ai"].update(cfgover)
        fs = {f["path"]: f for f in files_for(self.DIFF)}
        req = {"id": "correctness-01", "lens": "correctness", "files": ["a.py"]}
        return validate_result({"lens": "correctness", "findings": list(items)}, req, fs, c)

    def test_accepts_verified(self):
        acc, rej, errs = self.run_one(self.item())
        self.assertEqual((len(acc), len(rej), errs), (1, 0, []))
        self.assertEqual(acc[0]["origin"], "ai")

    def test_rejections(self):
        cases = {
            "file_not_in_diff": self.item(file="zzz.py"),
            "line_not_changed": self.item(line=99),
            "evidence_not_found": self.item(evidence="return x / 1"),
            "low_confidence": self.item(confidence=0.2),
            "bad_line_span": self.item(end_line=200),
        }
        for reason, it in cases.items():
            acc, rej, errs = self.run_one(it)
            self.assertEqual(acc, [], reason)
            self.assertEqual(rej[0]["reason"], reason)

    def test_whitespace_tolerant_evidence_and_severity_cap(self):
        acc, rej, _ = self.run_one(self.item(evidence="return   x /  0", severity="blocker"))
        self.assertEqual(acc[0]["severity"], "high")

    def test_schema_errors_and_wrong_lens(self):
        acc, rej, errs = self.run_one({"file": "a.py"})
        self.assertTrue(errs)
        fs = {f["path"]: f for f in files_for(self.DIFF)}
        _a, _r, errs = validate_result({"lens": "security", "findings": []},
                                       {"id": "x", "lens": "correctness", "files": []}, fs, cfg())
        self.assertTrue(errs)


class Planner(unittest.TestCase):
    def make(self, n):
        parts = []
        for i in range(n):
            parts.append("diff --git a/src/f%02d.py b/src/f%02d.py\nnew file mode 100644\n--- /dev/null\n+++ b/src/f%02d.py\n@@ -0,0 +1,2 @@\n+a = 1\n+b = 2\n" % (i, i, i))
        parts.append("diff --git a/src/auth/login.py b/src/auth/login.py\nnew file mode 100644\n--- /dev/null\n+++ b/src/auth/login.py\n@@ -0,0 +1 @@\n+x = 1\n")
        return files_for("".join(parts))

    def test_scope(self):
        fs = self.make(5)
        sel, dropped = planner.select(fs, "risk:1")
        self.assertEqual([f["path"] for f in sel], ["src/auth/login.py"])
        sel, _ = planner.select(fs, "path:src/f0*.py")
        self.assertEqual(len(sel), 5)
        with self.assertRaises(ValueError):
            planner.parse_scope("bogus")

    def test_chunks_deterministic_and_capped(self):
        fs = self.make(30)
        sel, _ = planner.select(fs, "all")
        c1, _ = planner.build_chunks(sel, 800)
        c2, _ = planner.build_chunks(sel, 800)
        self.assertEqual([c["text"] for c in c1], [c["text"] for c in c2])
        self.assertGreater(len(c1), 3)
        kept, dropped = planner.cap_chunks(c1, 4, 8)
        self.assertEqual(len(kept), 2)
        self.assertTrue(any("auth/login" in p for c in kept for p in c["files"]))

    def test_big_hunk_truncated(self):
        body = "".join("+line %d\n" % i for i in range(500))
        d = "diff --git a/big.py b/big.py\nnew file mode 100644\n--- /dev/null\n+++ b/big.py\n@@ -0,0 +1,500 @@\n" + body
        sel, _ = planner.select(files_for(d), "all")
        chunks, trunc = planner.build_chunks(sel, 1500)
        self.assertEqual(trunc, ["big.py"])
        self.assertTrue(all(len(c["text"]) < 2500 for c in chunks))


class LinterParsers(unittest.TestCase):
    def test_parsers(self):
        r = parse_ruff('[{"code":"F401","filename":"/r/a.py","location":{"row":3,"column":1},"message":"unused"}]')
        self.assertEqual((r[0]["line"], r[0]["code"]), (3, "F401"))
        e = parse_eslint('[{"filePath":"/r/a.js","messages":[{"ruleId":"no-eval","severity":2,"message":"m","line":4}]}]')
        self.assertEqual((e[0]["line"], e[0]["native"]), (4, 2))
        s = parse_shellcheck('{"comments":[{"file":"a.sh","line":2,"level":"warning","code":2086,"message":"q"}]}')
        self.assertEqual(s[0]["code"], "SC2086")
        h = parse_hadolint('[{"code":"DL3006","file":"Dockerfile","line":1,"level":"warning","message":"tag"}]')
        self.assertEqual(h[0]["native"], "warning")
        t = parse_tflint('{"issues":[{"rule":{"name":"r","severity":"error"},"message":"m","range":{"filename":"main.tf","start":{"line":9}}}]}')
        self.assertEqual((t[0]["file"], t[0]["line"]), ("main.tf", 9))
        self.assertEqual(parse_ruff(""), [])


class Publish(unittest.TestCase):
    def rep(self, host="github"):
        f1 = make("PY-SEC-001", "rule", "high", "security", "high", "a.py", 3, "eval", "bad", "eval(x)")
        f2 = make("ANA-TST-001", "analyzer", "medium", "testing", "high", None, None, "no tests", "add", "")
        f3 = make("PY-HYG-001", "rule", "low", "hygiene", "high", "a.py", 8, "print", "log", "print(1)")
        return {"verdict": "CHANGES_REQUESTED", "complete": True, "incomplete_reasons": [],
                "fingerprint": "abc", "counts": {"blocker": 0, "high": 1, "medium": 1, "low": 1, "info": 0},
                "findings": [f1, f2, f3],
                "source": {"pr": {"host": host, "owner": "o", "repo": "r", "number": 5, "head_sha": "deadbeef"}}}

    def test_payload(self):
        p, _pr = build_payload(self.rep(), "medium")
        self.assertEqual(len(p["comments"]), 1)
        self.assertEqual(p["comments"][0]["path"], "a.py")
        self.assertEqual(p["comments"][0]["line"], 3)
        self.assertEqual(p["commit_id"], "deadbeef")
        self.assertIn("no tests", p["body"])
        p, _ = build_payload(self.rep(), "low")
        self.assertEqual(len(p["comments"]), 2)

    def test_not_github(self):
        from engine.errors import ReviewError
        with self.assertRaises(ReviewError):
            build_payload(self.rep("azure"), "medium")


if __name__ == "__main__":
    unittest.main()
