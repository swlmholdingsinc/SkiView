"""Pass 7: fuller conifer impostors + a dark rounded 'canopy mass' impostor.
The pass-5 impostors were narrow spires with ~25% bare trunk, so the far tree line (drawn almost entirely with impostors)
read as evenly spaced spikes. Edge trees on a lake shore keep their lower branches, so the crowns now come down to ~6%
of the height. They are wider and lumpier (3-5 sub-lobes), darker, and shaded from the sun side."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import os
T = os.path.join(os.path.dirname(__file__), "../Assets/SkiView/Environment/Textures/")
rng = np.random.default_rng(707)

def blob_tree(w, h, base, kind, n=3000):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx = w / 2; top = 4
    d.rectangle([cx - 3, h * 0.88, cx + 3, h - 3], fill=(52, 40, 30, 255))
    # irregular silhouette: per-height half-width with a few lumps (branch tiers) and asymmetry
    ys = np.linspace(0, 1, 64)
    if kind == 'canopy':
        prof = np.sqrt(np.clip(1 - ((ys - 0.5) / 0.5) ** 2, 0, 1)) ** 0.8
    else:
        prof = ys ** (0.72 if kind == 'fir' else 0.62)
        prof = np.minimum(prof, 1.0 - 0.25 * np.clip((ys - 0.9) / 0.1, 0, 1))
    lumps = 1 + 0.16 * np.sin(ys * rng.uniform(9, 14) + rng.uniform(0, 6)) + 0.08 * np.sin(ys * 31 + rng.uniform(0, 6))
    halfL = prof * lumps * w * (0.47 if kind != 'cedar' else 0.48)
    halfR = prof * (1 + 0.16 * np.sin(ys * rng.uniform(9, 14) + rng.uniform(0, 6))) * w * 0.47
    span = 0.94 if kind != 'canopy' else 0.9
    for i in range(n):
        t = rng.uniform(0, 1) ** 0.85
        y = top + t * h * span
        k = min(63, int(t * 63))
        side = rng.uniform(-1, 1)
        r = (halfL[k] if side < 0 else halfR[k]) + 2
        x = cx + side * r * rng.uniform(0.15, 1) ** 0.6
        sz = rng.uniform(3, 7.5) * (0.65 + 0.7 * t)
        # sun from the upper left: lit side lighter, inner/lower darker (self-shadowed mass)
        lit = 0.5 + 0.35 * (cx - x) / (r + 1e-3) * 0.5 + 0.25 * (1 - t) * 0.0
        lit = np.clip(lit + 0.25 * (abs(side) ** 2) * (1 if side < 0 else 0.4) - 0.18 * (t > 0.8), 0.35, 1.05)
        c = tuple(int(np.clip(v * lit * rng.uniform(0.82, 1.12), 0, 255)) for v in base) + (255,)
        dy = sz * (0.55 if kind == 'fir' else 0.85)
        d.ellipse([x - sz, y - dy * 0.5, x + sz, y + dy], fill=c)
    a = np.array(im.filter(ImageFilter.GaussianBlur(0.5))); a[..., 3] = np.where(a[..., 3] > 100, 255, 0)
    return Image.fromarray(a)

blob_tree(256, 512, (38, 60, 34), 'fir').save(T + 'fir_impostor.png')
blob_tree(256, 512, (60, 84, 38), 'cedar').save(T + 'cedar_impostor.png')
blob_tree(256, 512, (34, 54, 32), 'canopy', 3600).save(T + 'canopy_impostor.png')
print('ok')
