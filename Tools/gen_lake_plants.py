"""Pass 16: procedural card textures for the PNW lake kit (our own; no photo pixels).
fern_frond.png   sword-fern frond (RGBA, alpha cut-out), frond tip at v = 1
reed_blades.png  cattail / reed-canary-grass blades, a few seed heads (RGBA)
tallgrass.png    unmowed meadow grass clump with seed heads (RGBA)
grime_streaks.png  building weathering overlay: splash-back dirt at the base, faint streaks below the eaves (RGBA, alpha = grime)
Outputs to Assets/SkiView/Environment/Textures/."""
import numpy as np, os
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SkiView', 'Environment', 'Textures')
rng = np.random.default_rng(16)

def save(img, name): img.save(os.path.join(OUT, name)); print('wrote', name, img.size)

# ---- sword fern frond: rachis up the middle, ~34 pairs of narrow pinnae tapering to the tip
W, H = 256, 512
im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for i in range(34):
    t = i / 34; y = H - 8 - t * (H - 20)
    L = (W * 0.34) * (0.35 + 0.65 * np.sin(np.pi * min(1, t * 1.15 + 0.08))) * (1 - t * 0.55)
    for s in (-1, 1):
        x0 = W / 2; ang = np.radians(62 - 18 * t)
        x1 = x0 + s * L * np.sin(ang); y1 = y - L * np.cos(ang) * 0.55
        g = int(rng.integers(70, 105)); col = (int(g * 0.45), g, int(g * 0.32), 255)
        wdt = max(3, int(11 * (1 - t * 0.6)))
        d.polygon([(x0, y + wdt * 0.3), (x1, y1), (x0, y - wdt)], fill=col)
d.line([(W / 2, H - 2), (W / 2, 10)], fill=(55, 70, 30, 255), width=4)
im = im.filter(ImageFilter.GaussianBlur(0.6)); save(im, 'fern_frond.png')

# ---- reeds: ~40 tapering blades, some bent, 4 seed heads
W, H = 512, 512
im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for i in range(46):
    x = rng.uniform(20, W - 20); h = rng.uniform(0.55, 0.98) * H; bend = rng.normal(0, 40); w0 = rng.uniform(5, 9)
    g = rng.uniform(0.75, 1.1); col = (int(95 * g), int(120 * g), int(55 * g), 255) if rng.random() > 0.25 else (int(150 * g), int(140 * g), int(85 * g), 255)
    pts = []
    for k in range(9):
        t = k / 8; pts.append((x + bend * t * t, H - t * h, w0 * (1 - t * 0.92)))
    for k in range(8):
        (xa, ya, wa), (xb, yb, wb) = pts[k], pts[k + 1]
        d.polygon([(xa - wa / 2, ya), (xb - wb / 2, yb), (xb + wb / 2, yb), (xa + wa / 2, ya)], fill=col)
for i in range(4):
    x = rng.uniform(80, W - 80); h = rng.uniform(0.75, 0.95) * H
    d.line([(x, H), (x, H - h)], fill=(90, 105, 50, 255), width=3)
    d.rounded_rectangle([x - 6, H - h + 6, x + 6, H - h + 70], 6, fill=(92, 62, 38, 255))
save(im.filter(ImageFilter.GaussianBlur(0.5)), 'reed_blades.png')

# ---- tall meadow grass: fine blades, straw-tinted tips and seed heads (summer, unmowed)
im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for i in range(140):
    x = rng.uniform(10, W - 10); h = rng.uniform(0.35, 0.95) * H; bend = rng.normal(0, 55); w0 = rng.uniform(2, 4.5)
    dry = rng.random() < 0.45; g = rng.uniform(0.8, 1.15)
    col = (int(170 * g), int(160 * g), int(100 * g), 255) if dry else (int(90 * g), int(125 * g), int(55 * g), 255)
    prev = (x, H)
    for k in range(1, 9):
        t = k / 8; p = (x + bend * t * t, H - t * h)
        d.line([prev, p], fill=col, width=max(1, int(w0 * (1 - t * 0.8)))); prev = p
    if rng.random() < 0.3:
        for j in range(6): ox = rng.normal(0, 3); d.ellipse([prev[0] - 2 + ox, prev[1] + j * 5 - 2, prev[0] + 2 + ox, prev[1] + j * 5 + 4], fill=(185, 170, 115, 255))
save(im.filter(ImageFilter.GaussianBlur(0.4)), 'tallgrass.png')

# ---- grime overlay (512 x 512, tiles along u): dark splash-back band at the bottom 12 %, faint vertical streaks from the top
W, H = 512, 512
v = np.linspace(1, 0, H)[:, None]                     # 1 at the top row
n = np.asarray(Image.fromarray((rng.random((16, 64)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), float) / 255
streak = np.asarray(Image.fromarray((rng.random((4, 48)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), float) / 255
base = np.clip(1 - v / 0.14, 0, 1) ** 1.6
top = np.clip((v - 0.55) / 0.45, 0, 1) * np.clip(streak - 0.45, 0, 1) * 2.2
alpha = np.clip(base * (0.55 + 0.45 * n) + top * 0.45, 0, 1) * 0.85
rgb = np.stack([np.full((H, W), 58), np.full((H, W), 54), np.full((H, W), 44)], -1)
img = np.dstack([rgb, alpha * 255]).astype(np.uint8)
save(Image.fromarray(img, 'RGBA'), 'grime_streaks.png')
