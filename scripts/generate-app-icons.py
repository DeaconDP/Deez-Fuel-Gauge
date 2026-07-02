#!/usr/bin/env python3
"""Generate macOS (.icns) and Windows (.ico) app icons from a shared source image."""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[1]
ICONS_DIR = REPO_ROOT / "packaging" / "icons"
SOURCE_PNG = ICONS_DIR / "app-icon-source.png"

MACOS_ICONSET_SIZES = [
    (16, "icon_16x16.png"),
    (32, "icon_16x16@2x.png"),
    (32, "icon_32x32.png"),
    (64, "icon_32x32@2x.png"),
    (128, "icon_128x128.png"),
    (256, "icon_128x128@2x.png"),
    (256, "icon_256x256.png"),
    (512, "icon_256x256@2x.png"),
    (512, "icon_512x512.png"),
    (1024, "icon_512x512@2x.png"),
]

WINDOWS_ICON_SIZES = [16, 24, 32, 48, 64, 128, 256]


def load_source() -> Image.Image:
    if not SOURCE_PNG.exists():
        raise FileNotFoundError(
            f"Missing source icon at {SOURCE_PNG}. "
            "Add app-icon-source.png (1024x1024 recommended) before running this script."
        )

    source = Image.open(SOURCE_PNG).convert("RGBA")
    if source.size != (1024, 1024):
        source = source.resize((1024, 1024), Image.Resampling.LANCZOS)
    return source


def render_icon(source: Image.Image, size: int) -> Image.Image:
    if source.size == (size, size):
        return source.copy()
    return source.resize((size, size), Image.Resampling.LANCZOS)


def write_png(path: Path, source: Image.Image, size: int) -> None:
    render_icon(source, size).save(path, format="PNG")


def write_ico(path: Path, source: Image.Image) -> None:
    images = [render_icon(source, size) for size in WINDOWS_ICON_SIZES]
    images[-1].save(
        path,
        format="ICO",
        sizes=[(image.width, image.height) for image in images],
        append_images=images[:-1],
    )


def write_icns(path: Path, source: Image.Image) -> None:
    iconutil = shutil.which("iconutil")
    if iconutil is None:
        raise RuntimeError("iconutil is required to build AppIcon.icns (macOS only).")

    iconset = REPO_ROOT / ".iconset-build" / "AppIcon.iconset"
    if iconset.exists():
        shutil.rmtree(iconset)
    iconset.mkdir(parents=True)

    for size, filename in MACOS_ICONSET_SIZES:
        write_png(iconset / filename, source, size)

    subprocess.run(
        [iconutil, "-c", "icns", str(iconset), "-o", str(path)],
        check=True,
    )


def main() -> int:
    ICONS_DIR.mkdir(parents=True, exist_ok=True)

    source = load_source()

    source_png = ICONS_DIR / "app-icon.png"
    write_png(source_png, source, 1024)

    ico_path = ICONS_DIR / "app-icon.ico"
    write_ico(ico_path, source)

    icns_path = ICONS_DIR / "AppIcon.icns"
    if sys.platform == "darwin":
        write_icns(icns_path, source)
    elif not icns_path.exists():
        print(
            "Skipping AppIcon.icns (iconutil unavailable). "
            "Run this script on macOS to regenerate the .icns file.",
            file=sys.stderr,
        )

    print(f"Wrote {source_png.relative_to(REPO_ROOT)}")
    print(f"Wrote {ico_path.relative_to(REPO_ROOT)}")
    if icns_path.exists():
        print(f"Wrote {icns_path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
