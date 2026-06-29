#!/usr/bin/env python3
"""Generate macOS (.icns) and Windows (.ico) app icons from a shared design."""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO_ROOT = Path(__file__).resolve().parents[1]
ICONS_DIR = REPO_ROOT / "packaging" / "icons"
SYMBOL = "\u25D0"  # ◐ — progress / partial fill
BACKGROUND = (0, 0, 0, 255)
FOREGROUND = (255, 255, 255, 255)

FONT_CANDIDATES = [
    "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    "/System/Library/Fonts/Apple Symbols.ttf",
    "/Library/Fonts/Arial Unicode.ttf",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
]

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


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in FONT_CANDIDATES:
        if Path(path).exists():
            try:
                return ImageFont.truetype(path, size=size)
            except OSError:
                continue
    return ImageFont.load_default()


def render_icon(size: int) -> Image.Image:
    image = Image.new("RGBA", (size, size), BACKGROUND)
    draw = ImageDraw.Draw(image)
    font_size = int(size * 0.72)
    font = load_font(font_size)

    bbox = draw.textbbox((0, 0), SYMBOL, font=font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    x = (size - text_width) / 2 - bbox[0]
    y = (size - text_height) / 2 - bbox[1]
    draw.text((x, y), SYMBOL, font=font, fill=FOREGROUND)
    return image


def write_png(path: Path, size: int) -> None:
    render_icon(size).save(path, format="PNG")


def write_ico(path: Path) -> None:
    images = [render_icon(size) for size in WINDOWS_ICON_SIZES]
    images[-1].save(
        path,
        format="ICO",
        sizes=[(image.width, image.height) for image in images],
        append_images=images[:-1],
    )


def write_icns(path: Path) -> None:
    iconutil = shutil.which("iconutil")
    if iconutil is None:
        raise RuntimeError("iconutil is required to build AppIcon.icns (macOS only).")

    iconset = REPO_ROOT / ".iconset-build" / "AppIcon.iconset"
    if iconset.exists():
        shutil.rmtree(iconset)
    iconset.mkdir(parents=True)

    for size, filename in MACOS_ICONSET_SIZES:
        write_png(iconset / filename, size)

    subprocess.run(
        [iconutil, "-c", "icns", str(iconset), "-o", str(path)],
        check=True,
    )


def main() -> int:
    ICONS_DIR.mkdir(parents=True, exist_ok=True)

    source_png = ICONS_DIR / "app-icon.png"
    write_png(source_png, 1024)

    ico_path = ICONS_DIR / "app-icon.ico"
    write_ico(ico_path)

    icns_path = ICONS_DIR / "AppIcon.icns"
    if sys.platform == "darwin":
        write_icns(icns_path)
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
