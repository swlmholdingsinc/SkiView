"""Pass 14: spray particle textures (our own). Soft everywhere: no particle has a hard edge.
spray_mist.png  128: soft, lumpy mist puff (gaussian falloff x fbm breakup), low density.
spray_drop.png   64: droplet - bright core with a soft halo (stretched along the velocity it becomes a motion streak).
spray_sheet.png 128: torn translucent water sheet - fibrous lace (thin bright filaments and holes) inside a soft window.
RGB = white (lit by the shader), A = density."""
import numpy as np
from PIL import Image
import os
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SkiView', 'Generated')
rng = np.random.default_rng(140)
def fbm(n, oct=5, base=4):
    acc = np.zeros((n, n)); amp = 1; tot = 0
    for o in range(oct):
        g = rng.random((base * 2 ** o, base * 2 ** o))
        acc += amp * np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), float) / 255; tot += amp; amp *= 0.55
    return acc / tot
def save(a, name):
    a = np.clip(a, 0, 1); img = np.zeros(a.shape + (4,), np.uint8); img[..., :3] = 255; img[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, name))
n = 128; y, x = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1; r = np.hypot(x, y)
f = fbm(n)
f2 = fbm(n, 5, 8)
mist = np.exp(-(r / 0.6) ** 2) ** 1.3 * np.clip(1.6 * (f - 0.42) + 0.6 * (f2 - 0.5), 0, 1) * np.clip((1 - r) / 0.35, 0, 1)   # wispy, no solid core
save(mist * 0.6, 'spray_mist.png')
n2 = 64; y2, x2 = np.mgrid[0:n2, 0:n2] / (n2 - 1) * 2 - 1; r2 = np.hypot(x2, y2)
drop = np.clip(np.exp(-(r2 / 0.22) ** 2) * 1.0 + np.exp(-(r2 / 0.55) ** 2) * 0.35, 0, 1) * np.clip((1 - r2) / 0.2, 0, 1)
save(drop, 'spray_drop.png')
# sheet: filaments = ridges of a stretched noise field, broken by holes, in a soft elliptical window
g = fbm(n, 4, 3); h = fbm(n, 5, 6)
ridge = 1 - np.abs(np.sin((y * 9 + g * 6) * np.pi * 0.5))           # wavy parallel filaments
ridge = ridge ** 3
holes = np.clip((h - 0.38) * 3.0, 0, 1)
win = np.exp(-((x / 0.75) ** 2 + (y / 0.6) ** 2) ** 1.4) * np.clip((1 - r) / 0.3, 0, 1)
sheet = (0.18 + 0.55 * ridge) * holes * win
save(sheet, 'spray_sheet.png')
print('ok')
