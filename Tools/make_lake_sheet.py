#!/usr/bin/env python3
"""Pass 15: per-lake comparison sheet (study-only reference beside the in-game lake): aerial, course, ramp, shoreline,
buildings, boat colour. Usage: make_lake_sheet.py <lake_id> <out.png>. Reference paths are read from a local file outside the
repo (/workspace/skiview-assets-staging/lake_refs_<id>.txt); the sheet itself is not committed (.gitignore lake_*_v*.png)."""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
LID = sys.argv[1] if len(sys.argv) > 1 else 'cedarbend'
out = sys.argv[2] if len(sys.argv) > 2 else 'lake_%s_v15.png' % LID
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Screens')
V = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets/SkiView/Lakes/%s/venue.json' % LID)))
refs = {}
cfg = '/workspace/skiview-assets-staging/lake_refs_%s.txt' % LID
if os.path.exists(cfg):
    for line in open(cfg):
        if '=' in line: k, v = line.strip().split('=', 1); refs[k] = v
G = lambda n: os.path.join(S, 'lake_%s_%s.png' % (LID, n))
try:
    F = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 17); FB = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 24)
except Exception:
    F = FB = ImageFont.load_default()
W, H, PAD, CAP = 780, 470, 14, 28
def fit(p, w=W, h=H, box=None):
    if not p or not os.path.exists(p):
        im = Image.new('RGB', (w, h), (40, 40, 40)); ImageDraw.Draw(im).text((20, h // 2), 'missing: ' + os.path.basename(p or '?'), font=F, fill=(220, 120, 120)); return im
    im = Image.open(p).convert('RGB')
    if box: im = im.crop(box)
    r = max(w / im.width, h / im.height); im = im.resize((int(im.width * r + 0.5), int(im.height * r + 0.5)), Image.LANCZOS)
    x = (im.width - w) // 2; y = (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))
def text_panel(lines, w=W, h=H):
    im = Image.new('RGB', (w, h), (28, 32, 36)); d = ImageDraw.Draw(im); y = 24
    for ln in lines: d.text((22, y), ln, font=F, fill=(230, 230, 230)); y += 28
    return im
# aerial crops: same ground window in both (Cedar Bend: x -290..310 m east, z = 85 +- 356.5 m north; Saltwhite: centred on
# the shore bbox, which is where the in-game aerial camera sits)
CAMX, CAMZ = 10.0, 85.0
X0, X1, ZC, HZ = -290.0, 310.0, 85.0, 356.5
AW, AH = 600, 713
if LID == 'saltwhite':
    CAMX, CAMZ = V['aerialCentre']; X0, X1, ZC, HZ = CAMX - 320, CAMX + 320, CAMZ, 290.0; AW, AH = 780, 707
def ref_aerial():
    p = refs.get('aerial'); s = float(refs.get('aerial_scale', 0.625)); oe = float(refs.get('aerial_origin_east', 0)); on = float(refs.get('aerial_origin_north', 0))
    if not p or not os.path.exists(p): return fit(None, AW, AH)
    im = Image.open(p); cx = im.width / 2 + oe / s; cy = im.height / 2 - on / s
    return fit(p, AW, AH, (int(cx + X0 / s), int(cy - (ZC + HZ) / s), int(cx + X1 / s), int(cy - (ZC - HZ) / s)))
def game_aerial():
    p = G('aerial')
    if not os.path.exists(p): return fit(None, AW, AH)
    im = Image.open(p); k = im.width / (2 * 980 * 0.36397 * im.width / im.height)   # px per metre (fov 40 vertical)
    cxp = im.width / 2 - CAMX * k; cyp = im.height / 2 + CAMZ * k
    return fit(p, AW, AH, (int(cxp + X0 * k), int(cyp - (ZC + HZ) * k), int(cxp + X1 * k), int(cyp - (ZC - HZ) * k)))
rows = []
if LID == 'cedarbend':
  rows.append(('AERIAL (north up, same 600 x 713 m window)', [(ref_aerial(), 'REFERENCE aerial (study only, never traced into textures)'), (game_aerial(), 'IN-GAME: v2 shoreline, islands, slip, narrow west lawn strip, road, firs')], 713, 600))
  rows.append(('OBLIQUE / COURSE', [(fit(refs.get('oblique')), 'REFERENCE listing drone photo (study only)'), (fit(G('oblique')), 'IN-GAME oblique from the SSW end')], H, W))
  rows.append(('', [(fit(refs.get('course')), 'REFERENCE: low drone along the lake (study only)'), (fit(G('course_drone')), 'IN-GAME: course from over the south pre-gate (buoys, gates)')], H, W))
  rows.append(('', [(fit(G('course_boatcam')), 'IN-GAME boat camera on the pass (red/black/white boat)'),
                    (text_panel(['RAMP: none at %s.' % V['lake']['name'],
                                 'LAKE-LAYOUTS 1: the real lake is slalom only and',
                                 'lakes.ts marks it slalom only, so no ramp is spawned;',
                                 'the JUMP button is hidden on this lake.',
                                 'Course: 259 m gate to gate, 6 balls at 11.5 m,',
                                 'bearing 24.2 deg (SSW -> NNE), Holly v2 re-trace:',
                                 '662 m long, 61-94 m wide (median 68 m).',
                                 'Loop round the north island; teardrop (R 34 m)',
                                 'in the south bowl, south island as dressing.',
                                 'Boat path >= 9.4 m to shore/island; skier',
                                 'envelope (+-18 m) >= 9.5 m to shore.']), 'RAMP / COURSE')], H, W))
  rows.append(('SHORELINE', [(fit(refs.get('shore_a')), 'REFERENCE: mown lawn to the water, fir wall (study only)'), (fit(G('shore_west_strip')), 'IN-GAME: WNW lawn strip (pass 17: 9-14 m, road 18 m back)')], H, W))
  rows.append(('', [(fit(refs.get('shore_b')), 'REFERENCE: grass island, fir/cedar wall (study only)'), (fit(G('shore_east_lawn')), 'IN-GAME: from the ESE fairway across to the tree row')], H, W))
  rows.append(('BUILDINGS', [(fit(refs.get('moorage')), 'REFERENCE: covered moorage (study only)'), (fit(G('moorage')), 'IN-GAME: timber dock, shed-roof boat cover + lift in the slip')], H, W))
  rows.append(('', [(fit(refs.get('farmhouse')), 'REFERENCE: lawn, white farmhouse (study only)'), (fit(G('farmhouse')), 'IN-GAME: white two-storey farmhouse, dark shingle roof')], H, W))
  if os.path.exists(G('farmhouse_close')):   # pass 16 detail shots
      rows.append(('BUILDING DETAIL', [(fit(G('farmhouse_close')), 'IN-GAME: trim, porch posts/rails, deep windows, gutters, chimney'), (fit(G('barn')), 'IN-GAME: barn in a mown yard (pass 17: no ferns), trim, louvres')], H, W))
  if os.path.exists(G('backdrop')):
      rows.append(('FOOTHILLS / BACKDROP', [(fit(refs.get('oblique')), 'REFERENCE (study only)'), (fit(G('backdrop')), 'IN-GAME: 5 hazed foothill / Cascade ridge layers 1.1-6.2 km')], H, W))
if LID == 'saltwhite':
  rp = V.get('ramp') or {}
  rows.append(('AERIAL (north up, same %d x %d m window)' % (X1 - X0, 2 * HZ), [(ref_aerial(), 'REFERENCE aerial (study only, never traced into textures)'), (game_aerial(), 'IN-GAME: Holly layout + 15 m SW widening, islands, estate ring')], AH, AW))
  rows.append(('OBLIQUE / COURSE', [(fit(G('oblique')), 'IN-GAME oblique from beyond the SE end'), (fit(G('course_drone')), 'IN-GAME: course from over the SE pre-gate (buoys, gates)')], H, W))
  rows.append(('', [(fit(G('course_boatcam')), 'IN-GAME boat camera on the pass'),
                    (text_panel(['%s: Gulf-coast estate lake (fictional).' % V['lake']['name'],
                                 'Layout: Holly (saltwhite_alt_layout_m) with the SW',
                                 'bank widened ~15 m (shore_build_widened15); end',
                                 'bulbs taken from the aerial water mask.',
                                 'Course 259 m, 6 balls; boat loops round both',
                                 'islands (loop 1182 m); boat >= 8.4 m to shore.',
                                 'Skier envelope (+-18 m) >= 6.6 m (NW reach).',
                                 'RAMP: SW bank, %.0f m off the line, run NW -> SE;' % V.get('rampFromSlalom', 22),
                                 'ramp 30.9 m from shore, landing line >= 24.7 m.']), 'RAMP / COURSE')], H, W))
  rows.append(('RAMP / JUMP', [(fit(G('ramp')), 'IN-GAME: ramp on the widened SW side (height %.2f m)' % rp.get('height', 0)), (fit(G('jump_flight')), 'IN-GAME: jump run NW -> SE, mid-flight')], H, W))
  rows.append(('SHORELINE', [(fit(G('shore_sw_sand')), 'IN-GAME: SW bank, white sand rim, lawn, palms'), (fit(G('shore_ne')), 'IN-GAME: NE bank looking across to the ramp side')], H, W))
  rows.append(('ESTATE HOMES', [(fit(G('estate')), 'IN-GAME: stucco two-storey, hip tile roof, lanai'), (fit(G('estate_close')), 'IN-GAME: pool deck and screened pool cage')], H, W))
  rows.append(('DOCKS / DRESSING', [(fit(G('commdock')), 'IN-GAME: community dock, boat lift + cover'), (fit(G('dressing')), 'IN-GAME: judges tower, tents, scoreboard (no text)')], H, W))
  rows.append(('ISLAND / BACKDROP', [(fit(G('island')), 'IN-GAME: SE island (sabal + palmetto)'), (fit(G('backdrop')), 'IN-GAME: flatwoods treelines 0.9-5.2 km, haze')], H, W))
if LID not in ('cedarbend', 'saltwhite'):
  # pass 18: lakes built from Holly's v2 layout files: her layout diagram (left, design doc) vs the in-game stills
  HD = '/workspace/waterski-game/roadmap/venue/lakes/%s_layout_diagram_v2.jpg' % LID
  rp = V.get('ramp') or {}
  ramp_txt = ('RAMP: none (slalom only, jump off by default)' if not rp else
              'RAMP: %.1f m off the line, jumpDir %+d%s' % (V.get('rampFromSlalom', 0), V.get('jumpDir', -1), ', JUMP MODE ONLY' if V.get('jumpOnly') else ''))
  rows.append(('LAYOUT / AERIAL', [(fit(HD), "Holly's v2 layout diagram (design doc, read-only)"), (fit(G('aerial')), 'IN-GAME aerial, north up')], H, W))
  rows.append(('OBLIQUE / COURSE', [(fit(G('oblique')), 'IN-GAME oblique'), (fit(G('course_drone')), 'IN-GAME course from over the pre-gate')], H, W))
  if not V.get('jumpOnly'):
    rows.append(('', [(fit(G('course_boatcam')), 'IN-GAME boat camera on the pass'), (fit(G('course_chase')), 'IN-GAME close chase on the pass')], H, W))
  rows.append(('BANKS', [(fit(G('bank_plus')), 'IN-GAME bank from the water (eye height)'), (fit(G('bank_minus')), 'IN-GAME opposite bank')], H, W))
  bl = [n for n in ('bldg_estate', 'bldg_boathouse', 'bldg_farmhouse', 'bldg_pier') if os.path.exists(G(n))]
  cells = [(fit(G(n)), 'IN-GAME ' + n.replace('bldg_', '')) for n in bl[:2]]
  if len(cells) < 2: cells.append((fit(G('mood')), 'IN-GAME mood shot down the lane'))
  rows.append(('BUILDINGS', cells, H, W))
  if len(bl) > 2: rows.append(('', [(fit(G(bl[2])), 'IN-GAME ' + bl[2].replace('bldg_', '')), (fit(G('mood')), 'IN-GAME mood shot down the lane')], H, W))
  elif len(bl) == 2: rows.append(('MOOD', [(fit(G('mood')), 'IN-GAME mood shot down the lane'), (text_panel([V['lake']['name'], ramp_txt, 'turns: see Docs/N1-PROGRESS.md pass 18']), 'NOTES')], H, W))
  if rp: rows.append(('RAMP / JUMP', [(fit(G('ramp')), 'IN-GAME ramp (%s)' % ramp_txt.split(': ', 1)[1]), (fit(G('jump_flight')), 'IN-GAME mid-flight')], H, W))
b = V['lake']['boat']
sw = Image.new('RGB', (W, H), (235, 235, 232)); d = ImageDraw.Draw(sw)
for i, (k, lab) in enumerate((('band', 'band (hull stripe)'), ('accent', 'accent'), ('deck', 'deck / hull'))):
    d.rectangle([40, 50 + i * 130, 260, 150 + i * 130], fill=b[k], outline=(0, 0, 0)); d.text((290, 85 + i * 130), '%s  %s' % (lab, b[k]), font=FB, fill=(20, 20, 20))
rows.append(('BOAT COLOUR', [(sw, 'boat_palettes.json (Christopher): %s' % LID), (fit(G('boat')), 'IN-GAME boat at %s (decal: SWLM only)' % V['lake']['name'])], H, W))
if LID == 'cedarbend': rows.append(('', [(fit(G('north_end')), 'IN-GAME: north end, island and the blue-roof shop'), (fit(G('aerial')), 'IN-GAME aerial, full frame (canopy cover)')], H, W))
TW = PAD + 2 * (max(W, AW) + PAD)
total = 70 + sum(h + CAP + PAD + (30 if t else 0) for t, c, h, w in rows)
sheet = Image.new('RGB', (TW, total), (18, 18, 18)); d = ImageDraw.Draw(sheet)
d.text((PAD, 18), '%s (%s) %s: study ref (left) vs in-game (right); refs are study only, never shipped.' % (V['lake']['name'], LID, os.path.splitext(os.path.basename(out))[0].rsplit('_', 1)[-1]), font=FB, fill=(255, 255, 255))
y = 70
for title, cells, h, w in rows:
    if title: d.text((PAD, y + 4), title, font=FB, fill=(255, 210, 90)); y += 30
    for i, (im, cap) in enumerate(cells):
        x = PAD + i * (W + PAD) + (W - im.width) // 2
        sheet.paste(im, (x, y)); d.text((PAD + i * (W + PAD), y + h + 4), cap, font=F, fill=(230, 230, 230))
    y += h + CAP + PAD
sheet.save(os.path.join(S, out)); print('wrote', os.path.join(S, out), sheet.size)
