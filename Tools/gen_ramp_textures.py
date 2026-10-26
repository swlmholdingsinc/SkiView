"""Pass 14: procedural textures for the tournament jump ramp (our own, no reference pixels).
Surface: red waxed fibreglass sheets (default colour), wet: 1.22 m sheet seams with rows of countersunk fasteners, the
6.50 m / 7.00 m length marks at both edges, a diagonal ski track worn into the wax (scuffs, dull patches, fine scratches
along the run), wax build-up at the edges, algae film below the waterline.
Aprons / front skirt: white gelcoat panels on an aluminium frame (vertical stiffeners), bolt heads with faint rust weeps,
scratches, a dark wet splash band and green algae at the waterline.
Water film: a tileable streak / rivulet height field for the scrolling film shader (SkiView/RampWaterFilm).
Outputs to Assets/SkiView/Generated/ramp_*.png. u = across the ramp, v = along the surface (top edge at v = 1)."""
import numpy as np
from PIL import Image, ImageFilter
import os
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'SkiView', 'Generated')
rng = np.random.default_rng(14)

def noise(h, w, scale, oct=4):
    acc = np.zeros((h, w)); amp = 1.0; tot = 0
    for o in range(oct):
        gh, gw = max(2, int(h / scale * 2 ** o)), max(2, int(w / scale * 2 ** o))
        g = rng.random((gh, gw))
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        acc += amp * (np.asarray(im, float) / 255.0); tot += amp; amp *= 0.5
    return acc / tot

def stretch(h, w, sx, sy, scale, oct=3):
    n = noise(max(2, h // sy), max(2, w // sx), scale, oct)
    return np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), float) / 255

# ---------------- surface: 4.22 m x 7.45 m (6.85 out of water + 0.6 under) -> 1024 x 1792 (~4.1 mm/px)
W_M, L_M = 4.22, 7.45
W, H = 1024, 1792
ppm_u, ppm_v = W / W_M, H / L_M
v_m = np.repeat((np.arange(H)[::-1, None] + 0.5) / ppm_v, W, 1)     # metres from the low (submerged) end, row 0 = top edge
u_m = np.repeat(((np.arange(W)[None, :] + 0.5) / ppm_u), H, 0)
from_top = L_M - v_m
red = np.array([0.56, 0.055, 0.045])
n1 = noise(H, W, 260); n2 = noise(H, W, 40, 3); n3 = noise(H, W, 9, 2)
alb = np.ones((H, W, 3)) * red * (0.93 + 0.12 * n1)[..., None]
rough = np.zeros((H, W))                     # 0 = fresh wax, 1 = worn
# ski track: skiers cross from the course side (u high at the toe) to the middle of the top edge, ~0.9 m wide band of wear
track_c = 3.05 - (v_m / L_M) * 1.0
d = (u_m - track_c) / 0.55
track = np.exp(-d ** 2) * np.clip((v_m - 0.5) / 0.4, 0, 1)
streaks = stretch(H, W, 2, 40, 6, 3)          # fine lengthwise scratches
scuff = np.clip((noise(H, W, 28, 3) - 0.45) * 3, 0, 1)
wear = np.clip(track * (0.55 + 0.6 * scuff) * (0.7 + 0.5 * streaks), 0, 1)
rough += wear * 0.8
# worn wax shows a paler, pinker, matte gelcoat with white-ish scratch lines
alb = alb * (1 - 0.55 * wear[..., None]) + wear[..., None] * np.array([0.70, 0.30, 0.27]) * (0.85 + 0.25 * streaks)[..., None]
scratch = (stretch(H, W, 1, 90, 3, 2) > 0.80) * track
alb = alb * (1 - 0.35 * scratch[..., None]) + 0.35 * scratch[..., None] * np.array([0.85, 0.72, 0.70])
# wax build-up at the side edges (glossier, slightly darker / browner)
edge = np.exp(-(np.minimum(u_m, W_M - u_m) / 0.18) ** 2)
alb *= (1 - 0.18 * edge)[..., None]; rough -= 0.2 * edge
# black heel / skid marks in the track
skid = np.clip((noise(H, W, 14, 2) - 0.72) * 6, 0, 1) * track * (stretch(H, W, 3, 30, 5, 2) > 0.55)
alb *= (1 - 0.5 * skid)[..., None]
# sheet seams every 1.22 m along the run (4 ft sheets) + centre seam, with rows of countersunk stainless fasteners
for k in np.arange(1.22, L_M, 1.22):
    dd = np.abs(v_m - k)
    alb *= (1 - 0.45 * np.exp(-(dd / 0.0045) ** 2))[..., None]
    alb += (0.12 * np.exp(-((v_m - k - 0.007) / 0.003) ** 2))[..., None] * 0.5   # sealant edge catching light
    for side in (-0.06, 0.06):
        for uu in np.arange(0.10, W_M, 0.20):
            r = np.hypot(u_m - uu, v_m - (k + side))
            m = r < 0.009
            if m.any():
                alb[m] = alb[m] * 0.25 + 0.75 * np.array([0.60, 0.60, 0.62]) * (1 - 0.4 * (r[m] / 0.009))[..., None]
                rough[m] = 0.3
dc = np.abs(u_m - W_M / 2); alb *= (1 - 0.35 * np.exp(-(dc / 0.004) ** 2))[..., None]
for vv in np.arange(0.15, L_M, 0.25):
    for s in (-0.05, 0.05):
        r = np.hypot(u_m - (W_M / 2 + s), v_m - vv); m = r < 0.008
        if m.any(): alb[m] = alb[m] * 0.25 + 0.75 * np.array([0.6, 0.6, 0.62])
# length marks (white on red) at both side edges: 6.50 m and 7.00 m from the top edge, 0.45 m long, 40 mm wide
for mark in (6.50, 7.00):
    band = np.abs(from_top - mark) < 0.02
    side = (u_m < 0.47) | (u_m > W_M - 0.47)
    alb[band & side] = np.array([0.88, 0.88, 0.86]) * 0.9
# below the waterline (last 0.6 m): darker, olive algae film
sub = np.clip((0.68 - v_m) / 0.25, 0, 1)
alb = alb * (1 - sub[..., None]) + sub[..., None] * (alb * 0.45 + np.array([0.05, 0.08, 0.03]) * (0.6 + 0.8 * n2[..., None]))
img = np.clip(alb * 255, 0, 255).astype(np.uint8)
Image.fromarray(img).save(os.path.join(OUT, 'ramp_surface_albedo.png'))
# normal: seams (grooves), fastener dimples, scratch micro-relief, slight sheet waviness
height = 0.25 * n1 + 0.03 * streaks * track + 0.02 * n3
for k in np.arange(1.22, L_M, 1.22): height -= 0.25 * np.exp(-((v_m - k) / 0.004) ** 2)
gx = np.gradient(height, axis=1) * 30.0; gy = np.gradient(height, axis=0) * 30.0
nrm = np.stack([-gx, gy, np.ones_like(gx)], -1); nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
Image.fromarray(((nrm * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, 'ramp_surface_normal.png'))
# smoothness: wet wax 0.93, worn track ~0.62, marks 0.75, algae 0.55
sm = 0.93 - 0.32 * np.clip(rough, 0, 1) - 0.02 * n3
sm[(np.abs(from_top - 6.5) < 0.02) | (np.abs(from_top - 7.0) < 0.02)] = 0.75
sm = sm * (1 - sub) + 0.55 * sub
mask = np.zeros((H, W, 4), np.uint8); mask[..., 3] = np.clip(sm * 255, 0, 255).astype(np.uint8)
Image.fromarray(mask, 'RGBA').save(os.path.join(OUT, 'ramp_surface_mask.png'))

# ---------------- apron / front skirt: 1024 x 512, u along the run (~8.8 m, ~116 px/m), row 0 = top edge
# (saved flipped: Unity v = 0 at the image bottom, the mesh uses v = 0 at the top edge). Waterline at v = 0.74.
W2, H2 = 1024, 512
uu = np.repeat(np.arange(W2)[None, :], H2, 0).astype(float); vv = np.repeat(np.arange(H2)[:, None], W2, 1).astype(float)
gel = np.array([0.78, 0.79, 0.77])
a = np.ones((H2, W2, 3)) * gel * (0.94 + 0.08 * noise(H2, W2, 120))[..., None]
a *= (0.90 + 0.14 * stretch(H2, W2, 1, 60, 4, 2))[..., None]
# grime streaks washing down from the top trim and the bolt rows
a *= (1 - 0.18 * np.clip(stretch(H2, W2, 3, 80, 5, 3) - 0.45, 0, 1) * 2)[..., None]       # weathered streaks running down the panels
rough2 = np.full((H2, W2), 0.35)
for k in range(0, W2 + 1, 140):   # vertical aluminium stiffeners every ~1.2 m: a raised strip with bolt heads
    d = np.abs(uu - k)
    strip = d < 5
    a[strip] = a[strip] * 0.3 + 0.7 * np.array([0.60, 0.62, 0.64])
    a *= (1 - 0.35 * np.exp(-((d - 5.5) / 1.2) ** 2))[..., None]
    rough2[strip] = 0.55
    for r in range(22, int(H2 * 0.72), 38):
        dd = np.hypot(uu - k, vv - r)
        a += (0.10 * np.exp(-(dd / 2.2) ** 2))[..., None]
        weep = np.exp(-((uu - k) / 1.5) ** 2) * np.clip((vv - r) / 3, 0, 1) * np.exp(-np.clip(vv - r, 0, None) / 26)
        a = a * (1 - 0.35 * weep[..., None]) + 0.35 * weep[..., None] * np.array([0.55, 0.33, 0.18])
# scratches and scuffs
sc = (stretch(H2, W2, 12, 1, 3, 2) > 0.83).astype(float) * (noise(H2, W2, 40, 2) > 0.5)
a *= (1 - 0.12 * sc)[..., None]
# aluminium trim along the top edge (v = 0)
a[:10] = [0.62, 0.64, 0.67]; rough2[:10] = 0.25
# splash zone: darker wet band, then a slimy green algae line at the waterline and murky below
t = vv / H2
wet = np.clip((t - 0.50) / 0.20, 0, 1) ** 1.5
a *= (1 - 0.45 * wet)[..., None]; rough2 -= 0.25 * wet
alg = np.exp(-((t - 0.728) / 0.035) ** 2) * (0.6 + 0.6 * noise(H2, W2, 18, 3))
a = a * (1 - 0.7 * np.clip(alg, 0, 1)[..., None]) + 0.7 * np.clip(alg, 0, 1)[..., None] * np.array([0.16, 0.22, 0.08])
below = np.clip((t - 0.74) / 0.06, 0, 1)
a = a * (1 - below[..., None]) + below[..., None] * (a * 0.35 + np.array([0.06, 0.10, 0.06]))
Image.fromarray(np.flipud(np.clip(a * 255, 0, 255).astype(np.uint8))).save(os.path.join(OUT, 'ramp_apron_albedo.png'))
sm2 = np.clip(1 - rough2, 0, 1) * 0.75 + 0.15 * wet
m2 = np.zeros((H2, W2, 4), np.uint8); m2[..., 3] = np.clip(sm2 * 255, 0, 255).astype(np.uint8); m2[:10, :, 0] = 230
Image.fromarray(np.flipud(m2).copy(), 'RGBA').save(os.path.join(OUT, 'ramp_apron_mask.png'))
# pass 15: apron normal map (they read as flat grey): raised stiffener strips, domed bolt heads, oil-canned panel waviness,
# gelcoat orange-peel, raised top trim. Own RNG so the other ramp textures don't change.
r15 = np.random.default_rng(1515)
def n15(h, w, scale):
    g = r15.random((max(2, int(h / scale)), max(2, int(w / scale))))
    return np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), float) / 255
hh = 0.55 * n15(H2, W2, 70) + 0.05 * n15(H2, W2, 3)
for k in range(0, W2 + 1, 140):
    d = np.abs(uu - k)
    hh += 1.6 * np.clip((6.5 - d) / 2.0, 0, 1)
    pan = (uu > k) & (uu < k + 140)
    hh += pan * 0.5 * np.sin(np.clip((uu - k) / 140, 0, 1) * np.pi) * (0.6 + 0.8 * r15.random())    # panel bulge
    for r in range(22, int(H2 * 0.72), 38):
        hh += 1.2 * np.exp(-(np.hypot(uu - k, vv - r) / 2.4) ** 2)
hh[:10] += 1.5
gy, gx = np.gradient(hh)
nz = np.ones_like(hh); nn = np.stack([-gx * 1.4, gy * 1.4, nz], -1); nn /= np.linalg.norm(nn, axis=-1, keepdims=True)
Image.fromarray(np.flipud(((nn * 0.5 + 0.5) * 255).astype(np.uint8)).copy()).save(os.path.join(OUT, 'ramp_apron_normal.png'))
# per-panel gelcoat tone (slightly different sheets), applied on top of the saved albedo
alb = np.asarray(Image.open(os.path.join(OUT, 'ramp_apron_albedo.png')), float) / 255
tone = np.ones((H2, W2))
for k in range(0, W2 + 1, 140): tone[:, k:k + 140] = 0.95 + 0.09 * r15.random()
alb *= np.flipud(tone)[..., None]
Image.fromarray(np.clip(alb * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, 'ramp_apron_albedo.png'))

# ---------------- water film: 256 x 512, tileable, R = film thickness (rivulets meandering down the slope), G = glints
FW, FH = 256, 512
def tile_noise(h, w, gx, gy):
    g = rng.random((gy, gx)); g = np.tile(g, (3, 3))
    im = Image.fromarray((g * 255).astype(np.uint8)).resize((w * 3, h * 3), Image.BICUBIC)
    return (np.asarray(im, float) / 255)[h:2 * h, w:2 * w]
riv = 0.6 * tile_noise(FH, FW, 48, 6) + 0.3 * tile_noise(FH, FW, 96, 12) + 0.1 * tile_noise(FH, FW, 24, 48)
riv = np.clip((riv - 0.35) * 1.8, 0, 1)
gl = np.clip((tile_noise(FH, FW, 64, 32) - 0.6) * 4, 0, 1)
film = np.zeros((FH, FW, 4), np.uint8)
film[..., 0] = (riv * 255).astype(np.uint8); film[..., 1] = (gl * 255).astype(np.uint8); film[..., 2] = 128; film[..., 3] = 255
Image.fromarray(film, 'RGBA').save(os.path.join(OUT, 'ramp_waterfilm.png'))
print('ok', img.shape)
