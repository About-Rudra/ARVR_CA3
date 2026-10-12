"""Writes videos/MARK-00x.mjpeg: each marker on a grey background, slight jitter,
640x480, used as Chrome's fake camera by marker_tracking.js. Needs Pillow."""
import io
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "..", "frontend", "ar", "targets", "src")
OUT = os.path.join(HERE, "videos")
os.makedirs(OUT, exist_ok=True)
for n in range(1, 6):
    marker = "MARK-%03d" % n
    img = Image.open(os.path.join(SRC, marker + ".png")).convert("RGB").resize((400, 400))
    with open(os.path.join(OUT, marker + ".mjpeg"), "wb") as f:
        for i in range(60):
            frame = Image.new("RGB", (640, 480), (200, 200, 195))
            frame.paste(img, (120 + (i % 3), 40))
            buf = io.BytesIO()
            frame.save(buf, "JPEG", quality=90)
            f.write(buf.getvalue())
    print("wrote", os.path.join("videos", marker + ".mjpeg"))
