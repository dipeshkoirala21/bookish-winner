#!/usr/bin/env python3
"""Render OBJ meshes to PNG preview sheets (numpy + Pillow software rasteriser). See README.md.

    python3 tools/mesh-preview/render.py in.obj -o out.png [--views N] [--az A --el E --dist D] [--size 900]
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from meshpreview.cli import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
