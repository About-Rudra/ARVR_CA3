"""Generate 3 printable high-contrast image-target markers (MARK-001..003).

Design rules for reliable AR image tracking:
  * 1024x1024 px, asymmetric, high-contrast black-on-white
  * thick black border (quiet zone + edge features)
  * distinct geometric motif per marker + large human-readable ID
  * no repetitive texture / no rotational symmetry

Print at 15cm x 15cm (see README.md). The physical print width must match
ARBootstrap.markerPrintWidthMeters (0.15m) or pose scale will be off.

Usage:  python3 Tools/generate_markers.py   (or run from Assets/Markers/)
Outputs PNGs next to this script AND into Assets/StreamingAssets/markers/.
"""
import math
import os
import shutil
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    sys.exit("Pillow is required: pip install pillow")

SIZE = 1024
BORDER = 90
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
STREAMING = os.path.join(REPO, "Assets", "StreamingAssets", "markers")


def load_font(px):
    for path in (
        "arial.ttf",
        "C:/Windows/Fonts/arial.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ):
        try:
            return ImageFont.truetype(path, px)
        except Exception:
            continue
    return ImageFont.load_default()


def base_image():
    img = Image.new("RGB", (SIZE, SIZE), "white")
    d = ImageDraw.Draw(img)
    # outer border
    d.rectangle([0, 0, SIZE - 1, SIZE - 1], fill="black")
    d.rectangle([BORDER, BORDER, SIZE - BORDER - 1, SIZE - BORDER - 1], fill="white")
    return img, d


def add_id_banner(d, marker_id):
    font = load_font(120)
    band_h = 190
    y0 = SIZE - BORDER - band_h
    d.rectangle([BORDER, y0, SIZE - BORDER, SIZE - BORDER], fill="black")
    text = marker_id
    bbox = d.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    d.text(((SIZE - tw) / 2, y0 + 25), text, font=font, fill="white")


def marker_001():
    """Concentric squares + diagonal wedge (top-left heavy)."""
    img, d = base_image()
    cx = cy = (SIZE - 190) / 2 + 40  # optical centre of the art zone
    for i, w in enumerate((300, 220, 140)):
        inset = w
        color = "black" if i % 2 == 0 else "white"
        d.rectangle([cx - inset, cy - inset - 60, cx + inset, cy + inset - 60],
                    fill=color, outline="black", width=10)
    d.rectangle([cx - 45, cy - 105, cx + 45, cy - 15], fill="black")
    # asymmetric wedge, top-left
    d.polygon([(BORDER, BORDER), (BORDER + 260, BORDER), (BORDER, BORDER + 260)], fill="black")
    d.ellipse([BORDER + 60, BORDER + 60, BORDER + 150, BORDER + 150], fill="white")
    add_id_banner(d, "MARK-001")
    return img


def marker_002():
    """Big triangle + circles row (right-heavy)."""
    img, d = base_image()
    top = SIZE / 2 - 260
    d.polygon([(SIZE / 2, top), (SIZE / 2 + 300, top + 420), (SIZE / 2 - 300, top + 420)],
              fill="black")
    d.polygon([(SIZE / 2, top + 130), (SIZE / 2 + 170, top + 360), (SIZE / 2 - 170, top + 360)],
              fill="white")
    d.ellipse([SIZE / 2 - 60, top + 210, SIZE / 2 + 60, top + 330], fill="black")
    # dot row, bottom-right asymmetric
    for i in range(4):
        x = SIZE - BORDER - 120 - i * 90
        y = SIZE - BORDER - 260
        r = 34 if i != 3 else 52
        d.ellipse([x - r, y - r, x + r, y + r], fill="black")
    add_id_banner(d, "MARK-002")
    return img


def marker_003():
    """Cross/plus + corner blocks (corner-heavy, anti-symmetric)."""
    img, d = base_image()
    cx, cy = SIZE / 2, SIZE / 2 - 60
    arm, thick = 280, 110
    d.rectangle([cx - thick, cy - arm, cx + thick, cy + arm], fill="black")
    d.rectangle([cx - arm, cy - thick, cx + arm, cy + thick], fill="black")
    d.ellipse([cx - 70, cy - 70, cx + 70, cy + 70], fill="white")
    d.ellipse([cx - 30, cy - 30, cx + 30, cy + 30], fill="black")
    # three corner blocks, one corner deliberately empty (breaks symmetry)
    c = 120
    d.rectangle([BORDER, BORDER, BORDER + c, BORDER + c], fill="black")
    d.rectangle([SIZE - BORDER - c, BORDER, SIZE - BORDER, BORDER + c], fill="black")
    d.rectangle([BORDER, SIZE - BORDER - 190 - c, BORDER + c, SIZE - BORDER - 190], fill="black")
    add_id_banner(d, "MARK-003")
    return img


def main():
    makers = {"MARK-001": marker_001, "MARK-002": marker_002, "MARK-003": marker_003}
    os.makedirs(STREAMING, exist_ok=True)
    for marker_id, fn in makers.items():
        img = fn()
        local = os.path.join(HERE, marker_id + ".png")
        img.save(local, "PNG")
        shutil.copyfile(local, os.path.join(STREAMING, marker_id + ".png"))
        print("wrote", local, "->", os.path.join(STREAMING, marker_id + ".png"))
    print("Done. Print each at 15cm x 15cm, matte, flat lighting.")


if __name__ == "__main__":
    main()
