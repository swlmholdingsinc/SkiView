#!/usr/bin/env python3
"""Pass 6 shoreline: extract the waterline (y = +0.02 m contour of heightmap.r16, marching squares), chain + smooth +
resample it to 1.5 m polylines with inland normals -> Generated/shoreline.json. Also builds the two edging strip textures
(rock rip-rap / grass bank) with a dark wet band at the water and a ragged alpha edge into the lawn."""
import json, numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage
G = 'Assets/SkiView/Generated/'; T = 'Assets/SkiView/Environment/Textures/'
v = json.load(open(G + 'venue.json')); tr = v['terrain']
RES, SIZE, YMIN, HR = tr['res'], tr['size'], tr['ymin'], tr['hrange']
raw = np.fromfile(G + 'heightmap.r16', '<u2').reshape(RES, RES).astype(np.float64) / 65535.0
H = YMIN + raw * HR            # H[zi, xi], zi = 0 at z = -SIZE/2 (Unity convention)
PX = SIZE / (RES - 1)
LEVEL = 0.02
lb = v['lakeBounds']; M = 30.0

def world(xi, zi): return (-SIZE / 2 + xi * PX, -SIZE / 2 + zi * PX)

# ---- marching squares on cells inside the lake bounds
x0 = int((lb[0] - M + SIZE / 2) / PX); x1 = int((lb[1] + M + SIZE / 2) / PX) + 1
z0 = int((lb[2] - M + SIZE / 2) / PX); z1 = int((lb[3] + M + SIZE / 2) / PX) + 1
segs = []
def interp(a, b, ha, hb):
    t = (LEVEL - ha) / (hb - ha); return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
for zi in range(z0, z1):
    for xi in range(x0, x1):
        c = [(xi, zi), (xi + 1, zi), (xi + 1, zi + 1), (xi, zi + 1)]
        hv = [H[p[1], p[0]] for p in c]
        idx = sum(1 << k for k in range(4) if hv[k] > LEVEL)
        if idx in (0, 15): continue
        e = {}
        for k in range(4):
            a, b = k, (k + 1) % 4
            if (hv[a] > LEVEL) != (hv[b] > LEVEL): e[k] = interp(c[a], c[b], hv[a], hv[b])
        ks = sorted(e)
        if len(ks) == 2: segs.append((e[ks[0]], e[ks[1]]))
        elif len(ks) == 4: segs.append((e[0], e[1])); segs.append((e[2], e[3]))
# ---- chain
key = lambda p: (round(p[0], 4), round(p[1], 4))
adj = {}
for i, (a, b) in enumerate(segs):
    adj.setdefault(key(a), []).append(i); adj.setdefault(key(b), []).append(i)
used = [False] * len(segs); lines = []
for i in range(len(segs)):
    if used[i]: continue
    used[i] = True; a, b = segs[i]; line = [a, b]
    for direction in (0, 1):
        while True:
            end = line[-1] if direction == 0 else line[0]
            nxt = None
            for j in adj.get(key(end), []):
                if not used[j]: nxt = j; break
            if nxt is None: break
            used[nxt] = True; p, q = segs[nxt]
            o = q if key(p) == key(end) else p
            if direction == 0: line.append(o)
            else: line.insert(0, o)
    lines.append(np.array(line))
def hgrid(x, z):   # bilinear height at world x,z
    fx = (x + SIZE / 2) / PX; fz = (z + SIZE / 2) / PX
    return ndimage.map_coordinates(H, [[fz], [fx]], order=1, mode='nearest')[0]
out = []; total = 0
for ln in lines:
    pts = np.array([world(p[0], p[1]) for p in ln])
    seglen = np.linalg.norm(np.diff(pts, axis=0), axis=1); L = seglen.sum()
    if L < 25: continue
    closed = np.linalg.norm(pts[0] - pts[-1]) < 2 * PX
    # smooth (3 passes of neighbour averaging) then resample at 1.5 m
    for _ in range(3):
        sm = pts.copy(); sm[1:-1] = (pts[:-2] + 2 * pts[1:-1] + pts[2:]) / 4
        if closed: sm[0] = sm[-1] = (pts[-2] + 2 * pts[0] + pts[1]) / 4
        pts = sm
    d = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
    n = max(4, int(d[-1] / 1.5)); s = np.linspace(0, d[-1], n)
    rs = np.stack([np.interp(s, d, pts[:, 0]), np.interp(s, d, pts[:, 1])], 1)
    tg = np.gradient(rs, axis=0); tg /= np.linalg.norm(tg, axis=1, keepdims=True) + 1e-9
    nrm = np.stack([-tg[:, 1], tg[:, 0]], 1)
    # orient normals inland (uphill)
    up = np.array([hgrid(*(p + q * 2.0)) - hgrid(*(p - q * 2.0)) for p, q in zip(rs[::max(1, n // 20)], nrm[::max(1, n // 20)])])
    if up.mean() < 0: nrm = -nrm
    out.append({'closed': bool(closed), 'p': [[round(float(a), 3), round(float(b), 3), round(float(c), 4), round(float(e), 4)] for (a, b), (c, e) in zip(rs, nrm)]})
    total += d[-1]
json.dump({'level': LEVEL, 'lines': out}, open(G + 'shoreline.json', 'w'))
print(f'shoreline: {len(out)} polylines, {total:.0f} m, {sum(len(o["p"]) for o in out)} points')

# ---- strip textures: v = 0 (row bottom) under water -> v = 1 (row top) inland
W, Hh = 1024, 256
rng = np.random.default_rng(6)
def load(f): return np.asarray(Image.open(T + f).convert('RGB').resize((W, W))).astype(np.float32) / 255
rock = load('shore_color.jpg')[:Hh * 2:2]; lawn = load('lawn_color.jpg')[:Hh]; mud = load('forest_color.jpg')[:Hh]
vv = np.linspace(1, 0, Hh)[:, None, None]   # row 0 = top = inland (v=1)
noise = ndimage.gaussian_filter(rng.standard_normal((Hh, W)), 6); noise = (noise - noise.min()) / (np.ptp(noise) + 1e-9)
fine = ndimage.gaussian_filter(rng.standard_normal((Hh, W)), 1.5); fine = (fine - fine.min()) / (np.ptp(fine) + 1e-9)
def wetness(vline):   # 1 at/under the waterline, fading to 0 by vline
    return np.clip((vline - vv[..., 0] + (noise - 0.5) * 0.12) / 0.10, 0, 1)
def strip(base_low, base_high, split, wet_to, alpha_from, algae):
    mix = np.clip((vv[..., 0] - split + (noise - 0.5) * 0.25) / 0.10, 0, 1)[..., None]
    col = base_low * (1 - mix) + base_high * mix
    w = wetness(wet_to)[..., None]
    col = col * (1 - w * 0.65) * (1 - w * np.array([0.05, 0.0, 0.08]))      # darker, slightly green-brown when wet
    under = np.clip((0.30 - vv[..., 0]) / 0.30, 0, 1)[..., None]
    col = col * (1 - under * 0.35) + algae * under * 0.15
    a = ((vv[..., 0] - alpha_from) / (1 - alpha_from) < (0.55 + 0.6 * (fine - 0.5) + 0.5 * (noise - 0.5)) * 1.0)
    alpha = np.where(vv[..., 0] < alpha_from, 1.0, a.astype(np.float32))
    img = np.dstack([np.clip(col, 0, 1), alpha])
    return Image.fromarray((img * 255).astype(np.uint8), 'RGBA')
algae = np.array([0.20, 0.24, 0.12])
strip(rock * np.array([0.80, 0.77, 0.70]), lawn * np.array([0.95, 0.95, 0.8]), 0.60, 0.50, 0.78, algae).save(T + 'shore_strip_rock.png')
strip(mud * 0.8, lawn, 0.48, 0.50, 0.74, algae).save(T + 'shore_strip_grass.png')
print('strip textures written')
