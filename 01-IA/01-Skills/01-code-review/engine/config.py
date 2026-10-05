import os

from .errors import ReviewError
from .util import canonical_json, deep_merge, read_json, sha256_text

SKILL_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_config(repo_root, extra_path=None):
    """default.json  <  <repo>/.code-review/config.json  <  --config FILE."""
    cfg = read_json(os.path.join(SKILL_DIR, "config", "default.json"))
    layers = []
    if repo_root:
        layers.append(os.path.join(repo_root, ".code-review", "config.json"))
    if extra_path:
        if not os.path.isfile(extra_path):
            raise ReviewError("CONFIG_NOT_FOUND", "config file not found: %s" % extra_path)
        layers.append(extra_path)
    used = []
    for p in layers:
        if os.path.isfile(p):
            try:
                cfg = deep_merge(cfg, read_json(p))
            except ValueError as e:
                raise ReviewError("CONFIG_INVALID", "invalid JSON in %s: %s" % (p, e))
            used.append(p)
    cfg["_layers"] = used
    return cfg


def config_hash(cfg):
    clean = {k: v for k, v in cfg.items() if not k.startswith("_")}
    return sha256_text(canonical_json(clean))[:16]
