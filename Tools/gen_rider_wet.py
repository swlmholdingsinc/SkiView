"""Pass 7: wet-look maps for the rider (derived from the staged Mixamo Remy textures; output stays out of git).
- skin_wet_ms.png  : URP Lit metallic/smoothness map (R=0 metallic, A=smoothness). Gloss-map breakup + droplet speckles,
                     so the wet sheen is broken up rather than a uniform plastic highlight.
- hair_wet.png     : hair albedo darkened and cooled (wet hair reads ~35-40% darker), alpha = hair opacity (for alpha clip).
- hair_wet_ms.png  : strand-aligned smoothness streaks.
"""
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
import os
D = os.path.join(os.path.dirname(__file__), "../Assets/SkiView/ThirdParty/Mixamo/")
T, O = D + "Textures/", D + "Derived/"
rng = np.random.default_rng(77)

def load(n, size=None, mode=None):
    im = Image.open(T + n)
    if mode: im = im.convert(mode)
    if size: im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0

S = 1024
gloss = load("Remy_Body_Gloss.png", S, "L")
g = (gloss - gloss.min()) / max(1e-3, gloss.max() - gloss.min())
# droplets: sparse small bright spots + a low-frequency wet/dry mottling (water sheets unevenly over skin)
drops = (rng.random((S, S)) > 0.9965).astype(np.float32)
drops = np.clip(gaussian_filter(drops, 1.1) * 9.0, 0, 1)
mott = gaussian_filter(rng.random((S, S)), 18); mott = (mott - mott.min()) / (mott.max() - mott.min())
sm = 0.30 + 0.30 * g + 0.16 * mott + 0.30 * drops
sm = np.clip(sm, 0, 0.92)
ms = np.zeros((S, S, 4), np.float32); ms[..., 3] = sm
Image.fromarray((ms * 255).astype(np.uint8), "RGBA").save(O + "skin_wet_ms.png")

H = 1024
hd = load("Remy_Hair_Diffuse.png", H, "RGB")
op = load("Remy_Hair_Opacity.png", H, "L")
wet = hd * np.array([0.60, 0.60, 0.64])          # darker, slightly cooler
# clump: wet hair groups into strands -> boost local contrast along the texture's strand direction (V)
streak = gaussian_filter(rng.random((H, H)), (14, 0.8)); streak = (streak - streak.min()) / (streak.max() - streak.min())
wet *= (0.82 + 0.36 * streak)[..., None]
rgba = np.dstack([np.clip(wet, 0, 1), op])
Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(O + "hair_wet.png")
hg = load("Remy_Hair_Gloss.png", H, "L"); hg = (hg - hg.min()) / max(1e-3, hg.max() - hg.min())
hs = np.clip(0.42 + 0.25 * hg + 0.25 * streak, 0, 0.9)
hm = np.zeros((H, H, 4), np.float32); hm[..., 3] = hs
Image.fromarray((hm * 255).astype(np.uint8), "RGBA").save(O + "hair_wet_ms.png")
print("skin smooth mean %.2f, hair smooth mean %.2f, hair opacity mean %.2f" % (sm.mean(), hs.mean(), op.mean()))

# pass 8: strand normal map (hair_wet_n.png) - fine strand-aligned ridges (strands run along V) on top of Remy's own hair
# normal, so the highlight breaks into strands/clumps instead of one smooth helmet sheen. Cheap: one more 1024 map.
fine = gaussian_filter(rng.random((H, H)), (9, 0.7)); fine = (fine - fine.min()) / (fine.max() - fine.min())
clump = gaussian_filter(rng.random((H, H)), (40, 3.0)); clump = (clump - clump.min()) / (clump.max() - clump.min())
hh = 0.65 * fine + 0.35 * clump
gy, gx = np.gradient(hh)
nx, ny = -gx * 14.0, -gy * 4.0
try:
    base = load("Remy_Hair_Normal.png", H, "RGB") * 2 - 1
    nx, ny = nx + base[..., 0], ny + base[..., 1]
except FileNotFoundError:
    pass
nz = np.ones_like(nx); l = np.sqrt(nx * nx + ny * ny + nz * nz)
nrm = np.dstack([nx / l, ny / l, nz / l]) * 0.5 + 0.5
Image.fromarray((np.clip(nrm, 0, 1) * 255).astype(np.uint8), "RGB").save(O + "hair_wet_n.png")
print("hair strand normal written")
