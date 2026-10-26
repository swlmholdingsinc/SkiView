#!/usr/bin/env python3
"""Pass 19 sheets: cameras_v19, turnaround_v19, lake_<id>_v19 (with a top-down course overlay from course_<id>_v19.json),
cam19_first_jump, ui_v19 (from Screens/ui19_*.png), perf_tiers_v19.png (from perf_tiers_v19.txt).
Usage: make_v19_sheets.py [cameras|turnaround|lakes|jump|ui|perf ...]   (default: all). Output in Screens/."""
import sys, os, json, re, glob
from PIL import Image, ImageDraw, ImageFont
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'); S = os.path.join(R, 'Screens')
def font(b, n):
    try: return ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans%s.ttf' % ('-Bold' if b else ''), n)
    except OSError: return ImageFont.load_default()
F, FS, FB, FT = font(0, 17), font(0, 14), font(1, 22), font(1, 30)
BG = (18, 20, 24)
def fit(p, w, h):
    if not p or not os.path.exists(p):
        im = Image.new('RGB', (w, h), (40, 40, 40)); ImageDraw.Draw(im).text((16, h // 2), 'missing: ' + os.path.basename(p or '?'), font=F, fill=(220, 120, 120)); return im
    im = Image.open(p).convert('RGB'); r = max(w / im.width, h / im.height)
    im = im.resize((int(im.width * r + .5), int(im.height * r + .5)), Image.LANCZOS); x = (im.width - w) // 2; y = (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))
def grid(title, sub, rows, cols, w, h, out, pad=12, cap=26):
    W = pad + cols * (w + pad); H = 90 + sum(30 + h + cap + pad for _ in rows)
    sh = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(sh); d.text((pad, 14), title, font=FT, fill=(255, 255, 255)); d.text((pad, 54), sub, font=F, fill=(190, 190, 190))
    y = 90
    for rt, cells in rows:
        d.text((pad, y + 4), rt, font=FB, fill=(255, 210, 110)); y += 30
        for i, (p, c) in enumerate(cells):
            x = pad + i * (w + pad); sh.paste(fit(p, w, h), (x, y)); d.text((x + 2, y + h + 4), c, font=FS, fill=(220, 220, 220))
        y += h + cap + pad
    sh.save(os.path.join(S, out), optimize=True); print(out, sh.size, os.path.getsize(os.path.join(S, out)))

def cameras():
    v = [('high', 'HIGH CHASE (default)'), ('close', 'CLOSE CHASE'), ('first', '1ST PERSON'), ('boat', 'BOAT')]
    rows = [(t, [(f'{S}/cam19_{k}_mid.png', 'mid-pass, wake crossing'), (f'{S}/cam19_{k}_buoy.png', 'at buoy 4')]) for k, t in v]
    rows.append(('1ST PERSON extras', [(f'{S}/cam19_first_cut.png', 'hard cut (lean -61)'), (f'{S}/cam19_first_wake.png', 'over the wake')]))
    grid('cameras_v19 · Ski View home lake (pass 19 build, real frames)', 'CAM / C / RB cycles HIGH -> CLOSE -> 1ST -> BOAT. Same two instants for every view.', rows, 2, 780, 439, 'cameras_v19.png')
def turnaround():
    cols = [('1_sink', 'sink'), ('1b_spin', 'spin (spin lakes)'), ('2_dws', 'DWS spray'), ('3_island', 'island / spin'), ('4_setup', 'set-up'), ('3b_island_boat', 'boat cam'), ('2b_dws_first', '1st person DWS')]
    lakes = sorted({re.match(r'turn19_([a-z]+)_', os.path.basename(p)).group(1) for p in glob.glob(f'{S}/turn19_*.png')}, key=lambda x: (x != 'skiview', x))
    rows = [(l, [(f'{S}/turn19_{l}_{k}.png', c) for k, c in cols]) for l in lakes]
    grid('turnaround_v19 · end-of-pass sequence per lake', 'sink -> deep-water start -> island turn (spin-turn lakes: SPIN TURN -> SET UP · <rope> OFF) -> set-up; TAP / SPACE skips to set-up', rows, 7, 400, 225, 'turnaround_v19.png')
def course_overlay(lid):
    img, js = f'{S}/course_{lid}_top.png', f'{S}/course_{lid}_v19.json'
    if not (os.path.exists(img) and os.path.exists(js)): return None, []
    im = Image.open(img).convert('RGB'); txt = open(js).read(); txt = txt.replace('"vy":F5', '"vy":null')
    try: J = json.loads(txt)
    except Exception: J = json.loads(re.sub(r'null\}?,', 'null},', txt))
    W, H = im.size; o = J.get('orthoSize', 112); hw = o * W / H
    d = ImageDraw.Draw(im, 'RGBA'); notes = []
    def px(b):
        vx = b.get('vx'); vy = b.get('vy')
        if vy is None: vx = 0.5 + b['along'] / (2 * hw); vy = 0.5 + b['lat'] / (2 * o)
        return vx * W, (1 - vy) * H
    # spec course (IWWF): turn balls at along -129.5+27+41i, lat +-11.5; gates at +-1.15 at along +-129.5 (+-1.25 m tol drawn)
    m2p = H / (2 * o)
    for i in range(6):
        a = -129.5 + 27 + 41 * i; l = 11.5 * (1 if i % 2 == 0 else -1)
        for b in J['balls']:
            if b['n'] == f'Turn{i+1}': l = 11.5 * (1 if b['lat'] > 0 else -1)
        cx, cy = W / 2 + a * m2p, H / 2 + l * m2p; r = 1.0 * m2p
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(0, 255, 120, 255), width=2)
    d.line((W / 2 - 129.5 * m2p, H / 2, W / 2 + 129.5 * m2p, H / 2), fill=(255, 255, 255, 110), width=1)
    pts = []
    for b in J['balls']:
        x, y = px(b); n = b['n']; col = (255, 140, 0, 255) if n.startswith('Turn') else (255, 230, 0, 255) if 'Gate' in n else (120, 200, 255, 255)
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=col, outline=(0, 0, 0, 255))
        if n.startswith('Turn'):
            i = int(n[4:]) - 1; ea = -129.5 + 27 + 41 * i; dA = b['along'] - ea; dL = abs(b['lat']) - 11.5
            d.text((x + 8, y - 8), f'{n[4:]}  Δa {dA:+.2f}  Δl {dL:+.2f}', font=FS, fill=(255, 255, 255, 255)); pts.append((b['along'], x, y)); notes.append((n, dA, dL))
    pts.sort(); 
    if len(pts) > 1: d.line([(x, y) for _, x, y in pts], fill=(255, 140, 0, 160), width=2)
    d.text((12, 10), f'{lid}: top-down, {2*o:.0f} m tall, course axis left->right (Turn1 -> Turn6). Green rings = spec turn balls (1 m), orange = in-game.', font=F, fill=(255, 255, 255, 255))
    return im, notes
def lakes(ids=None):
    ids = ids or [os.path.basename(p)[12:].split('.')[0] for p in glob.glob(f'{R}/Assets/SkiView/Scenes/SkiViewLake_*.unity')] + ['skiview']
    for lid in sorted(set(ids)):
        G = lambda n: f'{S}/lake_{lid}_{n}.png' if lid != 'skiview' else {'mood': f'{S}/cam19_high_mid.png', 'oblique': f'{S}/cam19_boat_mid.png', 'course_chase': f'{S}/cam19_close_mid.png', 'aerial': f'{S}/turn19_skiview_3_island.png'}.get(n, f'{S}/lake_skiview_{n}.png')
        ov, notes = course_overlay(lid)
        names = ['mood', 'oblique', 'aerial', 'course_chase', 'course_boatcam', 'course_drone', 'bank_plus', 'bank_minus', 'ramp', 'jump_flight', 'boat']
        shots = [(G(n), n) for n in names if os.path.exists(G(n))]
        w, h, pad = 640, 360, 12; cols = 3; nrow = (len(shots) + cols - 1) // cols
        ovh = 0
        if ov is not None: ovw = pad * -1 + cols * (w + pad); ovh = int(ov.height * ovw / ov.width); ov = ov.resize((ovw, ovh), Image.LANCZOS)
        W = pad + cols * (w + pad); H = 90 + (ovh + 40 if ov else 0) + nrow * (h + 30) + 60
        sh = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(sh)
        d.text((pad, 14), f'lake_{lid}_v19 · pass 19 build', font=FT, fill=(255, 255, 255))
        if notes:
            mx = max(max(abs(a), abs(l)) for _, a, l in notes); d.text((pad, 54), f'Course overlay: worst turn-ball offset from spec {mx:.2f} m ({"OK" if mx <= 0.25 else "CHECK"})', font=F, fill=(190, 190, 190))
        else: d.text((pad, 54), 'No course-top capture for this lake', font=F, fill=(230, 140, 140))
        y = 90
        if ov: sh.paste(ov, (pad, y)); y += ovh + 40
        for i, (p, c) in enumerate(shots):
            x = pad + (i % cols) * (w + pad); yy = y + (i // cols) * (h + 30); sh.paste(fit(p, w, h), (x, yy)); d.text((x + 2, yy + h + 4), c, font=FS, fill=(220, 220, 220))
        out = f'lake_{lid}_v19.png'; sh.save(os.path.join(S, out), optimize=True); print(out, sh.size, os.path.getsize(os.path.join(S, out)))
def jump():
    p = f'{S}/cam19_first_jump.png'; im = Image.open(p).convert('RGB'); W = 1600; im = im.resize((W, int(im.height * W / im.width)), Image.LANCZOS)
    sh = Image.new('RGB', (W, im.height + 70), BG); d = ImageDraw.Draw(sh); sh.paste(im, (0, 70))
    d.text((12, 12), 'cam19_first_jump · 1st person off the Ski View ramp (pass 19, ramp lip at along 45.3 m)', font=FB, fill=(255, 255, 255))
    d.text((12, 42), 'off the top at 73 km/h, vertical 5.6 m/s (pop 0.60 at 0.86); handle, ski tips and boat all in frame', font=FS, fill=(200, 200, 200))
    sh.save(f'{S}/cam19_first_jump_v19.png', optimize=True); print('cam19_first_jump_v19.png', sh.size, os.path.getsize(f'{S}/cam19_first_jump_v19.png'))
def ui():
    shots = sorted(glob.glob(f'{S}/ui19_*.png'))
    rows = [('', [(p, os.path.basename(p)[5:-4]) for p in shots[i:i + 3]]) for i in range(0, len(shots), 3)]
    grid('ui_v19 · WebGL build (headless Chrome, ?ui= states)', 'Julia batch 5: lake select 03 final, loading 16, pack 16b-d, tutorial v3, controls help, pause, results, PB, FP HUD', rows, 3, 640, 360, 'ui_v19.png')
def perf():
    L = open(f'{S}/perf_tiers_v19.txt').read().splitlines(); W = 1900; H = 70 + 20 * len(L)
    sh = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(sh); d.text((12, 12), 'perf_tiers_v19 · per lake, tier, camera (editor counts)', font=FB, fill=(255, 255, 255))
    mono = font(0, 13)
    try: mono = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf', 13)
    except OSError: pass
    for i, l in enumerate(L):
        m = re.search(r'tris ~ *(\d+)k', l); t = int(m.group(1)) if m else 0; mob = 'Mobile' in l
        col = (255, 120, 120) if mob and t > 600 else (255, 210, 120) if (not mob and t > 4000) else (215, 215, 215)
        d.text((12, 56 + 20 * i), l[:230], font=mono, fill=col)
    sh.save(f'{S}/perf_tiers_v19.png', optimize=True); print('perf_tiers_v19.png', sh.size, os.path.getsize(f'{S}/perf_tiers_v19.png'))
if __name__ == '__main__':
    what = sys.argv[1:] or ['cameras', 'turnaround', 'lakes', 'jump', 'perf', 'ui']
    for w in what: globals()[w]()
