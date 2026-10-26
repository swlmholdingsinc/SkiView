"""Procedural tileable foam texture for the water shader (pass 9 prop wash / wake caps). Original, no third-party content.
R = foam density: bubbly cell walls (periodic Worley F2-F1) at two scales, gathered into clumps and streaks by periodic fbm.
Shader thresholds coverage against R so foam breaks into patches/lace as it fades."""
import numpy as np, sys
from PIL import Image
N = 512; rng = np.random.default_rng(9)
def worley(cells):
    pts = (np.arange(cells)[:, None, None] * 0 + rng.random((cells, cells, 2)))
    yy, xx = np.mgrid[0:N, 0:N] / N * cells
    ci, cj = np.floor(yy).astype(int), np.floor(xx).astype(int)
    f1 = np.full((N, N), 9.0); f2 = np.full((N, N), 9.0)
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            ii, jj = ci + di, cj + dj
            p = pts[ii % cells, jj % cells]
            d = np.hypot(yy - (ii + p[..., 0]), xx - (jj + p[..., 1]))
            f2 = np.where(d < f1, f1, np.minimum(f2, d)); f1 = np.minimum(f1, d)
    return f1, f2
def fbm(oct=5, base=4):
    out = np.zeros((N, N))
    for o in range(oct):
        c = base * 2 ** o
        g = rng.random((c, c)); yy, xx = np.mgrid[0:N, 0:N] / N * c
        i0, j0 = np.floor(yy).astype(int), np.floor(xx).astype(int); fy, fx = yy - i0, xx - j0
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a = g[i0 % c, j0 % c]; b = g[i0 % c, (j0 + 1) % c]; cc = g[(i0 + 1) % c, j0 % c]; d = g[(i0 + 1) % c, (j0 + 1) % c]
        out += ((a * (1 - fx) + b * fx) * (1 - fy) + (cc * (1 - fx) + d * fx) * fy) * 0.5 ** o
    return out / (2 - 0.5 ** (oct - 1))
f1, f2 = worley(18); walls1 = np.exp(-((f2 - f1) / 0.22) ** 2)
g1, g2 = worley(46); walls2 = np.exp(-((g2 - g1) / 0.25) ** 2)
clump = fbm(5, 4)
clump2 = fbm(4, 12)
blot = np.clip((clump * 0.65 + clump2 * 0.35 - 0.30) * 2.0, 0, 1) ** 1.3     # foam patches
foam = blot * (0.55 + 0.45 * (0.6 * walls1 + 0.4 * walls2)) + 0.25 * walls2 * clump
foam = (foam - foam.min()) / (foam.max() - foam.min())
img = (np.clip(foam, 0, 1) ** 0.9 * 255).astype(np.uint8)
out = sys.argv[1] if len(sys.argv) > 1 else 'Assets/SkiView/Environment/Water/foam.png'
Image.fromarray(img, 'L').save(out); print('wrote', out, img.mean())
