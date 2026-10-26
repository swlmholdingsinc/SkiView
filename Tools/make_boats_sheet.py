#!/usr/bin/env python3
"""Pass 15: boats sheet. The same boat in every lake palette (Christopher's boat_palettes.json, 'order'), rendered in-game
(Screens/boat15_<id>.png), labelled with the fictional lake name and the three gelcoat colours. Usage: make_boats_sheet.py out.png"""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
S = os.path.join(R, 'Screens'); out = sys.argv[1] if len(sys.argv) > 1 else 'boats_v15.png'
P = json.load(open(os.path.join(R, 'Assets/SkiView/Boat/boat_palettes.json')))
NAMES = {'skiview': 'Ski View (home)', 'cedarbend': 'Cedar Bend', 'heronmarsh': 'Heron Marsh', 'villaserena': 'Villa Serena', 'saltwhite': 'Saltwhite',
         'tallpine': 'Tall Pine', 'harrowpond': 'Harrow Pond', 'gulfwind': 'Gulfwind', 'saguaro': 'Saguaro', 'marrowind': 'Marrowind'}
BUILT = {'skiview', 'cedarbend'}
try:
    F = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 16); FB = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 20)
except Exception:
    F = FB = ImageFont.load_default()
W, H, PAD, CAP = 620, 349, 12, 62
ids = list(P['order']) + ['home_skiview']
cols = 3; rows = (len(ids) + cols - 1) // cols
sheet = Image.new('RGB', (PAD + cols * (W + PAD), 60 + rows * (H + CAP + PAD)), (20, 20, 20)); d = ImageDraw.Draw(sheet)
d.text((PAD, 16), 'boats_v15: one boat, per-lake gelcoat (boat_palettes.json), rendered in Cedar Bend overcast light; last = home boat at Ski View', font=FB, fill=(255, 255, 255))
for i, lid in enumerate(ids):
    x = PAD + (i % cols) * (W + PAD); y = 60 + (i // cols) * (H + CAP + PAD)
    p = os.path.join(S, 'boat15_%s.png' % lid)
    if os.path.exists(p):
        im = Image.open(p).convert('RGB'); r = max(W / im.width, H / im.height); im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
        cx, cy = (im.width - W) // 2, (im.height - H) // 2; sheet.paste(im.crop((cx, cy, cx + W, cy + H)), (x, y))
    key = 'skiview' if lid == 'home_skiview' else lid; e = P['lakes'][key]
    name = NAMES.get(key, key) + (' - in its own lake' if lid == 'home_skiview' else (' (lake built)' if key in BUILT else ''))
    d.text((x, y + H + 4), name, font=FB, fill=(255, 220, 120))
    for j, k in enumerate(('band', 'accent', 'deck')):
        d.rectangle([x + j * 200, y + H + 32, x + j * 200 + 22, y + H + 52], fill=e[k], outline=(200, 200, 200)); d.text((x + j * 200 + 28, y + H + 33), '%s %s' % (k, e[k]), font=F, fill=(230, 230, 230))
sheet.save(os.path.join(S, out)); print('wrote', os.path.join(S, out), sheet.size)
