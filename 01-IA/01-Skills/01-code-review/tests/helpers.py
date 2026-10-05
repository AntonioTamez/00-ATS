import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest

SKILL = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if SKILL not in sys.path:
    sys.path.insert(0, SKILL)

from engine import cli  # noqa: E402


def git(repo, *args):
    p = subprocess.run(["git", "-c", "user.email=t@t.t", "-c", "user.name=t",
                        "-c", "core.autocrlf=false", "-C", repo] + list(args),
                       capture_output=True, text=True, encoding="utf-8")
    if p.returncode != 0:
        raise RuntimeError(p.stderr)
    return p.stdout


def jload(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def rtext(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def write(repo, rel, text):
    path = os.path.join(repo, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def review(*argv):
    """Run the CLI in-process. Returns (exit_code, envelope_dict)."""
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        code = cli.main(list(argv))
    return code, json.loads(buf.getvalue())


class RepoCase(unittest.TestCase):
    """Fresh git repo with one commit on `main`."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = os.path.realpath(self._tmp.name)
        git(self.repo, "init", "-q", "-b", "main")
        write(self.repo, "README.md", "# demo\n")
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "init")

    def commit(self, rel, text, msg="c"):
        write(self.repo, rel, text)
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", msg)
        return git(self.repo, "rev-parse", "HEAD").strip()

    def run_review(self, *argv):
        return review("run", "--repo-dir", self.repo, *argv)
