#!/usr/bin/env python3
"""Pass 17: Gulf-lake (Saltwhite) kit textures, all procedural except the CC0 roof tiles (ambientCG RoofingTiles013A, staged):
white quartz sand + normal, stucco + normal, palm frond (pinnate), fan leaf (sabal / saw palmetto), slash-pine needle tuft,
pine and palm impostors, sea-oat grass. Writes Assets/SkiView/Environment/Textures/*. Nothing traced from references."""
import os, numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets/SkiView/Environment/Textures/')
STAGE = '/workspace/skiview-assets-staging/ambientcg/RoofingTiles013A/'
rng = np.random.default_rng(1717)

def tile_noise(n, scale, seed, octaves=4):
    r = np.random.default_rng(seed); acc = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        k = max(2, int(n / scale * 2 ** o))
        g = r.standard_normal((k, k)); g = np.tile(g, (3, 3))
        z = ndimage.zoom(g, n * 3 / g.shape[0], order=3)[n:2 * n, n:2 * n]
        acc += z * amp; tot += amp; amp *= 0.5
    return acc / tot

def normal_from_height(h, strength):
    gy, gx = np.gradient(h); nx, ny, nz = -gx * strength, gy * strength, np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return Image.fromarray((np.stack([nx / l, ny / l, nz / l], -1) * 127.5 + 127.5).astype(np.uint8), 'RGB')

# --- white quartz sand (aerial samples #dcd5b9 / #e0d9bd) ---
N = 1024
grain = np.random.default_rng(3).standard_normal((N, N)); grain = ndimage.gaussian_filter(grain, 0.7, mode='wrap')
mid = tile_noise(N, 64, 4); big = tile_noise(N, 8, 5, 3)
ripple = np.sin((np.arange(N)[None, :] / N * 2 * np.pi * 18) + 2.5 * tile_noise(N, 16, 6, 2)) * 0.5
lum = 0.86 + 0.035 * grain / grain.std() + 0.025 * mid / mid.std() + 0.02 * big / big.std() + 0.012 * ripple
base = np.array([0.878, 0.851, 0.741])
col = np.clip(base[None, None, :] * lum[..., None] + 0.015 * np.random.default_rng(7).standard_normal((N, N, 1)), 0, 1)
Image.fromarray((col * 255).astype(np.uint8)).save(OUT + 'sand_white_color.jpg', quality=90)
normal_from_height(grain * 0.35 + ripple * 1.5 + mid * 0.8, 1.2).save(OUT + 'sand_white_normal.png')

# --- stucco (smooth trowel, ivory) ---
N = 512
st = tile_noise(N, 24, 11) * 0.6 + tile_noise(N, 96, 12) * 0.4 + 0.25 * ndimage.gaussian_filter(np.random.default_rng(13).standard_normal((N, N)), 0.8, mode='wrap')
lum = 0.93 + 0.03 * st / st.std()
Image.fromarray((np.clip(np.array([0.96, 0.94, 0.90])[None, None] * lum[..., None], 0, 1) * 255).astype(np.uint8)).save(OUT + 'stucco_color.jpg', quality=90)
normal_from_height(st, 2.0).save(OUT + 'stucco_normal.png')

def rgba(size): return Image.new('RGBA', size, (0, 0, 0, 0))

# --- pinnate palm frond: rachis along u (left -> right), leaflets either side, drooping tips ---
W, H = 1024, 256
im = rgba((W, H)); d = ImageDraw.Draw(im)
for i in range(110):
    t = i / 110; x = 20 + t * (W - 40)
    L = (1 - (2 * t - 0.95) ** 2) * 0.46 * H * (0.85 + 0.3 * rng.random()) + 6
    for s in (-1, 1):
        ang = np.radians(55 + 20 * t) ; ex = x + np.cos(ang) * L * 0.55; ey = H / 2 + s * np.sin(ang) * L
        g = int(105 + 40 * rng.random() - 30 * t); col = (int(g * 0.62), g, int(g * 0.38), 255)
        d.line([(x, H / 2), (ex, ey)], fill=col, width=max(2, int(5 * (1 - t) + 2)))
d.line([(10, H / 2), (W - 10, H / 2)], fill=(120, 116, 70, 255), width=5)
im.filter(ImageFilter.SMOOTH).save(OUT + 'palm_frond.png')

# --- fan leaf (sabal / saw palmetto): radial segments from the petiole at the bottom centre ---
S = 512; im = rgba((S, S)); d = ImageDraw.Draw(im)
cx, cy = S / 2, S - 20
for i in range(46):
    a = np.radians(200 + i * 140 / 45) ; L = S * (0.62 + 0.18 * rng.random()) * (1 - 0.25 * abs(i - 22.5) / 22.5)
    g = int(95 + 45 * rng.random()); col = (int(g * 0.7), g, int(g * 0.5), 255)
    pts = [(cx, cy)]
    for k in range(1, 9):
        r = L * k / 8; sag = (k / 8) ** 2 * 18
        pts.append((cx + np.cos(a) * r, cy + np.sin(a) * r + sag))
    d.line(pts, fill=col, width=9)
d.line([(cx, cy), (cx, S - 1)], fill=(110, 105, 70, 255), width=8)
im.filter(ImageFilter.SMOOTH).save(OUT + 'fan_leaf.png')

# --- slash pine needle tuft: long needles radiating from short twigs, clustered ---
S = 512; im = rgba((S, S)); d = ImageDraw.Draw(im)
for c in range(9):
    cx_, cy_ = S * (0.25 + 0.5 * rng.random()), S * (0.25 + 0.5 * rng.random())
    for k in range(70):
        a = rng.uniform(0, 2 * np.pi); L = rng.uniform(0.12, 0.26) * S
        g = int(70 + 45 * rng.random()); col = (int(g * 0.72), g, int(g * 0.42), 255)
        d.line([(cx_, cy_), (cx_ + np.cos(a) * L, cy_ + np.sin(a) * L * 0.8)], fill=col, width=2)
im.filter(ImageFilter.SMOOTH).save(OUT + 'pine_tuft.png')

# --- slash pine impostor: tall bare trunk, irregular flat-topped crown in the top third ---
W, H = 256, 512; im = rgba((W, H)); d = ImageDraw.Draw(im)
d.rectangle([W / 2 - 5, H * 0.25, W / 2 + 5, H - 1], fill=(92, 72, 56, 255))
for k in range(60):
    x = W / 2 + rng.normal(0, W * 0.2); y = H * (0.06 + 0.24 * rng.random()); r = rng.uniform(14, 34)
    g = int(60 + 35 * rng.random()); d.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=(int(g * 0.7), g, int(g * 0.45), 255))
im.filter(ImageFilter.SMOOTH).save(OUT + 'pine_impostor.png')

# --- palm impostor: grey trunk + fronds at the top ---
im = rgba((W, H)); d = ImageDraw.Draw(im)
d.rectangle([W / 2 - 7, H * 0.18, W / 2 + 7, H - 1], fill=(150, 145, 135, 255))
for k in range(16):
    a = np.radians(-170 + k * 160 / 15) + rng.normal(0, 0.08); L = rng.uniform(80, 115)
    pts = [(W / 2, H * 0.17)]
    for j in range(1, 7):
        r = L * j / 6; pts.append((W / 2 + np.cos(a) * r, H * 0.17 + np.sin(a) * r * 0.55 + (j / 6) ** 2 * 40))
    g = int(90 + 40 * rng.random()); d.line(pts, fill=(int(g * 0.65), g, int(g * 0.4), 255), width=10)
im.filter(ImageFilter.SMOOTH).save(OUT + 'palm_impostor.png')

# --- sea oats / beach grass blades ---
W, H = 256, 256; im = rgba((W, H)); d = ImageDraw.Draw(im)
for k in range(60):
    x = rng.uniform(10, W - 10); h = rng.uniform(0.5, 0.95) * H; lean = rng.normal(0, 18)
    g = int(130 + 50 * rng.random()); d.line([(x, H - 1), (x + lean * 0.5, H - h * 0.6), (x + lean, H - h)], fill=(int(g * 0.85), g, int(g * 0.55), 255), width=3)
    if rng.random() < 0.3: d.ellipse([x + lean - 4, H - h - 10, x + lean + 4, H - h + 6], fill=(200, 180, 120, 255))
im.save(OUT + 'seaoats.png')

# --- CC0 roof tiles (ambientCG RoofingTiles013A), 1K copies ---
if os.path.exists(STAGE + 'RoofingTiles013A_2K-JPG_Color.jpg'):
    Image.open(STAGE + 'RoofingTiles013A_2K-JPG_Color.jpg').convert('RGB').resize((1024, 1024), Image.LANCZOS).save(OUT + 'rooftile_color.jpg', quality=88)
    Image.open(STAGE + 'RoofingTiles013A_2K-JPG_NormalGL.jpg').convert('RGB').resize((1024, 1024), Image.LANCZOS).save(OUT + 'rooftile_normal.png')
print('wrote gulf textures to', os.path.abspath(OUT))
