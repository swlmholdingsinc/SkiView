#!/usr/bin/env python3
"""Pass 7 rider close-up / camera sheet: tournament broadcast frames (copyrighted, STUDY ONLY, left) next to the matching
in-game shots (right). Output is git-ignored (Screens/rider_compare*.png); the footage never enters the project."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
R = '/workspace/waterski-game/reference/twbc/frames/slalom_2020swisspro_women_final/'
S = os.path.join(os.path.dirname(__file__), '..', 'Screens') + '/'
out = sys.argv[1] if len(sys.argv) > 1 else 'rider_compare_closeup_v7.png'
pairs = [
    (R + 'f_043.jpg', '04_rider_closeup.png', 'A  skier close-up: wet skin/hair sheen, wet vest, spray on the legs, rope'),
    (R + 'f_043.jpg', '04b_rider_face.png', 'B  face + hair check (natural, not plastic)'),
    (R + 'f_011.jpg', '15_broadcast_chase.png', 'C  broadcast boat camera: low in the boat just ahead of the pylon top, looking straight back down the rope'),
]
CW, CH, PAD, HDR = 800, 450, 14, 34
try:
    font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 18)
    big = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 24)
except Exception:
    font = big = ImageFont.load_default()

def fit(im):
    w, h = im.size; t = 16 / 9
    if w / h > t: nw = int(h * t); im = im.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
    else: nh = int(w / t); im = im.crop((0, (h - nh) // 2, w, (h - nh) // 2 + nh))
    return im.resize((CW, CH), Image.LANCZOS)

rows = [p for p in pairs if os.path.exists(p[0]) and os.path.exists(S + p[1])]
W = PAD * 3 + CW * 2; H = 60 + len(rows) * (CH + HDR + PAD) + 40
sheet = Image.new('RGB', (W, H), (24, 26, 28)); d = ImageDraw.Draw(sheet)
d.text((PAD, 16), 'SKI VIEW N1 - rider close-up + camera (' + out.replace('rider_compare_closeup_','').replace('.png','') + ')   |   left: tournament footage (STUDY ONLY)   right: in-game', fill=(235, 235, 235), font=big)
y = 60
for a, b, cap in rows:
    d.text((PAD, y + 6), cap, fill=(210, 214, 220), font=font); y += HDR
    sheet.paste(fit(Image.open(a).convert('RGB')), (PAD, y)); sheet.paste(fit(Image.open(S + b).convert('RGB')), (PAD * 2 + CW, y))
    d.text((PAD + 8, y + 8), 'REFERENCE (study only)', fill=(255, 255, 255), font=font)
    d.text((PAD * 2 + CW + 8, y + 8), 'IN-GAME', fill=(255, 255, 255), font=font)
    y += CH + PAD
d.text((PAD, H - 30), 'Broadcast frames are copyrighted study material; never imported into the game. Rider is a stock Mixamo character.', fill=(160, 165, 170), font=font)
sheet.save(S + out)
print('wrote', S + out, sheet.size, len(rows), 'pairs')
