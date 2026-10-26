#!/usr/bin/env python3
"""Pass 17: player camera sheet - each camera (1st person, close chase, high chase = default, boat) at the same two instants of
one pass: mid-pass at the wake crossing and at a buoy. Usage: make_cameras_sheet.py <out.png>. Not committed (.gitignore)."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
out = sys.argv[1] if len(sys.argv) > 1 else 'cameras_v17.png'
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Screens')
try:
    F = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 17); FB = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 24)
except Exception:
    F = FB = ImageFont.load_default()
W, H, PAD, CAP = 780, 439, 14, 28
def fit(p):
    if not os.path.exists(p):
        im = Image.new('RGB', (W, H), (40, 40, 40)); ImageDraw.Draw(im).text((20, H // 2), 'missing: ' + os.path.basename(p), font=F, fill=(220, 120, 120)); return im
    im = Image.open(p).convert('RGB'); r = max(W / im.width, H / im.height)
    im = im.resize((int(im.width * r + 0.5), int(im.height * r + 0.5)), Image.LANCZOS); x = (im.width - W) // 2; y = (im.height - H) // 2
    return im.crop((x, y, x + W, y + H))
rows = [('first', '1ST PERSON: skier eyes, handle + hands, boat and rope ahead, ski tip; helmet-cam FOV 88, pitch 34; roll = 0.3 x lean, max 9 deg, 0.5 s smoothing'),
        ('close', 'CLOSE CHASE: 5 m back, 1.55 m (shoulder) high, kept >= 3.5 m off the body and 0.6 m above water'),
        ('high', 'HIGH CHASE (default): the existing view behind and above'),
        ('boat', 'BOAT: the existing broadcast camera from the boat')]
TW = PAD + 2 * (W + PAD); total = 100 + len(rows) * (30 + H + CAP + PAD)
sheet = Image.new('RGB', (TW, total), (18, 18, 18)); d = ImageDraw.Draw(sheet)
d.text((PAD, 18), 'Player cameras (%s): CAM button / C key cycles HIGH -> CLOSE -> 1ST -> BOAT, for the current run; Settings > Controls > Default camera sets the start view' % os.path.splitext(out)[0].rsplit('_', 1)[-1], font=FB, fill=(255, 255, 255))
d.text((PAD, 52), 'left: mid-pass at the wake crossing      right: at a buoy (same instants for every camera; 0.7 s blend on switch)', font=F, fill=(200, 200, 200))
y = 100
for k, title in rows:
    d.text((PAD, y + 4), title, font=F, fill=(255, 210, 90)); y += 30
    for i, n in enumerate(('mid', 'buoy')):
        sheet.paste(fit(os.path.join(S, 'cam17_%s_%s.png' % (k, n))), (PAD + i * (W + PAD), y)); d.text((PAD + i * (W + PAD), y + H + 4), 'cam17_%s_%s' % (k, n), font=F, fill=(230, 230, 230))
    y += H + CAP + PAD
sheet.save(os.path.join(S, out)); print('wrote', os.path.join(S, out), sheet.size)
