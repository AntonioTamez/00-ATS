"""Structural (non-regex) deterministic checks over the whole change set."""
import re

from .detector import is_test_path
from .diffparse import added_lines
from .findings import make
from .util import tr

CODE = {"dotnet", "python", "typescript"}
LOCKS = {"package-lock.json", "yarn.lock", "pnpm-lock.yaml", "npm-shrinkwrap.json"}
BINARY_OK = {"png", "jpg", "jpeg", "gif", "ico", "svg", "webp", "woff", "woff2", "ttf", "eot", "otf"}
_DEP_LINE = re.compile(r'^\s*"(?!version\b|name\b)[^"]+"\s*:\s*"(?:[\^~<>=]*\d|\*|latest|git|http|file:|npm:|workspace:)')

T = {
    "ANA-TST-001": {
        "title": {"es": "Cambios de código sin tests", "en": "Code changes without tests"},
        "message": {"es": "Se añaden {n} líneas de código de producción y ningún archivo de test cambia. Añade o actualiza pruebas.",
                    "en": "{n} lines of production code are added and no test file changes. Add or update tests."}},
    "ANA-DEP-001": {
        "title": {"es": "package.json cambia sin lockfile", "en": "package.json changed without lockfile"},
        "message": {"es": "Cambian dependencias pero no el lockfile; el build no será reproducible.",
                    "en": "Dependencies changed but the lockfile did not; the build will not be reproducible."}},
    "ANA-SIZE-001": {
        "title": {"es": "Cambio demasiado grande", "en": "Change too large"},
        "message": {"es": "{files} archivos y {lines} líneas modificadas (límites: {mf}/{ml}). Divídelo; los cambios grandes se revisan peor.",
                    "en": "{files} files and {lines} changed lines (limits: {mf}/{ml}). Split it; large changes get worse reviews."}},
    "ANA-HYG-001": {
        "title": {"es": "Binario añadido", "en": "Binary file added"},
        "message": {"es": "Los binarios no se pueden revisar ni diffear. Confirma que deben estar en el repo (¿Git LFS / artefacto?).",
                    "en": "Binaries cannot be reviewed or diffed. Confirm they belong in the repo (Git LFS / artifact?)."}},
    "ANA-PR-001": {
        "title": {"es": "PR sin descripción", "en": "PR without description"},
        "message": {"es": "La descripción está vacía; el revisor no sabe qué ni por qué cambia.",
                    "en": "The description is empty; the reviewer cannot tell what or why."}},
}


def _f(rid, sev, cat, lang, file=None, evidence="", **fmt):
    t = T[rid]
    return make(rid, "analyzer", sev, cat, "high", file, None, tr(t["title"], lang),
                tr(t["message"], lang).format(**fmt), evidence)


def analyze(files, source, cfg, lang):
    a = cfg["analyzers"]
    out = []
    live = [f for f in files if f["status"] != "deleted"]

    code = [f for f in live if f["reviewable"] and not f["binary"] and not is_test_path(f["path"])
            and CODE & set(f["stacks"])]
    n_added = sum(f["additions"] for f in code)
    if n_added >= a["tests_required_min_added_lines"] and not any(is_test_path(f["path"]) for f in files):
        names = ", ".join(sorted(f["path"] for f in code)[:3])
        out.append(_f("ANA-TST-001", "medium", "testing", lang, evidence=names, n=n_added))

    paths = {f["path"].rsplit("/", 1)[-1] for f in files}
    for f in live:
        if f["path"].rsplit("/", 1)[-1] == "package.json" and f["reviewable"] \
                and any(_DEP_LINE.match(t) for _n, t in added_lines(f)) and not (LOCKS & paths):
            out.append(_f("ANA-DEP-001", "low", "reliability", lang, file=f["path"]))

    total = sum(f["additions"] + f["deletions"] for f in files)
    if len(files) > a["max_files"] or total > a["max_changed_lines"]:
        out.append(_f("ANA-SIZE-001", "low", "hygiene", lang, evidence="%d files / %d lines" % (len(files), total),
                      files=len(files), lines=total, mf=a["max_files"], ml=a["max_changed_lines"]))

    for f in files:
        ext = f["path"].rsplit(".", 1)[-1].lower() if "." in f["path"] else ""
        if f["binary"] and f["status"] == "added" and ext not in BINARY_OK:
            out.append(_f("ANA-HYG-001", "low", "hygiene", lang, file=f["path"], evidence=f["path"]))

    pr = source.get("pr")
    if pr and pr.get("host") in ("github", "azure") and "body" in pr and not (pr.get("body") or "").strip():
        out.append(_f("ANA-PR-001", "low", "hygiene", lang))
    return out
