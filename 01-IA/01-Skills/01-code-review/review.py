#!/usr/bin/env python3
"""Entry point of the code-review engine. Python >= 3.10, standard library only."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from engine.cli import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
