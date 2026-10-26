#!/usr/bin/env python3
"""Pass 13/14: jump contact sheet. In-game frames (approach, on the ramp, airborne, landing, crash) and the ramp close-up
beside study-only reference frames. The reference paths are read from a local file outside the repo
(/workspace/skiview-assets-staging/jump_refs.txt, keys ramp_side / ramp_surface); the sheet itself is not committed."""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Screens')
out = sys.argv[1] if len(sys.argv) > 1 else 'jump_v14.png'
ver = os.path.splitext(out)[0].rsplit('_', 1)[-1]
refs = {}
cfg = '/workspace/skiview-assets-staging/jump_refs.txt'
if os.path.exists(cfg):
    for line in open(cfg):
        if '=' in line:
            k, v = line.strip().split('=', 1); refs[k] = v
G = lambda n: os.path.join(S, n + '.png')
samples = []
sp = os.path.join(S, 'jump_samples_v14.json')
if os.path.exists(sp):
    samples = {r['name']: r for r in json.load(open(sp))}
def samp(name, label):
    r = samples.get(name) if samples else None
    if not r or 'dist' not in r: return label + ' (no data)'
    pop = 'after the lip' if r['pop'] > 1 else f"at {r['pop']:.2f} of the ramp"
    return f"{label}: cut {-r['cut']:.1f} s out, pop {pop} -> {r['toeSpeed']:.1f} m/s, {r['cross']:.0f} deg, {r['dist']:.1f} m / {round(r['dist'] / 0.3048)} ft, {r['result'].lower()}"
cells = [(G('jump_1_approach'), 'Approach: boat camera, far-side turn before the cut (23 m line, 54 km/h)'),
         (G('jump_3_on_ramp'), 'On the ramp: compressed, crossing the red waxed surface on the diagonal'),
         (G('jump_ramp_surface_low'), 'In-game ramp: red wax, worn ski track, seams/fasteners, running water film'),
         (refs.get('ramp_surface', ''), 'REFERENCE (study only, not in the game): skier view of a wet ramp surface'),
         (G('jump_ramp_course_side'), 'In-game ramp from the side: gelcoat aprons, alu frame, waterline band, floats'),
         (refs.get('ramp_side', ''), 'REFERENCE (study only, not in the game): jump approach with the ramp beyond'),
         (G('jump_5_airborne'), 'Airborne: playable shore jump camera (kicks in at the toe)'),
         (G('jump_5b_side'), 'Flight side view: body folded forward over the skis, tips up, handle in'),
         (G('jump_6_landing'), 'Landing on the jump camera: tips up ~7 deg, soft mist + droplet spray'),
         (G('jump_7b_boatcam'), 'After the ride-out: back on the boat camera, rope tight behind the boat'),
         (G('jump_s_early'), samp('early', 'EARLY cut + early pop')),
         (G('jump_s_late_cut'), samp('late_cut', 'LATE hard cut')),
         (G('jump_5_airborne'), samp('ideal', 'IDEAL timing')),
         (G('jump_8_crash'), samp('late_pop', 'LATE pop (after the lip)'))]
W, H, PAD, CAP, TOP = 800, 450, 14, 26, 46
cols = 2; rows = (len(cells) + cols - 1) // cols
sheet = Image.new('RGB', (cols * W + (cols + 1) * PAD, TOP + rows * (H + CAP + PAD) + 30), (24, 26, 30))
d = ImageDraw.Draw(sheet)
try:
    f = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 17); fb = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 22)
except OSError:
    f = fb = ImageFont.load_default()
d.text((PAD, 12), f'SKI VIEW N1 - jump ({ver})   |   in-game frames (Unity URP) unless marked REFERENCE', font=fb, fill=(235, 235, 235))
for i, (p, cap) in enumerate(cells):
    x = PAD + (i % cols) * (W + PAD); y = TOP + (i // cols) * (H + CAP + PAD)
    ref = cap.startswith('REFERENCE')
    d.text((x, y), cap if len(cap) < 92 else cap[:90] + '..', font=f, fill=(255, 200, 120) if ref else (210, 210, 210))
    if p and os.path.exists(p):
        im = Image.open(p).convert('RGB'); r = min(W / im.width, H / im.height)
        im = im.resize((int(im.width * r), int(im.height * r)), Image.LANCZOS)
        sheet.paste(im, (x + (W - im.width) // 2, y + CAP + (H - im.height) // 2))
        if ref: d.rectangle([x, y + CAP, x + W - 1, y + CAP + H - 1], outline=(255, 170, 60), width=3)
    else:
        d.text((x + 20, y + CAP + 20), '(missing)', font=f, fill=(160, 90, 90))
d.text((PAD, sheet.height - 24), 'Reference frames are for study only and are never imported into the project. Rider: stock Mixamo Remy; ramp, skis and helmet are generated.', font=f, fill=(150, 150, 150))
sheet.save(os.path.join(S, out))
print('wrote', os.path.join(S, out), sheet.size)
