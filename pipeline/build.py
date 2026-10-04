#!/usr/bin/env python3
"""Build a region: extract, inference, tiles, pack, search index, routing graph, QA (thin wrapper).

    python build.py --region thamel_test|kathmandu_valley [--pbf PATH] [--raw-dir PATH]
                    [--out build/] [--qa] [--no-cache] [--stages tiles,search,routing,qa]

See ghumante_pipeline/build.py for the behaviour.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from ghumante_pipeline.build import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
