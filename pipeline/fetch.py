#!/usr/bin/env python3
"""Download the source data a region needs (thin wrapper).

    python fetch.py --region kathmandu_valley [--only osm,dem,landcover]
                    [--force] [--verify] [--raw-dir PATH] [--dry-run]

See ghumante_pipeline/fetch.py for the behaviour.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from ghumante_pipeline.fetch import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
