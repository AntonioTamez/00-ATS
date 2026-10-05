"""Minimal JSON-Schema validator (stdlib only). Supports the subset used by our schemas:
type, enum, required, properties, additionalProperties, items, minimum, maximum,
minLength, maxLength, pattern, minItems, maxItems."""
import re


def _type_ok(v, t):
    if t == "object":
        return isinstance(v, dict)
    if t == "array":
        return isinstance(v, list)
    if t == "string":
        return isinstance(v, str)
    if t == "integer":
        return isinstance(v, int) and not isinstance(v, bool)
    if t == "number":
        return isinstance(v, (int, float)) and not isinstance(v, bool)
    if t == "boolean":
        return isinstance(v, bool)
    if t == "null":
        return v is None
    return True


def validate(value, schema, path="$"):
    errs = []
    t = schema.get("type")
    if t:
        types = t if isinstance(t, list) else [t]
        if not any(_type_ok(value, x) for x in types):
            return ["%s: expected %s, got %s" % (path, "/".join(types), type(value).__name__)]
    if "enum" in schema and value not in schema["enum"]:
        errs.append("%s: %r not in %s" % (path, value, schema["enum"]))
    if isinstance(value, str):
        if "minLength" in schema and len(value) < schema["minLength"]:
            errs.append("%s: shorter than %d" % (path, schema["minLength"]))
        if "maxLength" in schema and len(value) > schema["maxLength"]:
            errs.append("%s: longer than %d" % (path, schema["maxLength"]))
        if "pattern" in schema and not re.search(schema["pattern"], value):
            errs.append("%s: does not match %s" % (path, schema["pattern"]))
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            errs.append("%s: < %s" % (path, schema["minimum"]))
        if "maximum" in schema and value > schema["maximum"]:
            errs.append("%s: > %s" % (path, schema["maximum"]))
    if isinstance(value, dict):
        for r in schema.get("required", []):
            if r not in value:
                errs.append("%s: missing required '%s'" % (path, r))
        props = schema.get("properties", {})
        for k, v in value.items():
            if k in props:
                errs += validate(v, props[k], "%s.%s" % (path, k))
            elif schema.get("additionalProperties") is False:
                errs.append("%s: unexpected property '%s'" % (path, k))
    if isinstance(value, list):
        if "minItems" in schema and len(value) < schema["minItems"]:
            errs.append("%s: fewer than %d items" % (path, schema["minItems"]))
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            errs.append("%s: more than %d items" % (path, schema["maxItems"]))
        if "items" in schema:
            for i, v in enumerate(value):
                errs += validate(v, schema["items"], "%s[%d]" % (path, i))
    return errs
