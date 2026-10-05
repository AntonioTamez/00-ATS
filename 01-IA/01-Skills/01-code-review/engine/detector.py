"""Deterministic technology detection from path (+ hunk content for k8s)."""
import re

from .diffparse import hunk_text

EXT = {
    ".cs": "dotnet", ".csproj": "dotnet", ".sln": "dotnet", ".cshtml": "dotnet",
    ".razor": "dotnet", ".props": "dotnet", ".targets": "dotnet", ".vb": "dotnet",
    ".py": "python", ".pyi": "python",
    ".ts": "typescript", ".tsx": "typescript", ".js": "typescript", ".jsx": "typescript",
    ".mjs": "typescript", ".cjs": "typescript", ".vue": "typescript",
    ".tf": "terraform", ".tfvars": "terraform",
    ".sh": "shell", ".bash": "shell", ".zsh": "shell",
    ".ps1": "powershell", ".psm1": "powershell",
    ".sql": "sql",
    ".yml": "yaml", ".yaml": "yaml",
}
NAME = {"dockerfile": "docker", "containerfile": "docker"}
CODE_STACKS = {"dotnet", "python", "typescript", "powershell", "shell", "sql"}
# Text files with no code rules: reviewed only by the generic (`*`) rules.
KNOWN_OTHER = {".md", ".txt", ".json", ".xml", ".toml", ".ini", ".cfg", ".conf", ".csv",
               ".html", ".css", ".scss", ".less", ".svg", ".gitignore", ".editorconfig",
               ".config", ".resx", ".rst", ".lock", ".env", ".properties", ".gradle",
               ".bat", ".cmd"}

_K8S_PATH = re.compile(r"(^|/)(k8s|kubernetes|manifests?|helm|charts?|deploy(ment)?s?)(/|$)", re.I)
_TEST_PATH = re.compile(
    r"(^|/)(tests?|__tests__|specs?|e2e|integration-?tests?|unit-?tests?)(/|$)"
    r"|\.(tests?|spec)\.[a-z0-9]+$|_tests?\.(py|go|js|ts)$|(^|/)test_[^/]*\.py$"
    r"|(^|/)[^/]*\.tests?(/|$)|[^/]Tests?\.cs$|[^/]Tests?\.vb$", re.I)


def is_test_path(path):
    return bool(_TEST_PATH.search(path))


def _ext(path):
    base = path.rsplit("/", 1)[-1]
    if "." not in base:
        return ""
    return "." + base.rsplit(".", 1)[-1].lower()


def detect(f):
    """Stacks for one file dict (sorted, unique). Empty list = unknown technology."""
    path = f["path"]
    base = path.rsplit("/", 1)[-1]
    low = base.lower()
    stacks = set()
    if low in NAME or low.startswith("dockerfile.") or low.endswith(".dockerfile"):
        stacks.add("docker")
    ext = _ext(path)
    if ext in EXT:
        stacks.add(EXT[ext])
    if "yaml" in stacks:
        if re.search(r"(^|/)\.github/(workflows|actions)/", path):
            stacks.add("github-actions")
        txt = hunk_text(f) if f.get("hunks") else ""
        if _K8S_PATH.search(path) or (re.search(r"^\s*apiVersion:", txt, re.M)
                                       and re.search(r"^\s*kind:", txt, re.M)):
            stacks.add("kubernetes")
        if re.match(r"docker-compose.*\.ya?ml$|compose\.ya?ml$", low):
            stacks.add("docker")
    return sorted(stacks)


def is_known_text(path):
    low = path.rsplit("/", 1)[-1].lower()
    return _ext(path) in KNOWN_OTHER or low in {"readme", "license", "makefile", "codeowners"}
