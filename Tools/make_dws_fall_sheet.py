#!/usr/bin/env python3
"""Pass 11: in-game contact sheet for the fall -> treading water -> deep-water start sequence (no reference media)."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Screens')
out = sys.argv[1] if len(sys.argv) > 1 else 'dws_fall_v11.png'
ver = os.path.splitext(out)[0].rsplit('_', 1)[-1]
shots = [('fall_1_catch', 'Fall 1: catches an edge, goes over the front (Falling Forward Death)'),
         ('fall_2_into_water', 'Fall 2: into the water'),
         ('fall_3_treading', 'Fall 3: treading water while the boat comes back (Treading Water)'),
         ('dws_1_in_water', 'Start 1: sitting back, knees up, ski tip out (Pull Heavy Object Start)'),
         ('dws_2_rising', 'Start 2: rope tight, boat throttles up, rising'),
         ('dws_3_almost_up', 'Start 3: coming over the ski'),
         ('dws_4_up', 'Start 4: up, back in the stance (Pull Heavy Object lean base)')]
W, H, PAD, CAP, TOP = 800, 450, 14, 26, 46
cols = 2; rows = (len(shots) + cols - 1) // cols
sheet = Image.new('RGB', (cols * W + (cols + 1) * PAD, TOP + rows * (H + CAP + PAD) + 30), (24, 26, 30))
d = ImageDraw.Draw(sheet)
try:
    f = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 17); fb = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 22)
except OSError:
    f = fb = ImageFont.load_default()
d.text((PAD, 12), f'SKI VIEW N1 - fall + deep-water start ({ver})   |   all frames in-game (Unity URP)', font=fb, fill=(235, 235, 235))
for i, (n, cap) in enumerate(shots):
    x = PAD + (i % cols) * (W + PAD); y = TOP + (i // cols) * (H + CAP + PAD)
    d.text((x, y), cap, font=f, fill=(210, 210, 210))
    p = os.path.join(S, n + '.png')
    if os.path.exists(p):
        sheet.paste(Image.open(p).convert('RGB').resize((W, H), Image.LANCZOS), (x, y + CAP))
d.text((PAD, sheet.height - 24), 'Rider and clips are stock Mixamo assets (Remy; Pull Heavy Object, Falling Forward Death, Treading Water).', font=f, fill=(150, 150, 150))
sheet.save(os.path.join(S, out))
print('wrote', os.path.join(S, out), sheet.size)
