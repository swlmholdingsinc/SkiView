#!/usr/bin/env python3
"""Environment realism v5 comparison sheet: Scott's real lake photos (study-only reference, left) next to matching
in-game angles (right). Output is git-ignored (Screens/env_compare*.png) - reference imagery never enters the repo."""
import sys, os, glob
from PIL import Image, ImageDraw, ImageFont
R = '/workspace/waterski-game/reference/'
S = os.path.join(os.path.dirname(__file__), '..', 'Screens') + '/'
out = sys.argv[1] if len(sys.argv) > 1 else 'env_compare_v5.png'
pairs = [
    (R + 'ski-nautique/scott/nautique-01.jpg', 'env_a_boat_low.png', 'A  calm glassy lake, boat + mirror reflection, cumulus'),
    (R + 'ski-view-houses/8017/listing-2026/p01.jpg', 'env_b_lawn_down_lake.png', 'B  from the lawn, down the lake (late-summer grass, fir line)'),
    (R + 'ski-view-houses/8017/listing-2026/p14.jpg', 'env_c_dock.png', 'C  low plank dock, U-slip with green lift canopy'),
    (R + '8505-ski-view/frames_lake/f_013.jpg', 'env_d_houses_across.png', 'D  across the water to the east-bank houses (Christopher\'s lot FBXs)'),
    (R + '8505-ski-view/frames_lake/f_015.jpg', 'env_f_shoreline.png', 'E  lawn to waterline edge (bank lip, wet band)'),
    (next(iter(sorted(glob.glob(R + 'ski-view-houses/8017/*-lake-house-concept.jpg'))), 'missing'), 'env_h_hero_8017_lake.png', 'H  hero lot 8017 from the lake: walkout, terraces down to the dock (concept image, text cropped)'),
    ('S:env_i_hero_8017_terraces.png', 'env_j_houses_row.png', 'I  in-game only: 8017 terraces + dock (left) | row of lot FBXs across the water (right)'),
]
CROP = {'concept': (0.22, 0.14, 1.0, 1.0)}   # drop the concept board's text column
CW, CH, PAD, HDR = 800, 450, 14, 34
try:
    font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 18)
    big = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 24)
except Exception:
    font = big = ImageFont.load_default()

def fit(im):
    # centre-crop to 16:9 then resize
    w, h = im.size; t = 16 / 9
    if w / h > t: nw = int(h * t); im = im.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
    else: nh = int(w / t); im = im.crop((0, (h - nh) // 2, w, (h - nh) // 2 + nh))
    return im.resize((CW, CH), Image.LANCZOS)

def src(a): return S + a[2:] if a.startswith('S:') else a
rows = [(a, b, c) for a, b, c in pairs if os.path.exists(src(a)) and os.path.exists(S + b)]
W = PAD * 3 + CW * 2; H = 60 + len(rows) * (CH + HDR + PAD) + 40
sheet = Image.new('RGB', (W, H), (24, 26, 28)); d = ImageDraw.Draw(sheet)
d.text((PAD, 16), 'SKI VIEW N1 - environment realism ' + out.replace('env_compare_','').replace('.png','') + '   |   left: real lake photo (STUDY ONLY, not in project)   right: in-game (Unity URP)', fill=(235, 235, 235), font=big)
y = 60
for a, b, cap in rows:
    d.text((PAD, y + 6), cap, fill=(210, 214, 220), font=font)
    y += HDR
    im = Image.open(src(a)).convert('RGB')
    for k, (x0, y0, x1, y1) in CROP.items():
        if k in a: w, h = im.size; im = im.crop((int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h)))
    sheet.paste(fit(im), (PAD, y))
    sheet.paste(fit(Image.open(S + b).convert('RGB')), (PAD * 2 + CW, y))
    d.text((PAD + 8, y + 8), 'IN-GAME' if a.startswith('S:') else 'REFERENCE (study only)', fill=(255, 255, 255), font=font)
    d.text((PAD * 2 + CW + 8, y + 8), 'IN-GAME', fill=(255, 255, 255), font=font)
    y += CH + PAD
d.text((PAD, H - 30), 'Reference photos are Scott\'s / listing study material; not imported into the game. Houses: Christopher\'s 18 lot FBXs, v2 detail pass (pass 8).', fill=(160, 165, 170), font=font)
sheet.save(S + out)
print('wrote', S + out, sheet.size, len(rows), 'pairs')
