"""Load pipeline configuration (config/regions.yaml, config/sources.yaml)."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

import yaml

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
CONFIG_DIR = PIPELINE_ROOT / "config"
DATA_DIR = PIPELINE_ROOT / "data"
RAW_DIR = DATA_DIR / "raw"
BUILD_DIR = PIPELINE_ROOT / "build"
REPO_ROOT = PIPELINE_ROOT.parent

# Bump when the generated output changes for the same inputs (format or
# algorithm change). Recorded in every tile and manifest.
PIPELINE_DATA_VERSION = 2  # 2: W2 F1/F2 (enums v2, stable seed, parts, RATR/JNCT/BFNT/PROP)


@dataclass(frozen=True)
class Region:
    id: str
    name_en: str
    name_ne: str
    bbox: tuple[float, float, float, float]  # lon_min, lat_min, lon_max, lat_max
    horizon_bbox: tuple[float, float, float, float]
    detail_levels: tuple[int, ...]
    horizon_levels: tuple[int, ...]
    height_grid: int
    biome_grid: int
    bbox_buffer_m: float
    milestone: str = ""
    base_install: bool = False
    extra: dict = field(default_factory=dict, compare=False, hash=False)

    @property
    def all_levels(self) -> tuple[int, ...]:
        return tuple(sorted(set(self.detail_levels) | set(self.horizon_levels)))

    @property
    def leaf_level(self) -> int:
        return max(self.detail_levels)


def load_regions(path: Path | None = None) -> dict[str, Region]:
    path = path or CONFIG_DIR / "regions.yaml"
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    defaults = data.get("defaults", {})
    out: dict[str, Region] = {}
    for rid, r in data["regions"].items():
        merged = {**defaults, **r}
        known = {"name_en", "name_ne", "bbox", "horizon_bbox", "detail_levels", "horizon_levels",
                 "height_grid", "biome_grid", "bbox_buffer_m", "milestone", "base_install"}
        out[rid] = Region(
            id=rid,
            name_en=merged["name_en"],
            name_ne=merged.get("name_ne", ""),
            bbox=tuple(merged["bbox"]),
            horizon_bbox=tuple(merged.get("horizon_bbox", merged["bbox"])),
            detail_levels=tuple(merged["detail_levels"]),
            horizon_levels=tuple(merged.get("horizon_levels", ())),
            height_grid=int(merged["height_grid"]),
            biome_grid=int(merged["biome_grid"]),
            bbox_buffer_m=float(merged["bbox_buffer_m"]),
            milestone=merged.get("milestone", ""),
            base_install=bool(merged.get("base_install", False)),
            extra={k: v for k, v in merged.items() if k not in known},
        )
    return out


def load_sources(path: Path | None = None) -> dict:
    path = path or CONFIG_DIR / "sources.yaml"
    return yaml.safe_load(path.read_text(encoding="utf-8"))
