"""Unified-diff parser (git style and plain `---/+++` patches).

File dict:  path, old_path, status (added|modified|deleted|renamed), binary,
            additions, deletions, hunks
Hunk dict:  old_start, old_lines, new_start, new_lines, header, lines
Line tuple: (type '+'|'-'|' ', old_no|None, new_no|None, text)
"""
import re

_HUNK = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$")
_GIT = re.compile(r'^diff --git (?:"a/(.+?)"|a/(.+?)) (?:"b/(.+?)"|b/(.+?))$')


def _clean(p):
    p = p.strip()
    if p.startswith('"') and p.endswith('"'):
        p = p[1:-1]
    if p.startswith(("a/", "b/")):
        p = p[2:]
    return p


def _new_file(path, old, git):
    return {"path": path, "old_path": old, "status": "modified", "binary": False,
            "additions": 0, "deletions": 0, "hunks": [], "_git": git}


def parse_diff(text):
    files, f, hunk = [], None, None
    old_left = new_left = 0
    lines = text.replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    old_no = new_no = 0
    i = 0
    while i < len(lines):
        line = lines[i]
        i += 1
        if hunk is not None and (old_left > 0 or new_left > 0):
            if line.startswith("\\"):
                continue
            t = line[:1] if line else " "
            body = line[1:] if line else ""
            if t == "+":
                hunk["lines"].append(("+", None, new_no, body))
                f["additions"] += 1
                new_no += 1
                new_left -= 1
            elif t == "-":
                hunk["lines"].append(("-", old_no, None, body))
                f["deletions"] += 1
                old_no += 1
                old_left -= 1
            else:
                hunk["lines"].append((" ", old_no, new_no, body))
                old_no += 1
                new_no += 1
                old_left -= 1
                new_left -= 1
            continue
        if line.startswith("\\"):
            continue
        hunk = None
        m = _GIT.match(line)
        if m:
            old = m.group(1) or m.group(2)
            new = m.group(3) or m.group(4)
            f = _new_file(new, old, True)
            files.append(f)
            continue
        if line.startswith("--- ") and (f is None or not f["_git"]):
            if i < len(lines) and lines[i].startswith("+++ "):
                oldp = _clean(line[4:].split("\t")[0])
                newp = _clean(lines[i][4:].split("\t")[0])
                path = newp if newp != "/dev/null" else oldp
                f = _new_file(path, oldp, False)
                if oldp == "/dev/null":
                    f["status"] = "added"
                if newp == "/dev/null":
                    f["status"] = "deleted"
                files.append(f)
                i += 1
            continue
        if f is None:
            continue
        if line.startswith("--- "):
            op = line[4:].split("\t")[0]
            if op.strip() == "/dev/null":
                f["status"] = "added"
        elif line.startswith("+++ "):
            np_ = line[4:].split("\t")[0]
            if np_.strip() == "/dev/null":
                f["status"] = "deleted"
            else:
                f["path"] = _clean(np_)
        elif line.startswith("new file mode"):
            f["status"] = "added"
        elif line.startswith("deleted file mode"):
            f["status"] = "deleted"
        elif line.startswith("rename from "):
            f["status"] = "renamed"
            f["old_path"] = line[len("rename from "):].strip()
        elif line.startswith("rename to "):
            f["status"] = "renamed"
            f["path"] = line[len("rename to "):].strip()
        elif line.startswith("Binary files ") or line.startswith("GIT binary patch"):
            f["binary"] = True
        else:
            hm = _HUNK.match(line)
            if hm:
                os_, oc, ns, nc, head = hm.groups()
                old_no, new_no = int(os_), int(ns)
                old_left = int(oc) if oc is not None else 1
                new_left = int(nc) if nc is not None else 1
                hunk = {"old_start": old_no, "old_lines": old_left, "new_start": new_no,
                        "new_lines": new_left, "header": head.strip(), "lines": []}
                f["hunks"].append(hunk)
    for x in files:
        x.pop("_git", None)
    return files


def added_lines(f):
    """[(new_no, text)] for every added line of the file."""
    return [(l[2], l[3]) for h in f["hunks"] for l in h["lines"] if l[0] == "+"]


def new_side_lines(f):
    """{new_no: text} for added + context lines (the file as seen after the change)."""
    return {l[2]: l[3] for h in f["hunks"] for l in h["lines"] if l[2] is not None}


def added_line_numbers(f):
    return {l[2] for h in f["hunks"] for l in h["lines"] if l[0] == "+"}


def hunk_text(f):
    return "\n".join(l[3] for h in f["hunks"] for l in h["lines"] if l[0] != "-")


def render_hunk(h):
    """Annotated hunk for AI prompts: `  L123 | + code` / `      | - removed`."""
    out = ["@@ -%d,%d +%d,%d @@ %s" % (h["old_start"], h["old_lines"], h["new_start"],
                                        h["new_lines"], h["header"])]
    for t, _o, n, x in h["lines"]:
        out.append("%6s | %s %s" % (("L%d" % n) if n is not None else "", t, x))
    return "\n".join(out)
