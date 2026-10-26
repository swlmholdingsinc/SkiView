"""Environment realism pass (v5): prepares CC0 textures (resized for mobile), generates foliage card textures,
copies the partly-cloudy HDRI and measures its sun direction. Output: Assets/SkiView/Environment/."""
import os, json, shutil, numpy as np
from PIL import Image, ImageDraw, ImageFilter
S = '/workspace/skiview-assets-staging/'; O = '/workspace/skiview-unity/Assets/SkiView/Environment/'
T = O + 'Textures/'; os.makedirs(T, exist_ok=True); os.makedirs(O + 'Sky', exist_ok=True)

def rs(src, dst, size, normal=False):
    if not os.path.exists(src) or os.path.getsize(src) == 0: print('missing/empty', src); return False
    im = Image.open(src).convert('RGB').resize((size, size), Image.LANCZOS)
    im.save(T + dst, quality=88) if dst.endswith('.jpg') else im.save(T + dst)
    print('tex', dst, os.path.getsize(T + dst) // 1024, 'KB')

rs(S + 'ambientcg/Grass004/Grass004_2K-JPG_Color.jpg', 'lawn_color.jpg', 1024)
rs(S + 'ambientcg/Grass004/Grass004_2K-JPG_NormalGL.jpg', 'lawn_normal.png', 512)
rs(S + 'ambientcg/Grass005/Grass005_2K-JPG_Color.jpg', 'drygrass_color.jpg', 1024)   # polyhaven aerial_grass_rock diff is a 0-byte download
rs(S + 'ambientcg/Grass005/Grass005_2K-JPG_NormalGL.jpg', 'drygrass_normal.png', 512)
rs(S + 'polyhaven/textures/forest_ground_04/forest_ground_04_diff_2k.jpg', 'forest_color.jpg', 1024)
rs(S + 'polyhaven/textures/forest_ground_04/forest_ground_04_nor_gl_2k.jpg', 'forest_normal.png', 512)
rs(S + 'ambientcg/Rocks011/Rocks011_2K-JPG_Color.jpg', 'shore_color.jpg', 1024)
rs(S + 'ambientcg/Rocks011/Rocks011_2K-JPG_NormalGL.jpg', 'shore_normal.png', 512)
rs(S + 'ambientcg/Planks012/Planks012_2K-JPG_Color.jpg', 'dock_color.jpg', 1024)
rs(S + 'ambientcg/Planks012/Planks012_2K-JPG_NormalGL.jpg', 'dock_normal.png', 512)
rs(S + 'polyhaven/textures/bark_brown_02/bark_brown_02_diff_2k.jpg', 'bark_color.jpg', 512)
rs(S + 'polyhaven/textures/bark_brown_02/bark_brown_02_nor_gl_2k.jpg', 'bark_normal.png', 512)
rs(S + 'ambientcg/WoodSiding009/WoodSiding009_2K-JPG_Color.jpg', 'siding_color.jpg', 512)

# ---- foliage cards (original, procedural). RGBA, alpha-clipped.
rng = np.random.default_rng(17)
def spray(w, h, base, var, kind):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    # main twig along x (card root at the left edge = trunk side), drooping slightly
    def col(k=0):
        c = np.array(base) * (1 + rng.uniform(-var, var)) + k
        return tuple(int(np.clip(x, 0, 255)) for x in c) + (255,)
    pts = [(int(x), int(h * 0.5 + (x / w) ** 2 * h * 0.18)) for x in np.linspace(2, w - 6, 40)]
    d.line(pts, fill=(70, 50, 34, 255), width=3)
    nb = 16 if kind == 'fir' else 11
    for i in range(nb):
        t = 0.08 + 0.9 * i / nb; x0, y0 = pts[int(t * 39)]
        for side in (-1, 1):
            L = (1 - t) * h * (0.42 if kind == 'fir' else 0.5) + h * 0.06
            ang = side * (0.95 if kind == 'fir' else 0.8) + rng.uniform(-0.15, 0.15)
            dx, dy = np.cos(ang * 0.6) * L * 0.75, np.sin(ang) * L
            x1, y1 = x0 + dx, y0 + dy
            d.line([(x0, y0), (x1, y1)], fill=(60, 48, 30, 255), width=2)
            n = int(L / 2.2)
            for k in range(n):
                u = k / max(1, n - 1); px, py = x0 + dx * u, y0 + dy * u
                if kind == 'fir':   # flat needles both sides of the branchlet
                    for s2 in (-1, 1):
                        nl = rng.uniform(5, 9) * (1.1 - 0.5 * u)
                        a2 = np.arctan2(dy, dx) + s2 * rng.uniform(0.9, 1.3)
                        d.line([(px, py), (px + np.cos(a2) * nl, py + np.sin(a2) * nl)], fill=col(rng.uniform(-10, 14)), width=2)
                else:               # cedar: flat scale-like lacy fans
                    r = rng.uniform(3, 6) * (1.15 - 0.5 * u)
                    d.ellipse([px - r, py - r * 0.6, px + r, py + r * 0.6], fill=col(rng.uniform(-8, 18)))
    im = im.filter(ImageFilter.GaussianBlur(0.4))
    a = np.array(im); a[..., 3] = np.where(a[..., 3] > 90, 255, 0); return Image.fromarray(a)

spray(512, 256, (48, 74, 42), 0.18, 'fir').save(T + 'fir_spray.png')
spray(512, 256, (92, 122, 50), 0.18, 'cedar').save(T + 'cedar_spray.png')

def impostor(w, h, base, kind):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cx = w / 2; top = 6; bot = h - 4
    d.rectangle([cx - 3, h * 0.75, cx + 3, bot], fill=(64, 46, 32, 255))
    for i in range(1400):
        t = rng.uniform(0, 1) ** 0.8; y = top + t * (h * 0.86)
        rmax = (t ** (0.9 if kind == 'fir' else 0.75)) * w * (0.44 if kind == 'fir' else 0.48) + 3
        x = cx + rng.uniform(-1, 1) * rmax * rng.uniform(0.3, 1)
        sz = rng.uniform(3, 7) * (0.6 + t)
        shade = 0.55 + 0.45 * (x - cx + rmax) / (2 * rmax + 1e-3) * rng.uniform(0.7, 1.1)  # light from the left/right
        c = tuple(int(np.clip(v * shade * rng.uniform(0.8, 1.15), 0, 255)) for v in base) + (255,)
        dy = sz * (0.5 if kind == 'fir' else 0.9)
        d.ellipse([x - sz, y - dy * 0.5, x + sz, y + dy], fill=c)
    a = np.array(im.filter(ImageFilter.GaussianBlur(0.5))); a[..., 3] = np.where(a[..., 3] > 100, 255, 0)
    return Image.fromarray(a)

impostor(256, 512, (46, 72, 40), 'fir').save(T + 'fir_impostor.png')
impostor(256, 512, (84, 112, 48), 'cedar').save(T + 'cedar_impostor.png')

# green standing-seam metal canopy (community dock lifts)
y, x = np.mgrid[0:256, 0:256]
seam = (np.abs(((x / 256 * 8) % 1) - 0.5) < 0.04).astype(float)
g = np.stack([40 + 0 * x, 92 + 0 * x, 66 + 0 * x], -1) * (1 - 0.25 * seam[..., None]) + rng.normal(0, 2, (256, 256, 1))
Image.fromarray(np.clip(g, 0, 255).astype(np.uint8)).save(T + 'canopy_metal.png')

# ---- HDRI: copy + sun direction (brightest region of the equirect)
hdr = S + 'polyhaven/hdri/kloofendal_48d_partly_cloudy_puresky_2k.hdr'
dst = O + 'Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr'
shutil.copy(hdr, dst)
def read_hdr(p):
    f = open(p, 'rb'); hdrtxt = b''
    while True:
        line = f.readline()
        if line.strip() == b'': break
        hdrtxt += line
    dims = f.readline().split(); H, W = int(dims[1]), int(dims[3])
    data = np.frombuffer(f.read(), np.uint8); img = np.zeros((H, W, 4), np.uint8); pos = 0
    for yy in range(H):
        pos += 4
        for c in range(4):
            xx = 0
            while xx < W:
                n = int(data[pos]); pos += 1
                if n > 128:
                    n -= 128; img[yy, xx:xx + n, c] = data[pos]; pos += 1
                else:
                    img[yy, xx:xx + n, c] = data[pos:pos + n]; pos += n
                xx += n
    e = img[..., 3].astype(np.float32)
    return img[..., :3].astype(np.float32) * np.ldexp(1.0, (e - 136).astype(int))[..., None] * (e[..., None] > 0), W, H
rgb, W, H = read_hdr(hdr)
lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
yy, xx = np.unravel_index(np.argmax(lum), lum.shape)
u, v = (xx + 0.5) / W, (yy + 0.5) / H
elev = (0.5 - v) * 180.0; azim = (u - 0.5) * 360.0   # Unity equirect: u=0.5 -> +Z, increasing u -> towards +X? (checked in Unity log)
json.dump({'u': u, 'v': v, 'elevation_deg': elev, 'azimuth_deg': azim, 'peak': float(lum.max())}, open(O + 'Sky/sun.json', 'w'), indent=1)
print('sun', u, v, elev, azim, lum.max())
# small preview for docs
prev = np.clip((rgb / (1 + rgb)) ** (1 / 2.2) * 255, 0, 255).astype(np.uint8)
Image.fromarray(prev).resize((512, 256)).save('/tmp/hdri_preview.jpg')

# ---- late-summer dry lawn (Scott's lake photos show tan/straw grass): recolour Grass005 toward straw, keep its detail
def straw(src, dst, amount=0.78):
    im = np.asarray(Image.open(T + src).convert('RGB')).astype(np.float32) / 255.0
    lum = im @ np.array([0.30, 0.59, 0.11], np.float32)
    lum = (lum - lum.mean()) * 1.25 + lum.mean()
    strawc = np.array([0.66, 0.57, 0.38], np.float32); greenc = np.array([0.45, 0.48, 0.26], np.float32)
    mix = np.clip(np.asarray(Image.open(T + src).convert('L').resize((64, 64)).resize(im.shape[1::-1], Image.BICUBIC)) / 255.0, 0, 1)[..., None]
    base = strawc * (1 - 0.35 * mix) + greenc * 0.35 * mix
    out = im * (1 - amount) + (lum[..., None] / max(lum.mean(), 1e-3)) * base * amount
    Image.fromarray(np.clip(out * 255, 0, 255).astype(np.uint8)).save(T + dst, quality=90)
    print('straw', dst)
straw('drygrass_color.jpg', 'drygrass_color.jpg')
