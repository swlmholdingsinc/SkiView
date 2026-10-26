"""Rider textures for the slalom rebuild (Docs/SLALOM-SKIER-LOOK.md).
- Generated/ski_topsheet.png: carbon-weave top sheet with a teal/orange cut-line graphic (no brand marks). u = tail->tip, v = across.
- ThirdParty/Mixamo/Derived/: board shorts and rash guard, recoloured from Remy's own textures (derived from Mixamo, so git-ignored)."""
import os, numpy as np
from PIL import Image, ImageDraw, ImageFilter
R = os.path.join(os.path.dirname(__file__), "..")
GEN = os.path.join(R, "Assets/SkiView/Generated"); MX = os.path.join(R, "Assets/SkiView/ThirdParty/Mixamo")
os.makedirs(os.path.join(MX, "Derived"), exist_ok=True)

# ---- ski top sheet 2048 x 256 (along x across)
W, H = 2048, 256
y, x = np.mgrid[0:H, 0:W].astype(np.float32)
weave = ((np.floor(x / 6) + np.floor(y / 6)) % 2) * 0.5 + 0.5          # 2x2 twill-ish checker
twill = 0.5 + 0.5 * np.sin((x + y) * 0.5)
base = 0.80 + 0.12 * weave + 0.08 * twill
# pass 3: saturated red-orange top sheet (reads against dark water, like the reference skis), dark graphic, no marks
img = np.stack([205 * base, 52 * base, 22 * base], -1)
im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(im)
d.polygon([(0, 150), (0, 256), (1150, 256), (1500, 128), (1150, 0), (900, 0)], fill=(24, 26, 32))
d.polygon([(0, 178), (0, 256), (1070, 256), (1370, 128), (1070, 0), (985, 0)], fill=(196, 40, 24))
d.line([(0, 164), (952, 6)], fill=(250, 200, 60), width=6)
d.line([(0, 244), (1162, 250), (1502, 128), (1162, 6)], fill=(250, 200, 60), width=5)
d.polygon([(1720, 0), (2048, 0), (2048, 256), (1720, 256), (1790, 128)], fill=(24, 26, 32))
d.line([(1720, 0), (1790, 128), (1720, 256)], fill=(250, 200, 60), width=10)
im = im.filter(ImageFilter.GaussianBlur(0.6))
im.save(os.path.join(GEN, "ski_topsheet.png"))

# ---- spray puff 64x64 (soft white blob with a little break-up) for the ski spray particles
yy, xx = np.mgrid[0:64, 0:64].astype(np.float32); rr = np.hypot(xx - 31.5, yy - 31.5) / 31.5
rng = np.random.default_rng(7); n = Image.fromarray((rng.random((16, 16)) * 255).astype(np.uint8)).resize((64, 64), Image.BICUBIC)
a = np.clip(1 - rr, 0, 1) ** 1.6 * (0.65 + 0.35 * np.asarray(n, np.float32) / 255)
Image.fromarray(np.dstack([np.full((64, 64), 255, np.uint8)] * 3 + [(a * 255).astype(np.uint8)]), "RGBA").save(os.path.join(GEN, "spray_puff.png"))

# ---- clothing reskins from the extracted Remy textures
T = os.path.join(MX, "Textures")
def recolour(src, dst, rgb, contrast, stripes=None):
    p = os.path.join(T, src)
    if not os.path.exists(p): print("missing", p); return
    g = np.asarray(Image.open(p).convert("L"), np.float32) / 255.0
    g = (g - g.mean()) * contrast + 0.5
    out = np.clip(np.stack([g * c * 2 for c in rgb], -1), 0, 255)
    if stripes is not None:
        hh, ww = g.shape; yy, xx = np.mgrid[0:hh, 0:ww]
        band = ((xx + yy * 0.35) % 170) < 22                                  # diagonal panel stripes
        pin = (((xx + yy * 0.35) % 170) >= 26) & (((xx + yy * 0.35) % 170) < 31)
        for m, col in ((band, stripes[0]), (pin, stripes[1])):
            out[m] = np.clip(np.stack([g[m] * c * 2 for c in col], -1), 0, 255)
    Image.fromarray(out.astype(np.uint8)).save(os.path.join(MX, "Derived", dst))
recolour("Remy_Bottom_Diffuse.png", "boardshorts.png", (18, 40, 92), 0.55, stripes=((20, 140, 150), (235, 235, 235)))
recolour("Remy_Top_Diffuse.png", "rashguard.png", (26, 28, 32), 0.5)
print("ok")
