#!/usr/bin/env python3
"""Pass 6 tree-line variety textures (all procedural/original): red alder + bigleaf maple leaf-cluster cards and impostors
(maple with a slight early-fall tint), a distant-hill forest canopy tile and a ragged fir-skyline fringe strip."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage
T = 'Assets/SkiView/Environment/Textures/'
rng = np.random.default_rng(66)

def jitter(c, v=0.15, k=0):
    return tuple(int(np.clip(x * (1 + rng.uniform(-v, v)) + k, 0, 255)) for x in c) + (255,)

def cluster(w, h, base, fall, kind):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx, cy = w / 2, h / 2
    # twigs from the centre-bottom outwards
    for t in range(9):
        a = rng.uniform(-2.6, -0.5); L = rng.uniform(0.25, 0.48) * w
        x1, y1 = cx + np.cos(a) * L, h * 0.85 + np.sin(a) * L
        d.line([(cx, h * 0.85), (x1, y1)], fill=(72, 60, 46, 255), width=3)
    n = 950 if kind == 'alder' else 720
    for i in range(n):
        r = rng.uniform(0, 1) ** 0.6; a = rng.uniform(0, 2 * np.pi)
        x = cx + np.cos(a) * r * w * 0.45; y = cy + np.sin(a) * r * h * 0.40 - h * 0.05
        sz = rng.uniform(5, 9) if kind == 'alder' else rng.uniform(7, 12)
        shade = 0.7 + 0.45 * (1 - (y / h)) * rng.uniform(0.8, 1.1)
        col = base
        if fall and rng.uniform() < 0.22: col = (196, 152, 52) if rng.uniform() < 0.7 else (204, 112, 44)
        c = jitter(tuple(v * shade for v in col), 0.12)
        ang = rng.uniform(0, 180)
        leaf = Image.new('RGBA', (int(sz * 2.2) + 2, int(sz * 2.2) + 2), (0, 0, 0, 0)); ld = ImageDraw.Draw(leaf)
        if kind == 'alder': ld.ellipse([1, sz * 0.5, sz * 2.2, sz * 1.7], fill=c)
        else:   # maple: 3 lobes
            for o in (-0.6, 0, 0.6):
                ld.ellipse([sz * 0.6 + o * sz * 0.8, sz * 0.3 + abs(o) * sz * 0.4, sz * 1.6 + o * sz * 0.8, sz * 1.5 + abs(o) * sz * 0.3], fill=c)
        leaf = leaf.rotate(ang, expand=False)
        im.alpha_composite(leaf, (int(x - sz), int(y - sz)))
    a = np.array(im.filter(ImageFilter.GaussianBlur(0.4))); a[..., 3] = np.where(a[..., 3] > 100, 255, 0)
    return Image.fromarray(a)

cluster(512, 512, (72, 110, 46), False, 'alder').save(T + 'alder_leaves.png')
cluster(512, 512, (88, 122, 44), True, 'maple').save(T + 'maple_leaves.png')

def impostor(w, h, base, fall, kind):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx = w / 2
    d.polygon([(cx - 5, h - 2), (cx + 5, h - 2), (cx + 2, h * 0.45), (cx - 2, h * 0.45)], fill=(78, 66, 52, 255))
    for i in range(2600):
        # irregular rounded crown (several lobes)
        lobe = rng.integers(0, 4); lx = cx + (lobe - 1.5) * w * 0.12; ly = h * (0.33 + 0.05 * (lobe % 2))
        r = rng.uniform(0, 1) ** 0.55
        a = rng.uniform(0, 2 * np.pi)
        x = lx + np.cos(a) * r * w * 0.34; y = ly + 0.04 * h + np.sin(a) * r * h * 0.31
        sz = rng.uniform(3, 7)
        shade = 0.55 + 0.6 * (1 - y / h) * rng.uniform(0.75, 1.1)
        col = base
        if fall and rng.uniform() < 0.2: col = (190, 150, 55)
        d.ellipse([x - sz, y - sz * 0.8, x + sz, y + sz * 0.8], fill=jitter(tuple(v * shade for v in col), 0.1))
    a = np.array(im.filter(ImageFilter.GaussianBlur(0.5))); a[..., 3] = np.where(a[..., 3] > 100, 255, 0)
    return Image.fromarray(a)

impostor(256, 512, (66, 102, 44), False, 'alder').save(T + 'alder_impostor.png')
impostor(256, 512, (84, 116, 42), True, 'maple').save(T + 'maple_impostor.png')

# distant hills: tiling dark fir canopy (fog does the aerial perspective)
n = sum(ndimage.gaussian_filter(rng.standard_normal((512, 512)), s, mode='wrap') * w for s, w in ((1.2, 1.0), (4, 1.5), (16, 2.0)))
n = (n - n.min()) / np.ptp(n)
col = np.stack([0.10 + 0.10 * n, 0.17 + 0.14 * n, 0.10 + 0.08 * n], -1)
dots = ndimage.gaussian_filter((rng.uniform(size=(512, 512)) > 0.985).astype(float), 1.0, mode='wrap')
col += dots[..., None] * np.array([0.10, 0.12, 0.05]) * 3
Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(T + 'hills_forest.png')

# skyline fringe: row of fir/deciduous silhouettes of varying height (alpha), bottom = canopy
W, H = 2048, 256
im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
x = 0
while x < W:
    h = rng.uniform(0.35, 0.95) * H if rng.uniform() < 0.8 else rng.uniform(0.95, 1.0) * H
    wdt = rng.uniform(10, 26)
    c = jitter((30, 48, 32), 0.12)
    if rng.uniform() < 0.18:   # rounded deciduous
        d.ellipse([x - wdt * 1.3, H - h * 0.8, x + wdt * 1.3, H - h * 0.8 + wdt * 2.4], fill=c); d.rectangle([x - wdt * 1.3, H - h * 0.8 + wdt * 1.2, x + wdt * 1.3, H], fill=c)
    else:
        d.polygon([(x, H - h), (x - wdt, H), (x + wdt, H)], fill=c)
        for k in range(6):   # ragged branch tips
            yy = H - h + (k + 1) * h / 7; ww = wdt * (k + 1) / 6
            d.line([(x - ww * 1.2, yy + 4), (x + ww * 1.2, yy + 4)], fill=c, width=2)
    x += wdt * rng.uniform(0.4, 1.1)
d.rectangle([0, H - 24, W, H], fill=(30, 48, 32, 255))
a = np.array(im); a[..., 3] = np.where(a[..., 3] > 100, 255, 0)
Image.fromarray(a).save(T + 'skyline_fringe.png')
print('tree v6 textures written')
