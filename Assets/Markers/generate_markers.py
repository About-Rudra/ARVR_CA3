"""Generate 5 printable high-contrast image-target markers (MARK-001..005).

Design rules for reliable AR image tracking:
  * 1024x1024 px, asymmetric, high-contrast black-on-white
  * thick black border (quiet zone + edge features)
  * distinct geometric motif per marker + large human-readable ID
  * no repetitive texture / no rotational symmetry

Print at 15cm x 15cm (see README.md). The physical print width must match
ARBootstrap.markerPrintWidthMeters (0.15m) or pose scale will be off.

Usage:  python3 Tools/generate_markers.py   (or run from Assets/Markers/)
Outputs PNGs next to this script, into Assets/StreamingAssets/markers/ (Unity)
and into frontend/ar/targets/src/ (WebAR / MindAR compiler input).
"""
import math
import random
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
WEBAR_SRC = os.path.join(REPO, "frontend", "ar", "targets", "src")


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


def speckle(d, box, seed, count, color, smin=14, smax=34):
    """Deterministic small rectangles: adds corner features inside flat regions
    (MindAR/ARCore need texture; big flat shapes alone tracked poorly)."""
    rnd = random.Random(seed)
    x0, y0, x1, y1 = box
    for _ in range(count):
        w, h = rnd.randint(smin, smax), rnd.randint(smin, smax)
        x, y = rnd.randint(int(x0), int(x1 - w)), rnd.randint(int(y0), int(y1 - h))
        d.rectangle([x, y, x + w, y + h], fill=color)


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
    speckle(d, (BORDER + 10, BORDER + 10, SIZE - BORDER - 10, SIZE - BORDER - 200), 1001, 160, "white")
    speckle(d, (BORDER + 10, BORDER + 10, SIZE - BORDER - 10, SIZE - BORDER - 200), 1002, 60, "black", 10, 24)
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
    speckle(d, (BORDER + 10, BORDER + 10, SIZE - BORDER - 10, SIZE - BORDER - 200), 3001, 160, "white")
    speckle(d, (BORDER + 10, BORDER + 10, SIZE - BORDER - 10, SIZE - BORDER - 200), 3002, 60, "black", 10, 24)
    add_id_banner(d, "MARK-003")
    return img


def marker_004():
    """Diagonal stripes (left half) + offset ring + L-bracket (bottom-right)."""
    img, d = base_image()
    art_bottom = SIZE - BORDER - 190
    # diagonal stripes clipped to the left half of the art zone
    stripes = Image.new("L", (SIZE, SIZE), 0)
    sd = ImageDraw.Draw(stripes)
    for k in range(-SIZE, SIZE, 70):
        sd.polygon([(k, art_bottom), (k + 35, art_bottom), (k + 35 + 600, art_bottom - 600), (k + 600, art_bottom - 600)], fill=255)
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rectangle([BORDER + 30, BORDER + 30, SIZE / 2 - 20, art_bottom - 30], fill=255)
    stripes = Image.composite(stripes, Image.new("L", (SIZE, SIZE), 0), mask)
    img.paste("black", (0, 0), stripes)
    # offset ring, upper right
    cx, cy, r = SIZE / 2 + 190, BORDER + 210, 150
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill="black")
    d.ellipse([cx - r + 55, cy - r + 55, cx + r - 55, cy + r - 55], fill="white")
    d.rectangle([cx - 20, cy - r, cx + 20, cy - r + 55], fill="white")  # notch breaks rotation symmetry
    # L-bracket, lower right
    x0, y1 = SIZE / 2 + 40, art_bottom - 40
    d.rectangle([x0, y1 - 70, SIZE - BORDER - 40, y1], fill="black")
    d.rectangle([SIZE - BORDER - 110, y1 - 330, SIZE - BORDER - 40, y1], fill="black")
    d.rectangle([x0 + 40, y1 - 230, x0 + 150, y1 - 120], fill="black")
    add_id_banner(d, "MARK-004")
    return img


def marker_005():
    """Irregular block grid (top) + half-disc + bar code (bottom-left heavy)."""
    img, d = base_image()
    # irregular block grid: fixed pattern, no repeats/symmetry
    pattern = ["1101001", "0110110", "1001011", "0111000"]
    cell, gx, gy = 88, BORDER + 50, BORDER + 40
    for row, bits in enumerate(pattern):
        for col, b in enumerate(bits):
            if b == "1":
                d.rectangle([gx + col * cell, gy + row * cell, gx + (col + 1) * cell - 14, gy + (row + 1) * cell - 14], fill="black")
    art_bottom = SIZE - BORDER - 190
    # half-disc, bottom-left
    d.pieslice([BORDER + 40, art_bottom - 300, BORDER + 380, art_bottom + 40], 180, 360, fill="black")
    d.ellipse([BORDER + 165, art_bottom - 175, BORDER + 255, art_bottom - 85], fill="white")
    # uneven bar code, bottom-right
    x = SIZE / 2 + 30
    for w in (18, 40, 14, 60, 22, 30, 12, 48):
        d.rectangle([x, art_bottom - 230, x + w, art_bottom - 30], fill="black")
        x += w + 18
    add_id_banner(d, "MARK-005")
    return img


def main():
    makers = {"MARK-001": marker_001, "MARK-002": marker_002, "MARK-003": marker_003,
              "MARK-004": marker_004, "MARK-005": marker_005}
    for out_dir in (STREAMING, WEBAR_SRC):
        os.makedirs(out_dir, exist_ok=True)
    for marker_id, fn in makers.items():
        img = fn()
        local = os.path.join(HERE, marker_id + ".png")
        img.save(local, "PNG")
        for out_dir in (STREAMING, WEBAR_SRC):
            shutil.copyfile(local, os.path.join(out_dir, marker_id + ".png"))
        print("wrote", local, "-> StreamingAssets/markers + frontend/ar/targets/src")
    print("Done. Print each at 15cm x 15cm, matte, flat lighting.")


if __name__ == "__main__":
    main()
