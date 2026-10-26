#!/usr/bin/env python3
"""Phase 1 review #3: per-lake sky variants with a tamed sun (writes next to the source .hdr).
  clear_bg_nosun.hdr : syferfontein clear sky with the low (18 deg) HDRI sun painted out -> Villa Serena is noon (sun at 79.9 deg, out of frame)
  sunset_bg_cap.hdr  : rosendal sunset with the horizon sun/glow soft-capped (smaller, dimmer disc) -> Tall Pine
"""
import cv2, numpy as np, os
D = os.path.join(os.path.dirname(__file__), '..', 'Assets/SkiView/Environment/Sky')
def rd(f): return cv2.imread(os.path.join(D, f), cv2.IMREAD_ANYDEPTH | cv2.IMREAD_COLOR)
def wr(f, a): cv2.imwrite(os.path.join(D, f), a.astype(np.float32)); print('wrote', f)

# 1) remove the sun: inside a soft disc around the peak, replace with a blurred, clamped copy of the surrounding sky
a = rd('clear_bg_cube.hdr'); L = a.max(2); h, w = L.shape
y, x = np.unravel_index(L.argmax(), L.shape)
yy, xx = np.mgrid[0:h, 0:w]; dx = np.minimum(abs(xx - x), w - abs(xx - x)); r = np.hypot(dx, yy - y)
# the HDRI sun has a wide forward-scatter halo (~2x the sky out to 40 deg), so remove the halo too: blend each row toward that
# row's sky colour from the half of the panorama facing away from the sun, weighted by distance from the sun
away = dx > w // 4
base = np.stack([np.median(a[i][away[i]], axis=0) for i in range(h)])[:, None, :]
# never darken: clamp to the away-sky row colour (x1.12) only where brighter, feathered over the sun-facing half
m = np.clip(1.3 - dx / (w * 0.25), 0, 1)[..., None]
cap = np.broadcast_to(base * 1.12, a.shape)
b = a * (1 - m) + np.minimum(a, cap) * m
b = cv2.GaussianBlur(b, (0, 0), 2) * m + b * (1 - m)
wr('clear_bg_nosun.hdr', b)

# 2) soft-cap the sunset: luminance knee at 2.5 -> asymptote 5 (the disc and its glow stop driving bloom)
a = rd('sunset_bg_cube.hdr'); L = a.max(2, keepdims=True) + 1e-6
k, c = 1.2, 2.2
Lc = np.where(L > k, k + (c - k) * np.tanh((L - k) / (c - k)), L)
wr('sunset_bg_cap.hdr', a * (Lc / L))

# 3) the water's reflection cube for Tall Pine gets the same cap (its uncapped sun was the long glare streak on the water)
a = rd('sunset_reflection_cube.hdr'); L = a.max(2, keepdims=True) + 1e-6
Lc = np.where(L > k, k + (c - k) * np.tanh((L - k) / (c - k)), L)
wr('sunset_reflection_cap.hdr', a * (Lc / L))
