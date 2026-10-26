"""Per-lake venue generator (pass 15). Usage: gen_venue_lake.py <lake_id>
Reads Holly's roadmap/venue/lakes/<id>_layout_m.json (frame, course, islands, buildings) + LAKE-LAYOUTS.md decisions
(coded below per lake) and Christopher's boat_palettes.json. Writes Assets/SkiView/Lakes/<id>/: heightmap.r16, splat.png,
venue.json. Ski View (home lake) keeps its own generator (Tools/gen_venue.py) and is not touched by this script.
World frame (Unity): +X east, +Z north, Y up; layout frame +x east, +y south -> unity z = -y. Origin = course centre."""
import json, math, sys, numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
LID = sys.argv[1] if len(sys.argv) > 1 else 'cedarbend'
import os
SH = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'lake_data', LID + '_shore.json')))
# pass 16: Tools/lake_data/<id>_shore.json "layout" picks Holly's layout file (v2 re-trace for Cedar Bend); default v1
SRC = '/workspace/waterski-game/roadmap/venue/lakes/' + SH.get('layout', '%s_layout_m.json' % LID)
OUT = '/workspace/skiview-unity/Assets/SkiView/Lakes/%s/' % LID
PAL = '/workspace/skiview-unity/Assets/SkiView/Boat/boat_palettes.json'
d = json.load(open(SRC))
rng = np.random.default_rng(1500 + sum(map(ord, LID)))
F = np.array(d['course']['dir_unit']) * [1, -1]       # course forward (unity x,z)
fS = -F                                                # "southbound" pass direction used by BoatPath / SkiViewGame
R_ = np.array([-fS[1], fS[0]])                         # lateral, + = layout +p side (left of the southbound boat; ESE at Cedar Bend)
def W(a_, l_): return np.outer(np.atleast_1d(a_), fS) + np.outer(np.atleast_1d(l_), R_)
def AL(p): p = np.asarray(p); return np.stack([p @ fS, p @ R_], -1)
# ---- pass 19: Holly's turnaround v2 (v1 fallback) for the main course. A course move (course_centre_used != 0) shifts every
# layout coordinate so the origin stays on the (new) course centre.
HT2 = '/workspace/waterski-game/roadmap/venue/lakes/%s_turnaround_v2.json' % LID
HT1 = HT2.replace('_v2.json', '_v1.json')
HTF = HT2 if os.path.exists(HT2) else (HT1 if os.path.exists(HT1) else None)
HV = {}; OFF = np.zeros(2); HT_NAME = None
if HTF:
    _tj = json.load(open(HTF)); HT_NAME = os.path.basename(HTF); _main = _tj['turnarounds'][0]['course']
    for t_ in _tj['turnarounds']:
        if t_['course'] == _main: HV[t_['end']] = t_
    OFF = np.array((HV.get('fwd') or HV.get('back'))['course_centre_used'], float)
def _shift(v):
    if isinstance(v, list) and len(v) == 2 and all(isinstance(x, (int, float)) for x in v): return [v[0] - OFF[0], v[1] - OFF[1]]
    if isinstance(v, list): return [_shift(x) for x in v]
    return v
if np.any(np.abs(OFF) > 0.05):
    for k_ in ('shore_build', 'shore_build_other', 'island_polys', 'shore', 'dock_box_v2_frame'):
        if k_ in d: d[k_] = _shift(d[k_])
    for e_ in d.get('ends', {}).values(): e_['at'] = _shift(e_['at'])
    if 'jump' in d: d['jump']['ramp_centre'] = _shift(d['jump']['ramp_centre'])
    print('course moved (Holly v2 course_centre_used %s): layout re-centred' % OFF.tolist())
USE_HV = bool(HV.get('fwd') and HV.get('back')) and not SH.get('no_holly_turns', False)
def chaikin(pts, n=2):
    for _ in range(n):
        q = []
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            q += [0.75 * a + 0.25 * b, 0.25 * a + 0.75 * b]
        pts = np.array(q)
    return pts
def resample(ring, step):
    seg = np.linalg.norm(np.diff(np.vstack([ring, ring[:1]]), axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)])
    ss_ = np.arange(0, cum[-1], step)
    return np.stack([np.interp(ss_, cum, np.append(ring[:, 0], ring[0, 0])), np.interp(ss_, cum, np.append(ring[:, 1], ring[0, 1]))], 1)
def LP(al_, p_): return W(-np.asarray(al_), p_)        # Holly's layout course frame (along +dir_unit, +p) -> world
TREESET = None; PLAY = None; NEWLOTS = []; EXTRA = {}; HOLLY_TURN = {}; STYLE = 'pnw'; CE_N = CE_S = 214; PASS_LATE = 186.0; RAMP = None; OTHER = []; TURNS = None; JUMP_ONLY = False; RAMP_LAT = 22.0; JUMP_DIR = -1

# =====================================================================================================================
if LID == 'cedarbend':
    NAME = 'Cedar Bend Lake'
    # ---- shoreline. shore_build in the layout JSON is ~35% wider than the water in the aerial (it follows the outer lawn
    # edge on the west and the reflection-blurred edge on the east, and has a reflection notch at the north neck), so the
    # shore is rebuilt from water-edge offsets read by eye off the aerial on a 25 m course-frame grid (study only, nothing
    # traced into textures). Table: along the course axis toward NNE (= -a_south), water edge on the WNW (-p) / ESE (+p)
    # side in metres from the course line. Holly's frame, course, islands, ends and buildings are kept.
    # pass 16: the shoreline source lives in Tools/lake_data/<id>_shore.json so Holly's re-trace drops in without code changes
    # (roadmap/venue/lakes/<id>_shore_retrace.json {"shore_build": [[x, y_south], ...]} wins when present).
    RETRACE = '/workspace/waterski-game/roadmap/venue/lakes/%s_shore_retrace.json' % LID
    if os.path.exists(RETRACE) or SH['source'] == 'layout':
        sb = json.load(open(RETRACE))['shore_build'] if os.path.exists(RETRACE) else d['shore_build']
        ring = np.array(sb, float) * [1, -1]; SHORE_SRC = 'retrace' if os.path.exists(RETRACE) else 'layout shore_build (%s)' % os.path.basename(SRC)
        shore = chaikin(ring, 2)
    else:
        EDGE = SH['edge_table']
        east = [W(-a, le)[0] for a, lw, le in EDGE]
        west = [W(-a, lw)[0] for a, lw, le in EDGE]
        tn, ts = SH['edge_tips']['north'], SH['edge_tips']['south']
        ring = np.array(east[1:-1] + [W(tn[0], tn[1])[0]] + west[::-1][1:-1] + [W(ts[0], ts[1])[0]])
        shore = chaikin(ring, 3); SHORE_SRC = 'edge table (by eye, Tools/lake_data)'
    print('shore source:', SHORE_SRC)
    # moorage slip: a 24 x 22 m basin cut into the ESE bank (layout ~(+5,+150) +-15 m)
    MOOR_A0, MOOR_A1, MOOR_P = SH['moorage_slip']['a_south_from'], SH['moorage_slip']['a_south_to'], SH['moorage_slip']['inner_p']
    MOOR_M = SH['moorage_slip'].get('mouth_p', 30.0)   # riprap line at the slip mouth; the moorage sits 12 m further in
    slip = np.array([W(MOOR_A0, MOOR_M)[0], W(MOOR_A0, MOOR_P)[0], W(MOOR_A1, MOOR_P)[0], W(MOOR_A1, MOOR_M)[0]])
    # resample the ring at 1.5 m and splice the slip into it (one water outline for the mask, the bank strip and the shots)
    shore = resample(shore, 1.5)
    q = AL(shore); hit = np.where((q[:, 0] > MOOR_A0) & (q[:, 0] < MOOR_A1) & (q[:, 1] > MOOR_M - 15))[0]
    i0, i1 = hit.min(), hit.max()
    inc = q[i1, 0] > q[i0, 0]
    la0 = float(np.interp(MOOR_A0, *(q[hit][np.argsort(q[hit, 0])].T))); la1 = float(np.interp(MOOR_A1, *(q[hit][np.argsort(q[hit, 0])].T)))
    side = [W(MOOR_A0, la0)[0]] + [W(MOOR_A0, l_)[0] for l_ in np.arange(la0 + 1.5, MOOR_P, 1.5)] + \
           [W(a_, MOOR_P)[0] for a_ in np.arange(MOOR_A0, MOOR_A1, 1.5)] + [W(MOOR_A1, l_)[0] for l_ in np.arange(MOOR_P, la1, -1.5)] + [W(MOOR_A1, la1)[0]]
    if not inc: side = side[::-1]
    shore = np.vstack([shore[:i0], np.array(side), shore[i1 + 1:]])
    # islands: north (auto-traced) as is; south (manual +-5 m) 4 m north so the tip gap clears 8 m of boat clearance
    # island outlines: convex hull + smoothing (the auto-traced north island has a reflection notch that reads as a hook)
    from scipy.spatial import ConvexHull
    isl = [np.array(p) * [1, -1] for p in d['island_polys']]
    isl = [chaikin(p[ConvexHull(p).vertices], 2) for p in isl]
    isl = [p_ + F * sh_ for p_, sh_ in zip(isl, SH['island_shift_north_m'])]
    WATER = dict(shallow=[0.105, 0.150, 0.125], deep=[0.012, 0.030, 0.026], refl=[0.90, 0.92, 0.94])   # #47564a overcast water
    MOOD = dict(hdri='kloofendal_overcast_puresky_2k.hdr', sunEl=48.3, sunAz=113.1, sunIntensity=0.82, sunColor=[0.96, 0.97, 1.0],
                shadowStrength=0.38, skyExposure=1.12, ambientSky=[0.64, 0.68, 0.76], ambientEquator=[0.56, 0.59, 0.60],
                ambientGround=[0.22, 0.24, 0.19], fogColor=[0.74, 0.77, 0.81], fogDensity=0.00082,
                postExposure=0.22, contrast=7.0, saturation=-6.0, temperature=-3.0, tint=0.0)
elif LID == 'saltwhite':
    # pass 17: Estero-style Gulf ski lake (LAKE-LAYOUTS 4, saltwhite_alt). Water edge = Holly's shore_build + the ~15 m SW bank
    # widening, with the two end bulbs round the islands (Tools/make_saltwhite_shore.py -> lake_data/saltwhite_shore.json).
    NAME = 'Saltwhite Lake'; STYLE = 'gulf'
    shore = resample(chaikin(np.array(SH['shore_build'], float) * [1, -1], 2), 1.5); SHORE_SRC = 'lake_data/saltwhite_shore.json (widened 15 m SW + end bulbs)'
    # pass 19 punch list: the SW / NE edges were stair-stepped (10-20 m steps from the widening). Low-pass the outline along its arc
    # (sigma 21 m), then report the tightest bend radius; the skier-track push below keeps the clearance and the widening.
    from scipy.ndimage import gaussian_filter1d
    shore = resample(gaussian_filter1d(shore, 14.0, axis=0, mode='wrap'), 1.5)
    def _minR(P):
        a_, b_, c_ = np.roll(P, 6, 0), P, np.roll(P, -6, 0)
        ab, bc, ca = np.linalg.norm(a_ - b_, axis=1), np.linalg.norm(b_ - c_, axis=1), np.linalg.norm(c_ - a_, axis=1)
        u_, w_ = b_ - a_, c_ - a_; cr = np.abs(u_[:, 0] * w_[:, 1] - u_[:, 1] * w_[:, 0]) / 2.0
        return np.where(cr > 1e-6, ab * bc * ca / (4.0 * np.maximum(cr, 1e-9)), 1e9)
    print('saltwhite smoothed shore: min bend radius %.0f m (5th pct %.0f m)' % (_minR(shore).min(), np.percentile(_minR(shore), 5)))
    # pass 18: the skier swings to 4.5 m off the bank at the NW end (Unity clearance check: along -221 m, 45 m off the line)
    # -> ease that bank out by up to 6 m over a ~36 m stretch (the aerial's water is wider there than shore_build)
    sk = None
    for sg in (1, -1):
        c_ = W(-221.0, sg * 45.0)[0]; dd_ = np.linalg.norm(shore - c_, axis=1)
        if sk is None or dd_.min() < sk[1]: sk = (c_, dd_.min())
    c_ = sk[0]; dv = shore - c_; dd_ = np.linalg.norm(dv, axis=1)
    push = 6.0 * np.clip(1.0 - (dd_ - sk[1]) / 18.0, 0, 1) ** 2
    shore = shore + dv / np.maximum(dd_, 1e-6)[:, None] * push[:, None]
    # and where the Unity skier track (SkiViewCapture.SkierClearance -> lake_data/saltwhite_skier_track.json) still comes within
    # 9 m of the water edge, push those edge points straight away from the track to 9 m (smoothed so the bank stays natural)
    TRK = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'lake_data', 'saltwhite_skier_track.json')
    if os.path.exists(TRK):
        from scipy.spatial import cKDTree
        tr = np.array(json.load(open(TRK))['xz'], float); kd = cKDTree(tr)
        for _ in range(3):
            dist, ix = kd.query(shore); need = np.clip(10.0 - dist, 0, None)
            need = np.convolve(np.concatenate([need[-8:], need, need[:8]]), np.hanning(17) / np.hanning(17).sum() * 2.0, 'same')[8:-8]
            need = np.maximum(need, np.clip(10.0 - dist, 0, None))
            dv = shore - tr[ix]; shore = shore + dv / np.maximum(np.linalg.norm(dv, axis=1), 1e-6)[:, None] * need[:, None]
        print('saltwhite skier-track push: %d edge points moved, track min to edge now %.1f m' % ((need > 0.05).sum(), kd.query(shore)[0].min()))
    print('saltwhite NW bank: nearest shore %.1f m from the skier point, pushed %d vertices up to %.1f m' % (sk[1], (push > 0.05).sum(), push.max()))
    isl = [chaikin(np.array(p_) * [1, -1], 1) for p_ in d['island_polys']]
    if not USE_HV: isl[1] = isl[1] + R_ * 2.5     # (pass 19: Holly's spin path is fitted to the unshifted island) SE island 2.5 m toward the SW bulb so the NE gap (18 m) holds 8 m of boat clearance
    MOOR_A0 = MOOR_A1 = 1e9; MOOR_M = MOOR_P = 0.0
    # course straights: the SE island sits 207 m down the line, so the SE straight stops at 178 m and the boat rounds the island;
    # passes start up to 172 m out (the NW-bound run-in is short)
    CE_N, CE_S, PASS_LATE = 178, 214, 172.0
    # jump (Holly's computed fit, widened SW bank): ramp centre (-22.3, 9.3) on the +p (SW) side, 22 m off the slalom line, jump run
    # along +dir_unit (NW -> SE) = -fS, so jumpDir = -1. Lip (top edge) = centre + 3.3 m down-run.
    rc = np.array(d['jump']['ramp_centre']) * [1, -1]
    RAMP = dict(lip=[float(v_) for v_ in rc + F * 3.3], fwd=[float(F[0]), float(F[1])], open_deg=1.75, ratio=0.255, length=6.85, height=1.75, width=4.22)
    WATER = dict(shallow=[0.10, 0.36, 0.31], deep=[0.010, 0.042, 0.037], refl=[0.92, 0.95, 0.98])   # turquoise over white sand, #284e43-#2b4e47 deep
    MOOD = dict(hdri='syferfontein_18d_clear_puresky.hdr', sunEl=21.8, sunAz=75.8, sunIntensity=1.35, sunColor=[1.0, 0.94, 0.84],
                shadowStrength=0.82, skyExposure=1.0, ambientSky=[0.52, 0.62, 0.80], ambientEquator=[0.62, 0.64, 0.62],
                ambientGround=[0.40, 0.38, 0.30], fogColor=[0.80, 0.86, 0.92], fogDensity=0.00042,
                postExposure=0.05, contrast=9.0, saturation=4.0, temperature=3.0, tint=0.0)
elif LID in ('villaserena', 'tallpine', 'heronmarsh'):
    # pass 18: built from Holly's v2 files (layout shore_build + shore_build_other, islands, ends, jump fit; look briefs).
    NAME = {'villaserena': 'Villa Serena', 'tallpine': 'Tall Pine', 'heronmarsh': 'Heron Marsh'}[LID]
    STYLE = {'villaserena': 'gulf', 'tallpine': 'pnw', 'heronmarsh': 'pnw'}[LID]
    shore = resample(chaikin(np.array(d['shore_build'], float) * [1, -1], 2), 1.5); SHORE_SRC = 'v2 shore_build'
    OTHER = [resample(chaikin(np.array(o_, float) * [1, -1], 2), 1.5) for o_ in d.get('shore_build_other', [])]
    isl = [chaikin(np.array(p_) * [1, -1], 1) for p_ in d['island_polys']]
    if LID == 'tallpine':   # islands 15% smaller about their centres: Holly's 9.5 m north-island gap (now 19 m) is too tight for an 18 m rope
        isl = [ip_.mean(0) + (ip_ - ip_.mean(0)) * 0.85 for ip_ in isl]
        # (the east island stays in the water but the boat no longer loops it, see TURNS)
        # past the west island's tip the skier swings wide round Holly's loop (Unity SkierClearance 4.5 m at a 369, lateral 37):
        # push just that end-of-bowl bank (a > 352) away from the recorded skier track, to 9 m
        TRK = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'lake_data', 'tallpine_skier_track.json')
        if os.path.exists(TRK):
            from scipy.spatial import cKDTree
            tr = np.array(json.load(open(TRK))['xz'], float); kd = cKDTree(tr); end_ = AL(shore)[:, 0] > 352; d0 = kd.query(shore[end_])[0].min()
            for _ in range(3):
                dist, ix = kd.query(shore); need = np.clip(9.0 - dist, 0, None) * end_
                need = np.maximum(np.convolve(np.concatenate([need[-8:], need, need[:8]]), np.hanning(17) / np.hanning(17).sum() * 2.0, 'same')[8:-8] * (AL(shore)[:, 0] > 330), need)
                dv = shore - tr[ix]; shore = shore + dv / np.maximum(np.linalg.norm(dv, axis=1), 1e-6)[:, None] * need[:, None]
            print('tallpine west-end bank push: skier track to bank %.1f -> %.1f m' % (d0, kd.query(shore[AL(shore)[:, 0] > 352])[0].min()))
    # (a Saltwhite-style skier-track bank push was tried here and dropped: around an island loop it pulls the outer bank into the gap)
    MOOR_A0 = MOOR_A1 = 1e9; MOOR_M = MOOR_P = 0.0
    def end_a(e):   # along_south of an end-circle centre
        c_ = np.array(d['ends'][e]['at'], float) * [1, -1]; return float(AL(c_[None])[0, 0]), float(AL(c_[None])[0, 1]), float(d['ends'][e]['max_turn_radius_m'])
    # turns: island loops at Tall Pine (both ends, Holly turnaround v1); teardrops in the end bowls at Villa Serena and Heron
    # Marsh (Holly's spin turns need a boat pivot the sim doesn't do; the teardrop radius is the free circle less 13 m)
    TURNS = {}
    HT = '/workspace/waterski-game/roadmap/venue/lakes/%s_turnaround_v1.json' % LID
    HOLLY_TURN = {}
    if LID == 'tallpine' and os.path.exists(HT):
        for t_ in json.load(open(HT))['turnarounds']:
            bp_ = np.array(t_['boat_path'], float) * [1, -1]
            if t_['end'] == 'back': HOLLY_TURN['S'] = bp_
            # the east ('fwd') island loop is dropped: the Unity skier on a 16-18 m rope cuts the chord straight across that 35 m
            # island (SkierClearance 0.5 m, track inside the island polygon), so that end turns in the open bowl before it
            # (Holly's free circle R 39.9 m at x 200) as a teardrop
    for e in ('fwd', 'back'):
        a_c, p_c, rf = end_a(e)
        TURNS['S' if a_c > 0 else 'N'] = dict(kind='island' if (LID == 'tallpine' and e == 'back') else 'teardrop', a=abs(a_c), p=p_c, R=max(11.0, rf - 13.0))
    if LID == 'tallpine':   # east bowl teardrop pushed out to 255 m (the bowl is ~78 m wide from 175 to 300 m) so the pass keeps its pre-gates
        k_ = 'S' if TURNS['S']['kind'] == 'teardrop' else 'N'; TURNS[k_].update(a=255.0, R=25.0)
    # pass-end straights: stop 25 m short of the end circle so the turn has room
    CE_N = int(min(214, TURNS['N']['a'] - (TURNS['N']['R'] if TURNS['N']['kind'] == 'teardrop' else 40) - 8))
    CE_S = int(min(214, TURNS['S']['a'] - (TURNS['S']['R'] if TURNS['S']['kind'] == 'teardrop' else 40) - 8))
    PASS_LATE = float(min(186.0, min(CE_N, CE_S) - 6))
    JUMP_ONLY = LID == 'heronmarsh'
    RAMP_LAT = 22.0
    if LID in ('tallpine', 'heronmarsh'):
        jd = d['jump']; rc = np.array(jd['ramp_centre'], float) * [1, -1]
        jdir = F if jd['jump_direction'].startswith('same') else -F      # run direction (unity)
        RAMP = dict(lip=[float(v_) for v_ in rc + jdir * 3.3], fwd=[float(jdir[0]), float(jdir[1])], open_deg=1.75, ratio=0.255, length=6.85, height=1.75, width=4.22)
        RAMP_LAT = abs(float(AL(rc[None])[0, 1]))
        JUMP_DIR = 1 if np.dot(jdir, fS) > 0 else -1
    if LID == 'villaserena':
        WATER = dict(shallow=[0.13, 0.36, 0.52], deep=[0.020, 0.075, 0.150], refl=[0.92, 0.95, 0.98])    # #387cb7 eye level, #58a0d9 lane
        MOOD = dict(hdri='syferfontein_18d_clear_puresky.hdr', sunEl=79.9, sunAz=130.8, sunIntensity=1.55, sunColor=[1.0, 0.97, 0.92],
                    shadowStrength=0.85, skyExposure=1.05, ambientSky=[0.50, 0.62, 0.84], ambientEquator=[0.64, 0.68, 0.66],
                    ambientGround=[0.36, 0.38, 0.28], fogColor=[0.74, 0.83, 0.92], fogDensity=0.00050,
                    postExposure=0.0, contrast=10.0, saturation=6.0, temperature=1.0, tint=0.0)
    elif LID == 'tallpine':
        # pass 19 Tall Pine look: less murky (clearer, bluer water, thinner haze), warmer golden-hour grade, brighter ambient
        WATER = dict(shallow=[0.24, 0.33, 0.38], deep=[0.045, 0.070, 0.085], refl=[0.97, 0.98, 1.0])     # #657b87 golden-hour glass, lifted
        MOOD = dict(hdri='rosendal_park_sunset_puresky_2k.hdr', sunEl=12.6, sunAz=287.7, sunIntensity=1.45, sunColor=[1.0, 0.78, 0.52],
                    shadowStrength=0.52, skyExposure=1.05, ambientSky=[0.80, 0.86, 0.96], ambientEquator=[0.86, 0.82, 0.74],
                    ambientGround=[0.52, 0.52, 0.44], fogColor=[0.92, 0.85, 0.74], fogDensity=0.00034,
                    postExposure=0.15, contrast=7.0, saturation=8.0, temperature=16.0, tint=1.0)
    else:
        WATER = dict(shallow=[0.055, 0.085, 0.085], deep=[0.008, 0.014, 0.014], refl=[0.97, 0.98, 1.0])   # #1a2628 tannin teal
        MOOD = dict(hdri='rosendal_park_sunset_puresky_2k.hdr', sunEl=7.7, sunAz=290.8, sunIntensity=1.1, sunColor=[1.0, 0.74, 0.50],
                    shadowStrength=0.7, skyExposure=0.95, ambientSky=[0.52, 0.58, 0.66], ambientEquator=[0.62, 0.56, 0.44],
                    ambientGround=[0.24, 0.22, 0.14], fogColor=[0.84, 0.82, 0.72], fogDensity=0.00085,
                    postExposure=0.0, contrast=9.0, saturation=-2.0, temperature=16.0, tint=3.0)
elif LID in ('saguaro', 'harrowpond', 'gulfwind', 'marrowind'):
    # pass 19: new lakes from Holly's v2 layouts + turnaround v2 + look briefs (LAKE-LAYOUTS 6-9). In-game names are fictional.
    NAME = {'saguaro': 'Saguaro Flats', 'harrowpond': 'Harrow Pond', 'gulfwind': 'Gulfwind', 'marrowind': 'Marrowind'}[LID]
    STYLE = {'saguaro': 'desert', 'harrowpond': 'pnw', 'gulfwind': 'gulf', 'marrowind': 'gulf'}[LID]
    TREESET = {'saguaro': 'desert'}.get(LID, 'pnw')
    shore = resample(chaikin(np.array(d['shore_build'], float) * [1, -1], 2), 6.0 if LID == 'marrowind' else 1.5); SHORE_SRC = 'v2 shore_build'
    isl = [chaikin(np.array(p_, float) * [1, -1], 1) for p_ in d.get('island_polys', []) if len(p_) > 2]
    if LID == 'gulfwind':
        # LAKE-LAYOUTS 7 build decision: widen each bank ~2 m along course B and its turn reaches (along -335..300 m) so the skier's
        # +-18 m swing keeps >= 3 m to the bank (Holly measured 1.8 m on the narrowest cross-section)
        q_ = AL(shore); al_ = -q_[:, 0]; p_ = q_[:, 1]
        w_ = np.clip((al_ + 340) / 20, 0, 1) * np.clip((305 - al_) / 20, 0, 1) * np.clip((np.abs(p_) - 8) / 6, 0, 1)
        shore = shore + np.sign(p_)[:, None] * R_[None] * (2.0 * w_)[:, None]
        print('gulfwind: banks widened 2 m on %d shore points along course B' % int((w_ > 0.5).sum()))
    MOOR_A0 = MOOR_A1 = 1e9; MOOR_M = MOOR_P = 0.0
    JUMP_ONLY = LID == 'gulfwind'
    jd = d['jump']; rc = np.array(jd['ramp_centre'], float) * [1, -1]
    jdir = F if jd['jump_direction'].startswith('same') else -F
    RAMP = dict(lip=[float(v_) for v_ in rc + jdir * 3.3], fwd=[float(jdir[0]), float(jdir[1])], open_deg=1.75, ratio=0.255, length=6.85, height=1.75, width=4.22)
    RAMP_LAT = abs(float(AL(rc[None])[0, 1])); JUMP_DIR = 1 if np.dot(jdir, fS) > 0 else -1
    def _w(hx, k=0.52, dk=0.17):   # LAKE-LAYOUTS water hex -> shader shallow / deep (same mapping as the pass 18 lakes)
        c_ = [int(hx[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]; s_ = [round(v_ * k, 3) for v_ in c_]
        return dict(shallow=s_, deep=[round(v_ * dk, 3) for v_ in s_], refl=[0.95, 0.97, 1.0])
    if LID == 'saguaro':
        WATER = _w('#286b60')
        MOOD = dict(hdri='syferfontein_18d_clear_puresky.hdr', sunEl=22.3, sunAz=78.0, sunIntensity=1.4, sunColor=[1.0, 0.93, 0.82],
                    shadowStrength=0.85, skyExposure=1.0, ambientSky=[0.55, 0.66, 0.86], ambientEquator=[0.74, 0.68, 0.58],
                    ambientGround=[0.50, 0.42, 0.34], fogColor=[0.80, 0.86, 0.94], fogDensity=0.00030,
                    postExposure=0.05, contrast=9.0, saturation=2.0, temperature=4.0, tint=0.0)
    elif LID == 'harrowpond':
        WATER = _w('#4b6176')
        MOOD = dict(hdri='kloofendal_48d_partly_cloudy_puresky_2k.hdr', sunEl=41.5, sunAz=97.0, sunIntensity=1.3, sunColor=[1.0, 0.97, 0.90],
                    shadowStrength=0.75, skyExposure=1.0, ambientSky=[0.56, 0.64, 0.80], ambientEquator=[0.60, 0.64, 0.58],
                    ambientGround=[0.26, 0.28, 0.20], fogColor=[0.76, 0.82, 0.90], fogDensity=0.00055,
                    postExposure=0.05, contrast=8.0, saturation=4.0, temperature=1.0, tint=0.0)
    elif LID == 'gulfwind':
        WATER = _w('#498989')
        MOOD = dict(hdri='kloofendal_48d_partly_cloudy_puresky_2k.hdr', sunEl=75.5, sunAz=122.5, sunIntensity=1.5, sunColor=[1.0, 0.98, 0.93],
                    shadowStrength=0.8, skyExposure=1.05, ambientSky=[0.58, 0.64, 0.80], ambientEquator=[0.68, 0.70, 0.66],
                    ambientGround=[0.36, 0.36, 0.26], fogColor=[0.74, 0.80, 0.88], fogDensity=0.00075,
                    postExposure=0.0, contrast=6.0, saturation=-10.0, temperature=2.0, tint=0.0)
    else:
        WATER = _w('#5f7586')
        MOOD = dict(hdri='rosendal_park_sunset_puresky_2k.hdr', sunEl=6.3, sunAz=68.8, sunIntensity=1.15, sunColor=[1.0, 0.72, 0.48],
                    shadowStrength=0.6, skyExposure=0.95, ambientSky=[0.50, 0.56, 0.70], ambientEquator=[0.70, 0.60, 0.50],
                    ambientGround=[0.24, 0.22, 0.16], fogColor=[0.86, 0.78, 0.70], fogDensity=0.00045,
                    postExposure=0.05, contrast=8.0, saturation=0.0, temperature=12.0, tint=2.0)
        PLAY = [[float(c_[0]), -float(c_[1])] for c_ in d['play_area']['playable_water_box']['corners']]
else:
    raise SystemExit('lake not configured: ' + LID)

# =====================================================================================================================
TS = 1600.0; RES = 1025; HALF = TS / 2; PX = TS / (RES - 1)
# pass 19: terrain centre offset for the long lakes (Gulfwind's 1.3 km canal, Marrowind's east shore); Unity reads terrain.cx / cz
CX, CZ = {'gulfwind': (-360.0, 0.0), 'marrowind': (-300.0, 0.0)}.get(LID, (0.0, 0.0))
def to_px(p): return [((x - CX + HALF) / PX, (HALF - (z - CZ)) / PX) for x, z in p]
def mask_of(poly):
    im = Image.new('L', (RES, RES), 0); ImageDraw.Draw(im).polygon(to_px(poly), fill=1); return np.array(im, bool)
lake = mask_of(shore)
for o_ in OTHER: lake |= mask_of(o_)
islands = np.zeros_like(lake)
for p in isl: islands |= mask_of(p)
water = lake & ~islands
dist_in = ndimage.distance_transform_edt(water) * PX
dist_out = ndimage.distance_transform_edt(~water) * PX
cols = np.arange(RES) * PX - HALF + CX; rows = HALF - np.arange(RES) * PX + CZ
X, Z = np.meshgrid(cols, rows)
A = X * fS[0] + Z * fS[1]; Pp = X * R_[0] + Z * R_[1]      # Pp = layout p (+ = ESE)
def fbm(octaves, base, seed):
    r = np.random.default_rng(seed); acc = np.zeros((RES, RES)); amp = 1; tot = 0
    for o in range(octaves):
        n = r.standard_normal((max(2, int(RES / base * 2 ** o)),) * 2)
        n = ndimage.zoom(n, RES / n.shape[0], order=3)[:RES, :RES]
        acc += n * amp; tot += amp; amp *= 0.5
    return acc / tot
hills = fbm(5, 8, 11); fine = fbm(4, 40, 12)
land = dist_out
def sample(arr, x, z):
    c = int(round((x - CX + HALF) / PX)); r = int(round((HALF - (z - CZ)) / PX))
    return arr[min(max(r, 0), RES - 1), min(max(c, 0), RES - 1)]

if LID == 'cedarbend':
    # ---- zones (course frame: A = a_south, Pp = +ESE) ----
    eastSide = Pp > 0
    lawnE_w = np.interp(-A, [-300, -230, -190, -140, 330, 380, 440], [10, 18, 60, 135, 135, 60, 30])    # east lawn depth from the shore
    lawnE = eastSide & (land < lawnE_w + 18 * fine)
    # pass 17: the west lawn strip is narrow on the aerial (v2 re-trace): mown grass 9-14 m from the water to the road's tree
    # row; the road runs ~18 m and the rail-trail ~31 m back from the water (LAKE-LAYOUTS 1: 15-30 m), following the bank
    westReach = (~eastSide) & (-A > -250) & (-A < 380)
    lawnW = westReach & (land < 11.5 + 2.5 * fine)
    lawnN = (-A > 365) & (land < 45 + 10 * fine)
    southEnd = (-A < -235)
    lawn = (lawnE | lawnW | lawnN) & ~(southEnd & (land > 10))
    lawn |= (land < 9) & ~islands      # mown grass lip all round
    # west road (paved, 6 m) and the rail-trail (gravel, 3.5 m): straight, parallel to the course ~40 m / ~60 m behind the WNW
    # bank (aerial: p = -82 and -104), running the length of the valley; lawn strip between the water and the road's tree row
    landS = ndimage.gaussian_filter(land, 6)                          # smoothed so the road doesn't copy every wiggle of the bank
    bend = westReach & (-A < 290)
    road = bend & (np.abs(landS - 18.0) < 3.0)
    trail = bend & (np.abs(landS - 31.0) < 1.8)
    # north of the bulb the road and trail carry straight on up the valley (they don't wrap round the north end)
    for msk, wd in ((road, 3.0), (trail, 1.8)):
        sel = msk & (np.abs(-A - 285) < 6)
        if sel.any():
            p0 = float(np.median(Pp[sel])); msk |= (~eastSide) & (-A >= 290) & (-A < 460) & (np.abs(Pp - p0) < wd) & ~lake
    # moorage gravel pad + parking (east, south of the slip), gravel lane north up the east lawn, barn pad at the north end
    def near_seg(p0, p1, wdt):
        p0 = np.array(p0); p1 = np.array(p1); v = p1 - p0; t = np.clip(((X - p0[0]) * v[0] + (Z - p0[1]) * v[1]) / (v @ v), 0, 1)
        return np.hypot(X - (p0[0] + t * v[0]), Z - (p0[1] + t * v[1])) < wdt
    pad = (np.abs(A - 128) < 22) & (np.abs(Pp - 92) < 24)
    lane = near_seg(W(110, 96)[0], W(-60, 112)[0], 2.2) | near_seg(W(-60, 112)[0], W(-250, 150)[0], 2.2) | near_seg(W(150, 58)[0], W(140, 80)[0], 2.6)
    house = W(206, 66)[0]                       # farmhouse, SE corner, ~40 m back from the water
    houseYard = np.hypot(X - house[0], Z - house[1]) < 36 + 6 * fine
    drive = near_seg(house, W(150, 100)[0], 2.4)
    barn = np.array([151.0, 397.0])              # layout (151, -397)
    barnPad = np.hypot(X - barn[0], Z - barn[1]) < 24
    barnYard = np.hypot(X - barn[0], Z - barn[1]) < 36 + 4 * fine       # pass 17: mown farmyard round the barn (no ferns / brambles)
    houseYard = houseYard | barnYard
    gravel = road | trail | pad | lane | drive | barnPad
    lawn |= houseYard
    road = road & ~barnYard; trail = trail & ~barnYard
    # ---- heights ----
    h = 0.30 + np.clip(land, 0, 10) / 8.0 * 1.0                          # 0.3 m soft lip then 1:8 to ~1.55 m
    h = np.minimum(h, 1.55) + np.clip(land - 10, 0, 300) * 0.008          # gentle fairway rise
    # pass 16: rolling foothills. Low swells on the valley floor past the lawns, then forested valley walls rising to
    # 40-130 m at the terrain edge (the lake sits in a flat-floored foothill valley; the backdrop rings carry on beyond).
    far = np.clip((land - 150) / 470, 0, 1) ** 1.35
    swell = np.clip((land - 60) / 160, 0, 1) * (3.5 + 3.0 * hills)
    ridges = np.abs(fbm(4, 5, 13))                       # ridge/gully pattern on the walls
    h = h + swell + far * (78 + 34 * hills + 22 * ridges) + np.clip(land - 20, 0, 80) / 80 * fine * 0.5
    h = np.where(road | trail, h - 0.08, h)
    h = np.where(water, -np.clip(dist_in, 0, 12) / 12 * 3.4 - 0.22, h)
    islH = ndimage.distance_transform_edt(islands) * PX
    h = np.where(islands, 0.25 + np.clip(islH, 0, 6) * 0.16, h)
    # flatten pads under the buildings
    for c, r_, y in ((house, 16, None), (barn, 20, None)):
        m_ = np.hypot(X - c[0], Z - c[1]) < r_
        h[m_] = float(np.median(h[m_]))
    h = ndimage.gaussian_filter(h, 0.8)
    # ---- splat: R mown lawn, G rough / dry grass, B forest floor, A gravel / rock ----
    dry = np.clip(0.45 + 1.4 * fbm(4, 28, 21), 0, 1) * 0.38        # summer-dry patches in the fairway (photos)
    spl = np.zeros((RES, RES, 4))
    # mowing stripes on the fairway and strip (~7 m passes along the course axis, light/dark), faded near the trees
    stripe = 0.5 + 0.5 * np.tanh(4 * np.sin(2 * np.pi * Pp / 14.0))
    dry = np.clip(dry + (stripe - 0.5) * 0.16 * (land < 90), 0, 1)
    spl[..., 0] = lawn * (1 - dry); spl[..., 1] = lawn * dry
    rough = (~lawn) & (land < 75) & ~southEnd
    spl[..., 1] += rough * 0.55; spl[..., 2] += (~lawn) * (1 - rough * 0.55)
    spl[gravel] = [0, 0.08, 0, 0.92]
    # pass 16: natural lawn edges. Unmowed strips where the lawn meets the trees (3-7 m, ragged) and a broken unmowed fringe at
    # the waterline; a dark wet band right at the water.
    to_edge = ndimage.distance_transform_edt(lawn) * PX                 # metres inside the lawn from its edge
    forest_edge = lawn & (to_edge < 4.0 + 3.0 * fine) & (land > 6)
    fringe_n = fbm(3, 30, 22)
    water_fringe = lawn & (land < 1.6 + 1.2 * fringe_n) & (fringe_n > -0.15)
    unmowed = (forest_edge | water_fringe) & ~gravel
    spl[unmowed] = [0.12, 0.88, 0, 0]
    wet = (land < 0.8 + 0.4 * fine) & ~water
    spl[wet] = spl[wet] * 0.35 + np.array([0, 0.15, 0.40, 0.10])
    spl[islands] = [0.75, 0.25, 0, 0]
    spl = ndimage.gaussian_filter(spl, (0.9, 0.9, 0)); spl /= spl.sum(-1, keepdims=True) + 1e-6
elif LID == 'saltwhite':
    # ---- the estate (aerial study, Holly frame): a mown lawn compound inside a ring road, 13 lots on both banks, white sand rim
    # 3-8 m round the water, pine flatwoods (slash pine + saw palmetto) outside the road to the horizon ----
    def near_seg(p0, p1, wdt):
        p0 = np.array(p0); p1 = np.array(p1); v = p1 - p0; t = np.clip(((X - p0[0]) * v[0] + (Z - p0[1]) * v[1]) / (v @ v), 0, 1)
        return np.hypot(X - (p0[0] + t * v[0]), Z - (p0[1] + t * v[1])) < wdt
    EST = np.array([(-285, 50), (-65, 240), (315, 240), (315, 0), (130, -150), (-70, -230), (-270, -260)], float)   # layout x, y_south
    est_w = chaikin(EST * [1, -1], 1)
    estate = mask_of(est_w)
    road = np.zeros_like(estate)
    ring_ = np.vstack([est_w, est_w[:1]])
    for i in range(len(est_w)): road |= near_seg(ring_[i], ring_[i + 1], 3.2)
    # 13 lots (layout along, p): 7 on the NE bank, 5 on the SW bank, 1 past the SE end; houses face the water
    LOTS = [(-226, -78), (-157, -102), (-26, -100), (35, -88), (99, -92), (166, -94), (245, -122),
            (-200, 136), (-117, 98), (-14, 100), (81, 96), (160, 94), (336, -44)]
    lots = [LP(a_, p_)[0] for a_, p_ in LOTS]
    est_pts = resample(est_w, 2.0)
    drives = np.zeros_like(estate); houseYard = np.zeros_like(estate)
    for c in lots:
        q_ = est_pts[np.argmin(np.hypot(*(est_pts - c).T))]
        drives |= near_seg(c, q_, 2.1)
        drives |= np.hypot(X - (c[0] + 0.25 * (q_[0] - c[0])), Z - (c[1] + 0.25 * (q_[1] - c[1]))) < 7.5    # motor-court circle
        houseYard |= np.hypot(X - c[0], Z - c[1]) < 30 + 4 * fine
    n01 = np.clip(0.5 + 1.6 * fine, 0, 1)
    sand = (land < 3.0 + 5.0 * n01) & ~water
    sand |= islands
    gravel = (road | drives) & ~sand
    lawn = estate & ~sand & ~gravel
    unmowed = np.zeros_like(estate); fringe_n = fbm(3, 30, 22)
    est_edge = ndimage.distance_transform_edt(estate) * PX
    # ---- heights: flat Gulf-coast ground. Sand beach 1:8 up to ~1.1 m, then near-level lawn and flatwoods (1-3 m swells) ----
    h = 0.22 + np.clip(land, 0, 7) / 7.0 * 0.9
    h = h + np.clip(land - 7, 0, 300) * 0.0025 + np.clip((land - 90) / 300, 0, 1) * (1.0 + 1.2 * hills) + np.clip(land - 10, 0, 60) / 60 * fine * 0.25
    h = np.where(road | drives, h - 0.05, h)
    h = np.where(water, -np.clip(dist_in, 0, 16) / 16 * 3.0 - 0.12, h)          # long pale sand shelf, then 3 m
    islH = ndimage.distance_transform_edt(islands) * PX
    h = np.where(islands, 0.18 + np.clip(islH, 0, 7) * 0.12, h)
    for c in lots:
        m_ = np.hypot(X - c[0], Z - c[1]) < 22
        h[m_] = float(np.median(h[m_]))
    h = ndimage.gaussian_filter(h, 0.8)
    # ---- splat: R mown St Augustine lawn, G white quartz sand, B flatwoods floor (needle litter / wiregrass, also the dry lawn
    # patches), A asphalt / shell road ----
    dry = np.clip(0.45 + 1.4 * fbm(4, 28, 21), 0, 1) * 0.30
    stripe = 0.5 + 0.5 * np.tanh(4 * np.sin(2 * np.pi * Pp / 12.0))
    dry = np.clip(dry + (stripe - 0.5) * 0.12, 0, 1)
    spl = np.zeros((RES, RES, 4))
    spl[..., 0] = lawn * (1 - dry); spl[..., 2] = lawn * dry
    out_ = ~estate & ~sand & ~gravel
    prairie = np.clip(0.5 + 1.2 * fbm(3, 14, 23), 0, 1)
    spl[..., 0] += out_ * 0.25 * prairie; spl[..., 2] += out_ * (1 - 0.25 * prairie)
    spl[sand] = [0, 1, 0, 0]
    wet = (land < 0.9 + 0.4 * fine) & ~water & ~islands
    spl[wet] = [0, 0.74, 0.26, 0]
    spl[gravel] = [0, 0.05, 0, 0.95]
    spl = ndimage.gaussian_filter(spl, (1.1, 1.1, 0)); spl /= spl.sum(-1, keepdims=True) + 1e-6
elif LID == 'villaserena':
    # ---- Villa Serena (brief: Central Florida twin ski lakes): villa row on the west bank (12 lots, ~37 m pitch, houses ~55 m back),
    # 5 villas on the east bank of the east lake, the slash-pine wall on the median and the far banks right to the water ----
    def near_seg(p0, p1, wdt):
        p0 = np.array(p0); p1 = np.array(p1); v = p1 - p0; t = np.clip(((X - p0[0]) * v[0] + (Z - p0[1]) * v[1]) / (v @ v), 0, 1)
        return np.hypot(X - (p0[0] + t * v[0]), Z - (p0[1] + t * v[1])) < wdt
    def west_edge(z_):
        q_ = shore[np.abs(shore[:, 1] - z_) < 3.0]; q_ = q_[q_[:, 0] < 0]; return float(q_[:, 0].min()) if len(q_) else -30.0
    def east_edge_other(z_):
        best = None
        for o_ in OTHER:
            q_ = o_[np.abs(o_[:, 1] - z_) < 3.0]
            if len(q_): best = max(best or -1e9, float(q_[:, 0].max()))
        return best
    LOTS = []
    for k in range(12):
        z_ = 205.0 - k * 37.0; LOTS.append(np.array([west_edge(z_) - 55.0, z_]))
    EAST = []
    for z_ in (-150.0, -75.0, 0.0, 75.0, 150.0):
        e_ = east_edge_other(z_)
        if e_ is not None: EAST.append(np.array([e_ + 45.0, z_]))
    lots = LOTS + EAST
    roadW = np.array([[west_edge(z_) - 92.0, z_] for z_ in np.arange(-290, 300, 10.0)])
    roadW[:, 0] = ndimage.gaussian_filter1d(roadW[:, 0], 3)
    roadE = np.array([[(east_edge_other(z_) or 120.0) + 80.0, z_] for z_ in np.arange(-250, 260, 10.0)]) if OTHER else np.zeros((0, 2))
    if len(roadE): roadE[:, 0] = ndimage.gaussian_filter1d(roadE[:, 0], 3)
    road = np.zeros_like(lake)
    for rr in (roadW, roadE):
        for i in range(len(rr) - 1): road |= near_seg(rr[i], rr[i + 1], 3.2)
    est_pts = np.vstack([roadW, roadE]) if len(roadE) else roadW
    # residential strips: from the west water edge to just past the road (west), and the east lake's east bank to its road
    westStrip = (X < 0) & (X > np.interp(Z, roadW[:, 1], roadW[:, 0]) - 18) & (np.abs(Z) < 290) & ~lake
    estate = westStrip.copy()
    if len(roadE): estate |= (X > 60) & (X < np.interp(Z, roadE[:, 1], roadE[:, 0]) + 18) & (np.abs(Z) < 250) & ~lake & (dist_out < 110)
    drives = np.zeros_like(estate); houseYard = np.zeros_like(estate)
    lot_dims = []
    for c in lots:
        q_ = est_pts[np.argmin(np.hypot(*(est_pts - c).T))]
        drives |= near_seg(c, q_, 2.4)
        drives |= np.hypot(X - (c[0] + 0.3 * (q_[0] - c[0])), Z - (c[1] + 0.3 * (q_[1] - c[1]))) < 6.5    # paver motor court
        houseYard |= np.hypot(X - c[0], Z - c[1]) < 26 + 3 * fine
    n01 = np.clip(0.5 + 1.6 * fine, 0, 1)
    sand = (land < 1.0 + 1.6 * n01) & ~water & estate          # pale sand shelf where the lawns are mown to the water
    gravel = (road | drives) & ~sand
    lawn = estate & ~sand & ~gravel
    unmowed = np.zeros_like(estate); fringe_n = fbm(3, 30, 22)
    est_edge = ndimage.distance_transform_edt(estate) * PX
    h = 0.30 + np.clip(land, 0, 5) / 5.0 * 0.8
    h = h + np.clip(land - 5, 0, 300) * 0.002 + np.clip((land - 120) / 300, 0, 1) * (1.5 + 1.5 * hills) + np.clip(land - 10, 0, 60) / 60 * fine * 0.25
    h = np.where(road | drives, h - 0.05, h)
    h = np.where(water, -np.clip(dist_in, 0, 10) / 10 * 3.2 - 0.15, h)
    for c in lots:
        m_ = np.hypot(X - c[0], Z - c[1]) < 20
        h[m_] = float(np.median(h[m_]))
    h = ndimage.gaussian_filter(h, 0.8)
    dry = np.clip(0.45 + 1.4 * fbm(4, 28, 21), 0, 1) * 0.22
    stripe = 0.5 + 0.5 * np.tanh(4 * np.sin(2 * np.pi * X / 11.0))
    dry = np.clip(dry + (stripe - 0.5) * 0.14, 0, 1)
    spl = np.zeros((RES, RES, 4))
    spl[..., 0] = lawn * (1 - dry); spl[..., 2] = lawn * dry
    out_ = ~estate & ~gravel
    spl[..., 2] += out_
    spl[sand] = [0.25, 0.75, 0, 0]
    spl[gravel] = [0, 0.05, 0, 0.95]
    spl = ndimage.gaussian_filter(spl, (1.0, 1.0, 0)); spl /= spl.sum(-1, keepdims=True) + 1e-6
elif LID in ('tallpine', 'heronmarsh'):
    # ---- Tall Pine: flat straw farmland valley; north bank = 12 m gravel bank road + one dense redwood windbreak row; south bank =
    # a low conifer row right at the water over a mown verge. Heron Marsh: live-oak / pine walls on both banks to the water,
    # a mown verge + service track on the north bank only. North = layout -p (both lakes run W-E) ----
    def near_seg(p0, p1, wdt):
        p0 = np.array(p0); p1 = np.array(p1); v = p1 - p0; t = np.clip(((X - p0[0]) * v[0] + (Z - p0[1]) * v[1]) / (v @ v), 0, 1)
        return np.hypot(X - (p0[0] + t * v[0]), Z - (p0[1] + t * v[1])) < wdt
    north = Pp < 0
    landS = ndimage.gaussian_filter(land, 4)
    if LID == 'tallpine':
        road = north & (landS > 2.5) & (landS < 13.5) & (np.abs(A) < 330)
        lawn = (~north & (land < 7.5 + 1.5 * fine)) | ((land < 2.5) & ~islands)
        farm = (land > 40)
    else:
        road = north & (np.abs(landS - 10.5) < 1.6) & (np.abs(A) < 260)
        lawn = north & (land < 8.0 + 2.0 * fine) & (np.abs(A) < 260)
        farm = np.zeros_like(lake)
    gravel = road & ~lawn if LID == 'heronmarsh' else road
    houseYard = np.zeros_like(lake); unmowed = np.zeros_like(lake); fringe_n = fbm(3, 30, 22)
    # buildings (pads): Tall Pine boathouse at Holly's dock box + clubhouse / service yard behind the road; Heron Marsh dock + sheds
    if LID == 'tallpine':
        bx = np.array(d.get('dock_box_v2_frame', [[222.0, -45.4], [244.5, -45.4], [244.5, -36.0], [222.0, -36.0]]), float) * [1, -1]
        boathouse = bx.mean(0); club = boathouse + np.array([10.0, 46.0]); service = boathouse + np.array([-40.0, 40.0])
        for c_, r_ in ((club, 30), (service, 22)): houseYard |= np.hypot(X - c_[0], Z - c_[1]) < r_
        gravel |= near_seg(club, service, 3.0)
    lawn |= houseYard
    h = 0.35 + np.clip(land, 0, 3) / 3.0 * 0.9                                     # steep earth bank, then flat
    h = h + np.clip(land - 3, 0, 400) * 0.0015
    if LID == 'tallpine':
        east_card = np.clip((A * -1 - 420) / 300, 0, 1)                             # low foothill card to the east (layout +along = east)
        h = h + east_card * (25 + 20 * hills) + np.clip((land - 300) / 400, 0, 1) * (2 + 3 * hills)
    else:
        h = h + np.clip((land - 120) / 300, 0, 1) * (1.0 + 1.5 * hills)
    h = np.where(road, h - 0.06, h)
    h = np.where(water, -np.clip(dist_in, 0, 6) / 6 * 3.6 - 0.25, h)
    islH = ndimage.distance_transform_edt(islands) * PX
    h = np.where(islands, 0.3 + np.clip(islH, 0, 6) * 0.15, h)
    if LID == 'tallpine':
        for c_ in (club, service):
            m_ = np.hypot(X - c_[0], Z - c_[1]) < 18; h[m_] = float(np.median(h[m_]))
    h = ndimage.gaussian_filter(h, 0.8)
    spl = np.zeros((RES, RES, 4))
    dry = np.clip(0.45 + 1.4 * fbm(4, 28, 21), 0, 1)
    if LID == 'tallpine':
        # straw-gold farmland (G), mown verge (R), forest floor under the rows (B), gravel road (A); orchard-row stripes in the straw
        rows_ = 0.5 + 0.5 * np.tanh(3 * np.sin(2 * np.pi * Pp / 7.0))
        # pass 19: greener ground - the farmland reads as green pasture / orchard rows with straw only in the drier stripes
        spl[..., 0] = (~lawn) * (0.62 - 0.30 * dry * rows_); spl[..., 1] = (~lawn) * (0.28 + 0.30 * dry * rows_); spl[..., 2] = (~lawn) * 0.10
        spl[..., 0] += lawn * (1 - 0.15 * dry); spl[..., 1] += lawn * 0.15 * dry
        under = (land < 26) & ~lawn & ~road
        spl[under] = [0, 0.2, 0.8, 0]
    else:
        spl[..., 2] = ~lawn; spl[..., 0] = lawn * (1 - 0.35 * dry); spl[..., 1] = lawn * 0.35 * dry
        mud = (land < 0.9 + 0.5 * fine) & ~water
        spl[mud] = [0, 0.1, 0.9, 0]
    spl[gravel] = [0, 0.08, 0, 0.92]
    spl[islands] = [0.8, 0.2, 0, 0]
    spl = ndimage.gaussian_filter(spl, (0.9, 0.9, 0)); spl /= spl.sum(-1, keepdims=True) + 1e-6

elif LID in ('saguaro', 'harrowpond', 'gulfwind', 'marrowind'):
    # ---- pass 19 new lakes: zones, lots (kit houses on graded pads), heights, splat ----
    def near_seg(p0, p1, wdt):
        p0 = np.array(p0); p1 = np.array(p1); v = p1 - p0; t = np.clip(((X - p0[0]) * v[0] + (Z - p0[1]) * v[1]) / (v @ v), 0, 1)
        return np.hypot(X - (p0[0] + t * v[0]), Z - (p0[1] + t * v[1])) < wdt
    SR = resample(shore, 3.0); SN = []
    for i in range(len(SR)):
        t_ = SR[(i + 1) % len(SR)] - SR[i - 1]; n_ = np.array([t_[1], -t_[0]]); n_ /= np.linalg.norm(n_) + 1e-9
        if sample(water, *(SR[i] + n_ * 2.0)): n_ = -n_
        SN.append(n_)
    SN = np.array(SN)
    def bank_lots(sel, ts, axis, pitch_tol=6.0, nmin=0.55, nsign=None):
        """one shore point per target coordinate ts along axis (0 = x, 1 = z) among the selected points, with a near-perpendicular normal"""
        out_ = []
        for t0 in ts:
            c_ = np.where(sel & (np.abs(SR[:, axis] - t0) < pitch_tol) & (np.abs(SN[:, 1 - axis]) > nmin))[0]
            if nsign is not None: c_ = c_[np.sign(SN[c_, 1 - axis]) == nsign]
            if len(c_): i_ = c_[np.argmin(np.abs(SR[c_, axis] - t0))]; out_.append((SR[i_].copy(), SN[i_].copy()))
        return out_
    landS = ndimage.gaussian_filter(land, 4)
    fringe_n = fbm(3, 30, 22); unmowed = np.zeros_like(lake); houseYard = np.zeros_like(lake)
    gravel = np.zeros_like(lake); lawn = np.zeros_like(lake); pads = []   # (centre, radius)
    NEWLOTS = []      # (shore point, outward normal, built piece or None, mirror)
    EXTRA = {}
    h = 0.35 + np.clip(land, 0, 3) / 3.0 * 0.9
    if LID == 'saguaro':
        # outer banks only: west bank of the west lake, east bank of the east lake; 53 m lots, 5 built per side, float dock on every lot
        W_PIECES = ['desert_modern_1s', 'desert_pueblo', 'desert_modern_pavilion', 'desert_territorial']
        zs = np.arange(-250, 380, 53.0)
        west = bank_lots(SR[:, 0] < -15, zs, 1, nsign=None); east = bank_lots(SR[:, 0] > 115, zs, 1)
        for side, lots_, built in (('w', west, {1, 3, 4, 7, 9}), ('e', east, {0, 2, 5, 8, 10})):
            for k_, (q_, n_) in enumerate(lots_):
                pc = W_PIECES[(k_ + (side == 'e')) % 4] if k_ in built else None
                NEWLOTS.append((q_, n_, pc, k_ % 2 == 1)); pads.append((q_ + n_ * 36.0, 17.0))
        south = SR[np.argmin(SR[:, 1] + 1e3 * (np.abs(SR[:, 0] - 71) > 25))]; ns_ = SN[np.argmin(SR[:, 1] + 1e3 * (np.abs(SR[:, 0] - 71) > 25))]
        EXTRA['club'] = (south, ns_); pads.append((south + ns_ * 30, 18.0))
        median = (~water) & (X > -20) & (X < 125) & (Z > -290) & (land < 40) & ~islands
        alfalfa = (X > 260) & (land > 80) & (np.abs(Z) < 640) & (X < 760)
        gpath = (landS > 5.0) & (landS < 7.5) & ~median
        road = (np.abs(landS - 72) < 3.0)
        gravel = gpath | road
        lawn = alfalfa
        h = 0.35 + np.clip(land, 0, 4) / 4.0 * 0.95 + np.clip(land - 4, 0, 500) * 0.002
        h = h + np.clip((land - 380) / 420, 0, 1) * (3 + 7 * np.abs(hills))
        EXTRA['median'] = median
    elif LID == 'harrowpond':
        ec = np.array([138.7, -10.1])                              # east channel course centre (layout (138.7, 10.1))
        def scan_land(p0, dv, maxd=400):
            for s_ in np.arange(0, maxd, 1.0):
                if not sample(water, *(p0 + dv * s_)): return p0 + dv * s_
            return p0 + dv * maxd
        ebank = scan_land(ec, -R_); club = ebank - R_ * 32.0       # ENE = -p
        wbank = scan_land(np.zeros(2), R_); judges = wbank + R_ * 6.0
        sp_i = np.argmax(SR @ F + 1e3 * ((SR @ R_) > 0) * 0 - 1e3 * (np.abs(SR @ R_ - (ec @ R_) * 0.5) > 45))
        spit = SR[sp_i]; spitN = SN[sp_i]
        EXTRA.update(club=club, ebank=ebank, judges=judges, wbank=wbank, spit=(spit, spitN))
        pads += [(club, 22.0), (judges, 8.0)]
        Le = float(ec @ R_)
        berm = (~water) & (Pp < -6) & (Pp > Le + 6) & (land < 6) & (np.abs(A) < 420)
        eastLawn = (Pp < Le - 4) & (land < 14 + 4 * fine) & (np.abs(A - (club @ fS)) < 140)
        lawn = eastLawn | (np.hypot(X - club[0], Z - club[1]) < 55) | (np.hypot(X - spit[0], Z - spit[1]) < 40) | (np.hypot(X - judges[0], Z - judges[1]) < 22)
        lawn &= ~berm
        footpath = (Pp < Le - 4) & (np.abs(landS - 8.5) < 1.0) & (np.abs(A - (club @ fS)) < 150)
        # highway on an embankment closing the north end, two short bridge spans (LAKE-LAYOUTS 6)
        b1 = np.array([-56.0, 229.0]); b2 = np.array([75.0, 188.0]); hv_ = (b2 - b1) / np.linalg.norm(b2 - b1)
        hwy = near_seg(b1 - hv_ * 900, b2 + hv_ * 900, 9.0) & ~water
        hwyE = near_seg(b1 - hv_ * 900, b2 + hv_ * 900, 18.0) & ~water
        EXTRA.update(bridges=[b1, b2], hwy_dir=hv_)
        gravel = footpath | hwy
        h = h + np.clip(land - 3, 0, 400) * 0.004
        h = h + np.clip((land - 110) / 480, 0, 1) ** 1.3 * (55 + 70 * np.abs(hills))       # wooded hills 100-250 m round
        dh_ = ndimage.distance_transform_edt(~hwyE) * PX
        h = np.where(dh_ < 14, np.maximum(h, 5.2 - np.clip(dh_ - 9, 0, 5) * 0.9), h)
        EXTRA['berm'] = berm
    elif LID == 'gulfwind':
        south = Pp > 0                                             # +p = S bank (lots); N = levee + woods
        xs = -780 + 75.0 * np.arange(14)
        lots_ = bank_lots(((SR @ R_) > 0) & (SR[:, 1] < 0), xs, 0, pitch_tol=30.0, nmin=0.5)
        pieces = ['gulf_brick_ranch', 'gulf_siding_ranch', 'gulf_estate_1s', 'gulf_brick_ranch', 'gulf_siding_ranch']
        for k_, (q_, n_) in enumerate(lots_[:13]):
            # the boathouse goes in the deepest slip notch within 25 m of the lot's frontage point
            c_ = np.where((np.abs(SR[:, 0] - q_[0]) < 25) & (SR[:, 1] < 0) & (np.abs(SN[:, 1]) > 0.3))[0]
            qn = SR[c_[np.argmin(SR[c_, 1])]] if len(c_) else q_
            NEWLOTS.append((qn, np.array([0.0, -1.0]), pieces[k_ % 5], k_ % 2 == 1)); pads.append((np.array([qn[0], qn[1] - 50.0]), 22.0))
        roadS = south & (np.abs(landS - 82) < 3.0)
        levee = (~south) & (land < 15 + 5 * fine)
        ditch = (~south) & (np.abs(landS - 20) < 1.8)
        lawn = (south & (landS < 79)) | levee | islands
        gravel = roadS | ditch
        for c_, r_ in pads: gravel |= near_seg(c_, c_ + np.array([0.0, -30.0]), 2.0)
        h = h + np.where(~south, np.clip(land - 3, 0, 10) / 10 * 0.7, 0) - np.where(ditch, 0.6, 0)
        h = h + np.clip(land - 30, 0, 600) * 0.0008
        EXTRA['south'] = south
    else:
        # Marrowind: only the east shore is inside the terrain (homes on 25-35 m lots, park on the SE); the rest of the lake ring is a silhouette
        east = X > 120
        zs = np.arange(-470, 700, 31.0)
        lots_ = bank_lots(SR[:, 0] > 150, zs, 1, pitch_tol=8.0, nmin=0.35)
        rp = ['med_ranch_marrowind', 'med_ranch_marrowind', 'med_estate_2s', 'med_ranch_marrowind', 'med_villa_tower']
        for k_, (q_, n_) in enumerate(lots_):
            NEWLOTS.append((q_, n_, rp[k_ % 5], k_ % 2 == 1)); pads.append((q_ + n_ * 44.0, 16.0))
        park = east & (Z < -500) & (Z > -820) & ~water
        lawn = (east & (land < 58) & ~park) | (park & (land < 260))
        gpaths = park & ((np.abs(np.sin(X * 0.045) * 30 + Z + 650) < 2.0) | (np.abs(landS - 12) < 1.4))
        gravel = gpaths
        h = h + np.clip(land - 3, 0, 600) * 0.0025
        EXTRA['park'] = park
        sq_ = SR[(SR[:, 0] > 150) & (SR[:, 1] < -480) & (SR[:, 1] > -760)]
        EXTRA['parkShore'] = sq_
    houseYard = np.zeros_like(lake)
    for c_, r_ in pads: houseYard |= np.hypot(X - c_[0], Z - c_[1]) < r_
    h = np.where(water, -np.clip(dist_in, 0, 6) / 6 * 3.6 - 0.25, h)
    islH = ndimage.distance_transform_edt(islands) * PX
    h = np.where(islands, 0.3 + np.clip(islH, 0, 6) * 0.15, h)
    for c_, r_ in pads:
        m_ = (np.hypot(X - c_[0], Z - c_[1]) < r_) & ~water
        if m_.any(): h[m_] = float(np.median(h[m_]))
    h = ndimage.gaussian_filter(h, 0.8)
    spl = np.zeros((RES, RES, 4)); dry = np.clip(0.45 + 1.4 * fbm(4, 28, 21), 0, 1)
    if LID == 'saguaro':
        # R alfalfa / irrigated lawn, G decomposed granite, B open desert floor, A gravel road + lake path
        spl[..., 2] = 1.0
        dg = (land < 5.5 + fine) | houseYard | EXTRA['median'] & (land < 8)
        spl[dg] = [0, 1, 0, 0]
        rows_ = 0.5 + 0.5 * np.tanh(3 * np.sin(2 * np.pi * X / 9.0))
        spl[lawn] = np.stack([0.8 + 0.2 * rows_[lawn], 0.2 - 0.2 * rows_[lawn], 0 * rows_[lawn], 0 * rows_[lawn]], -1)
        spl[gravel] = [0, 0.2, 0, 0.8]
        spl[islands] = [0, 0.3, 0, 0.7]
    elif LID == 'harrowpond':
        spl[..., 2] = 1.0
        spl[lawn] = np.stack([1 - 0.3 * dry[lawn], 0.3 * dry[lawn], 0 * dry[lawn], 0 * dry[lawn]], -1)
        mud = (land < 0.9 + 0.5 * fine) & ~water & ~lawn
        spl[mud] = [0, 0.2, 0.8, 0]
        spl[gravel] = [0, 0.05, 0, 0.95]
    elif LID == 'gulfwind':
        stripe = 0.5 + 0.5 * np.tanh(4 * np.sin(2 * np.pi * X / 11.0))
        spl[..., 2] = 1.0
        spl[lawn] = np.stack([0.85 - 0.1 * stripe[lawn], 0.05 + 0 * stripe[lawn], 0.1 + 0.1 * stripe[lawn], 0 * stripe[lawn]], -1)
        pasture = EXTRA['south'] & (landS > 86)
        spl[pasture] = [0.35, 0.0, 0.65, 0]
        sandlip = (land < 1.2 + 0.4 * fine) & ~water
        spl[sandlip] = [0.2, 0.8, 0, 0]
        spl[gravel] = [0, 0.1, 0, 0.9]
    else:
        spl[..., 2] = 1.0
        spl[lawn] = np.stack([0.9 - 0.2 * dry[lawn], 0 * dry[lawn], 0.1 + 0.2 * dry[lawn], 0 * dry[lawn]], -1)
        sandlip = (land < 1.0 + 0.5 * fine) & ~water
        spl[sandlip] = [0.3, 0.5, 0.2, 0]
        spl[gravel] = [0, 0.3, 0, 0.7]
    spl = ndimage.gaussian_filter(spl, (0.9, 0.9, 0)); spl /= spl.sum(-1, keepdims=True) + 1e-6

YMIN = -6.0; HRANGE = 200.0
os.makedirs(OUT, exist_ok=True)
r16 = np.clip((h - YMIN) / HRANGE, 0, 1)
(np.flipud(r16) * 65535).astype('<u2').tofile(OUT + 'heightmap.r16')
Image.fromarray((spl * 255).astype(np.uint8), 'RGBA').save(OUT + 'splat.png')
def hgt(x, z): return float(sample(h, x, z))

# ---------- boat loop round both islands (LAKE-LAYOUTS 0.2: >= 8 m clearance; joins the line >= 30 m before the pre-gate) ----------
def island_geo(poly):
    q = AL(poly); ac, lc = q.mean(0)
    amn, amx = q[:, 0].min(), q[:, 0].max(); lmn, lmx = q[:, 1].min(), q[:, 1].max()
    def scan(a_, l_, da, dl):
        k = 0
        while sample(water, *W(a_ + da * k, l_ + dl * k)[0]) and k < 400: k += 1
        return k * 0.5
    gw = scan(ac, lmn - 1.5, 0, -0.5) + 1.0; ge = scan(ac, lmx + 1.5, 0, 0.5) + 1.0
    ts = 1 if ac > 0 else -1; tipA = amx if ac > 0 else amn
    gt = scan(tipA + ts * 1.5, lc, ts * 0.5, 0) + 1.0
    return dict(ac=ac, lc=lc, ha=(amx - amn) / 2, amn=amn, amx=amx, lmn=lmn, lmx=lmx, gw=gw, ge=ge, gt=gt,
                lw=lmn - gw / 2, le=lmx + ge / 2, tip=tipA + ts * gt / 2)
GI = [island_geo(p) for p in isl]
Nn = ([g for g in GI if g['ac'] < 0] or [None])[0]; Ss = ([g for g in GI if g['ac'] > 0] or [None])[0]
for nm, g in (('north', Nn), ('south', Ss)):
    if g is not None: print('%s island: a=%.0f l=%.0f, gaps: lo-l side %.1f m, hi-l side %.1f m, tip %.1f m' % (nm, g['ac'], g['lc'], g['gw'], g['ge'], g['gt']))
def teardrop(sgn, Ac, Rt, pc=0.0):
    # pass 18: end-bowl teardrop (same rotational sense at both ends: south enters on +p, north is the point mirror)
    return [W(sgn * (Ac + Rt * math.sin(ph)), sgn * (pc * sgn + Rt * math.cos(ph)))[0] for ph in np.radians([-60, -25, 10, 45, 90, 135, 170, 205, 240])]
def turn(g, sgn, first_lo):
    near = g['amn'] if sgn > 0 else g['amx']; far = g['amx'] if sgn > 0 else g['amn']; mid = g['lc']
    l1, l2 = (g['lw'], g['le']) if first_lo else (g['le'], g['lw'])
    return [W(near - sgn * 22, l1 * 0.4)[0], W(near + sgn * g['ha'] * 0.25, l1)[0], W(far - sgn * g['ha'] * 0.25, l1)[0],
            W(g['tip'] - sgn * g['gt'] * 0.15, (l1 + mid) / 2)[0], W(g['tip'], mid)[0], W(g['tip'] - sgn * g['gt'] * 0.15, (l2 + mid) / 2)[0],
            W(far - sgn * g['ha'] * 0.25, l2)[0], W(near + sgn * g['ha'] * 0.25, l2)[0], W(near - sgn * 16, l2 * 0.35)[0]]
if USE_HV: CE_N = CE_S = 184; PASS_LATE = 172.0
COURSE_END = max(CE_N, CE_S)
wp = [W(a_, 0)[0] for a_ in np.linspace(-CE_N, CE_S, 17)]
ST = SH.get('south_turn', {}) if LID == 'cedarbend' else {}
if ST.get('kind') == 'teardrop':
    # pass 16 (LAKE-LAYOUTS 1, v2): the south island sits ~8 m off the south bank, so the boat turns in the south bowl on a
    # teardrop (R ~ end circle - 8 m) and the island stays as dressing. Circle centre on the course line at a_south = ac.
    Rt, ac_ = ST['radius_m'], ST['centre_a_south_m']
    for ph in np.radians(ST.get('phi_deg', [-60, -25, 10, 45, 90, 135, 170, 205, 240])):
        wp.append(W(ac_ + Rt * math.sin(ph), Rt * math.cos(ph))[0])
    print('south turn: teardrop R %.0f m centred %.0f m south of the course centre (island kept as dressing)' % (Rt, ac_))
elif TURNS and 'S' in HOLLY_TURN:
    q_ = HOLLY_TURN['S']; aa = AL(q_)[:, 0]; wp += [w_ for w_, a1 in zip(q_, aa) if a1 > CE_S + 4][::3]
    print('south turn: Holly turnaround v1 boat path (%s)' % 'island loop')
elif TURNS and TURNS['S']['kind'] == 'teardrop':
    wp += teardrop(+1, TURNS['S']['a'], TURNS['S']['R'], TURNS['S']['p'] * 0.5)
    print('south turn: teardrop R %.0f m at %.0f m' % (TURNS['S']['R'], TURNS['S']['a']))
elif not USE_HV:
    wp += turn(Ss, +1, Ss['gw'] >= Ss['ge'])
wp += [W(a_, 0)[0] for a_ in np.linspace(CE_S, -CE_N, 17)]
if TURNS and 'N' in HOLLY_TURN:
    q_ = HOLLY_TURN['N']; aa = AL(q_)[:, 0]; wp += [w_ for w_, a1 in zip(q_, aa) if a1 < -CE_N - 4][::3]
    print('north turn: Holly turnaround v1 boat path (island loop)')
elif TURNS and TURNS['N']['kind'] == 'teardrop':
    wp += teardrop(-1, TURNS['N']['a'], TURNS['N']['R'], TURNS['N']['p'] * 0.5)
    print('north turn: teardrop R %.0f m at %.0f m' % (TURNS['N']['R'], TURNS['N']['a']))
elif not USE_HV:
    wp += turn(Nn, -1, Nn['gw'] < Nn['ge'])
wp = np.array(wp)
for _ in range(2): wp = 0.25 * np.roll(wp, 1, 0) + 0.5 * wp + 0.25 * np.roll(wp, -1, 0)
HV_V = HV_SPIN = None
if USE_HV:
    # pass 19: the loop is Holly's two boat paths (exit gate -> round the end -> pre-gate, 3 m steps) joined by the course straights;
    # speeds from her profile, spin-turn idle stretch flagged (the game floats the skier through the spin, then deep-water starts)
    def _hp(t):
        q = (np.array(t['boat_path'], float) - OFF) * [1, -1]
        s_ = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(q, axis=0), axis=1))])
        pr = np.array(t['speed_profile_s_mps_mph'], float); vv = np.interp(s_, pr[:, 0], pr[:, 1])
        ph = t.get('phases_s_from_exit_gate', {}); sp = np.zeros(len(q))
        if t['turn_type'] == 'spin turn' and 'idle_and_sink' in ph:
            s1 = ph.get('spin_done_deep_water_start', ph.get('spin_done', s_[-1]))
            sp = ((s_ >= ph['idle_and_sink'] - 2) & (s_ <= s1 + 2)).astype(float)
        return np.column_stack([q, vv, sp])
    tS = _hp(HV['back']); tN = _hp(HV['fwd'])
    if AL(tS[:1, :2])[0, 0] < 0: tS, tN = tN, tS
    if PLAY is not None:
        # Marrowind: Holly's open-water teardrops reach past the 929 m play box (along +-464.5); slide each turn in along the line
        # (its legs are colinear with the course there) so the whole loop stays inside the box with 1 m to spare
        for t_ in (tS, tN):
            ex_ = np.abs(AL(t_[:, :2])[:, 0]).max() - 463.5
            if ex_ > 0:
                sg_ = np.sign(AL(t_[:, :2])[np.abs(AL(t_[:, :2])[:, 0]).argmax(), 0]); t_[:, :2] -= sg_ * fS[None] * ex_
                print('play box: %s turn slid %.1f m toward the course' % ('south' if sg_ > 0 else 'north', ex_))
    def _st(a, b):
        n_ = max(2, int(np.linalg.norm(b[:2] - a[:2]) / 3.0)); t_ = np.linspace(0, 1, n_ + 1)[1:-1]
        return np.column_stack([a[:2] + np.outer(t_, b[:2] - a[:2]), np.full(len(t_), V_PASS_), np.zeros(len(t_))])
    V_PASS_ = 34 * 0.44704
    loop_ = np.vstack([_st(tN[-1], tS[0]), tS, _st(tS[-1], tN[0]), tN])
    wp = loop_[:, :2]; HV_V = loop_[:, 2]; HV_SPIN = loop_[:, 3]
    for e_, t_ in (('fwd', HV['fwd']), ('back', HV['back'])):
        print('%s end: Holly %s %s (%s) %s' % (e_, t_['turn_type'], t_['direction'], HT_NAME, t_['parameter']))
def catmull(pts, n=8):
    out = []; N = len(pts)
    for i in range(N):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % N], pts[(i + 2) % N]
        for t in np.linspace(0, 1, n, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    return np.array(out)
pth = catmull(wp, 3 if USE_HV else 10)
seg = np.linalg.norm(np.diff(np.vstack([pth, pth[:1]]), axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)])
L = cum[-1]; ss = np.arange(0, L, 1.0)
px = np.interp(ss, cum, np.append(pth[:, 0], pth[0, 0])); pz = np.interp(ss, cum, np.append(pth[:, 1], pth[0, 1]))
path = np.stack([px, pz], 1)
if USE_HV:
    _rep = lambda a: np.repeat(a, 3)
    hv_v = np.interp(ss, cum, np.append(_rep(HV_V), HV_V[0])); hv_sp = np.interp(ss, cum, np.append(_rep(HV_SPIN), HV_SPIN[0])) > 0.5
gy, gx = np.gradient(dist_in)
for it in range(0 if USE_HV else 60):
    al = path @ fS; la = path @ R_
    free = ~((al >= -CE_N) & (al <= CE_S) & (np.abs(la) < 5))
    for i in np.where(free)[0]:
        x, z = path[i]; c = sample(dist_in, x, z)
        if c < 12.5:
            gv = np.array([sample(gx, x, z), -sample(gy, x, z)]); n = np.linalg.norm(gv)
            if n > 1e-6: path[i] += gv / n * min(1.0, (12.5 - c) * 0.4)
    sm = 0.25 * np.roll(path, 1, 0) + 0.5 * path + 0.25 * np.roll(path, -1, 0)
    path[free] = sm[free]
px, pz = path[:, 0].copy(), path[:, 1].copy()
clear = np.array([sample(dist_in, x, z) for x, z in path])
along = path @ fS; lat = path @ R_
sgn = np.sign(np.gradient(px) * fS[0] + np.gradient(pz) * fS[1])
prog = along * sgn
oncourse = (prog >= -COURSE_END) & (prog <= 184.5) & (np.abs(lat) < 6)
V_PASS = 34 * 0.44704; V_TURN = 23 * 0.44704; V_GAP = 25 / 3.6
hd = np.arctan2(np.gradient(px), np.gradient(pz)); curv = np.abs(np.gradient(np.unwrap(hd)))
vt = np.where(oncourse, V_PASS, np.minimum(V_TURN, np.sqrt(3.6 / np.maximum(curv, 1e-4)))); vt = np.maximum(vt, V_GAP * 0.9)
v = vt.copy(); n = len(v)
if USE_HV:
    v = np.maximum(hv_v, 2.0); v[oncourse] = np.maximum(v[oncourse], V_PASS); n = len(v)
    for _ in range(3):   # Holly's profile, kept within the boat's accel (3 m/s2) / decel (3.5 m/s2) limits
        for i in range(1, 2 * n): v[i % n] = min(v[i % n], math.sqrt(v[(i - 1) % n] ** 2 + 2 * 3.0))
        for i in range(2 * n, 0, -1): k = (i - 1) % n; v[k] = min(v[k], math.sqrt(v[i % n] ** 2 + 2 * 3.5))
else:
    for _ in range(3):
        for i in range(1, 2 * n): v[i % n] = min(v[i % n], math.sqrt(v[(i - 1) % n] ** 2 + 2 * 3.0))
        for i in range(2 * n, 0, -1):
            k = (i - 1) % n
            if not oncourse[k]: v[k] = min(v[k], math.sqrt(v[i % n] ** 2 + 2 * 2.5))
    for i in range(1, 2 * n):
        k = i % n
        if not oncourse[k]: v[k] = max(v[k], math.sqrt(max(0, v[(i - 1) % n] ** 2 - 2 * 2.0)))
passIdx = np.where(oncourse & (np.abs(prog) <= 184.5))[0]
print('loop %.0f m, lap %.1f s, min clearance %.1f m at %s, pass speed min %.2f m/s' % (L, np.sum(1 / v), clear.min(), path[clear.argmin()].round(1), v[passIdx].min()))
offline = ~((along >= -CE_N) & (along <= CE_S) & (np.abs(lat) < 5))
print('min clearance in the turns %.1f m' % clear[offline].min())
# skier envelope check (LAKE-LAYOUTS 0.2: +-18 m round the slalom line through the buoy zone, gate to gate)
env_c = min(sample(dist_in, *W(a_, l_)[0]) for a_ in np.arange(-129.5, 130, 1.0) for l_ in (-18.0, 18.0))
line_c = min(sample(dist_in, *W(a_, 0)[0]) for a_ in np.arange(-129.5, 130, 1.0))
print('skier envelope (+-18 m, gate to gate) min %.1f m to shore; boat line min %.1f m' % (env_c, line_c))

# ---------- buildings, docks, riprap ----------
buildings = []
def face_water(p):
    best = None
    for q in shore:
        dd = np.hypot(*(q - p))
        if best is None or dd < best[0]: best = (dd, q)
    v_ = best[1] - p; return math.degrees(math.atan2(v_[0], v_[1])), best[0]
if LID == 'cedarbend':
    yaw, dw = face_water(house)
    buildings.append(dict(kind='farmhouse', x=float(house[0]), z=float(house[1]), y=hgt(*house), yaw=round(yaw, 1), w=9.5, d=8.0, h=6.4,
                          wall=[0.949, 0.941, 0.918], roof=[0.231, 0.239, 0.251]))
    print('farmhouse %.0f m from the water, facing %.0f deg' % (dw, yaw))
    yawB, dwb = face_water(barn)
    buildings.append(dict(kind='barn', x=float(barn[0]), z=float(barn[1]), y=hgt(*barn), yaw=round(yawB + 90, 1), w=22.0, d=14.0, h=5.0,
                          wall=[0.62, 0.64, 0.66], roof=[0.290, 0.416, 0.549]))
    buildings.append(dict(kind='shed', x=float(barn[0] + 20), z=float(barn[1] + 6), y=hgt(barn[0] + 20, barn[1] + 6), yaw=round(yawB + 90, 1), w=9.0, d=7.0, h=3.6,
                          wall=[0.86, 0.85, 0.80], roof=[0.36, 0.38, 0.40]))
    # covered moorage in the slip: dock along the south side of the basin, shed-roof boat cover + lift over the slip
    mo = W(0.5 * (MOOR_A0 + MOOR_A1), MOOR_M + 12)[0]
    buildings.append(dict(kind='moorage', x=float(mo[0]), z=float(mo[1]), y=0.0, yaw=round(math.degrees(math.atan2(-R_[0], -R_[1])), 1), w=12.0, d=3.0, h=3.4,
                          wall=[0.92, 0.92, 0.90], roof=[0.95, 0.95, 0.94]))
    print('barn %.0f m from water; moorage at %s' % (dwb, mo.round(1)))
lot_dims = []
if LID == 'saltwhite':
    WALLS = [[0.953, 0.937, 0.894], [0.914, 0.878, 0.800]]          # #f3efe4 white stucco, #e9e0cc ivory
    ROOFS = [[0.557, 0.576, 0.588], [0.902, 0.894, 0.871]]          # #8e9396 grey tile, #e6e4de white tile
    lr = np.random.default_rng(1733)
    for i, c in enumerate(lots):
        yaw, dw = face_water(c)
        w_ = float(lr.uniform(26, 34)); d_ = float(lr.uniform(15, 19)); two = i % 3 != 1
        lot_dims.append((yaw, w_, d_))
        buildings.append(dict(kind='estate', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw, 1), w=round(w_, 1), d=round(d_, 1), h=7.0 if two else 4.0,
                              wall=WALLS[i % 2], roof=ROOFS[(i // 2) % 2]))
    # short timber docks on six lots (two with a boat lift), the community dock with a covered lift near the SE end
    for i in (0, 2, 4, 7, 9, 11):
        c = lots[i]; j = np.argmin(np.hypot(*(shore - c).T)); q_ = shore[j]
        yaw, _ = face_water(c); v_ = q_ - c; yaw = math.degrees(math.atan2(v_[0], v_[1]))
        buildings.append(dict(kind='pier', x=float(q_[0]), z=float(q_[1]), y=0.0, yaw=round(yaw, 1), w=9.0, d=1.8, h=1.0 if i in (4, 9) else 0.0, wall=[0.9, 0.9, 0.9], roof=[0.95, 0.95, 0.94]))
    cd_ = LP(210, 70)[0]; j = np.argmin(np.hypot(*(shore - cd_).T)); q_ = shore[j]; v_ = q_ - cd_
    buildings.append(dict(kind='commdock', x=float(q_[0]), z=float(q_[1]), y=0.0, yaw=round(math.degrees(math.atan2(v_[0], v_[1])), 1), w=14.0, d=2.4, h=1.0, wall=[0.9, 0.9, 0.9], roof=[0.96, 0.96, 0.95]))
    # tournament dressing on the SE lawn (generic, no logos): tents, two-storey judges' tower, scoreboard, inflatable arch
    for al_, p_, sz in ((238, 80, 6.0), (252, 70, 6.0), (264, 60, 3.0)):
        c = LP(al_, p_)[0]; yaw, _ = face_water(c)
        buildings.append(dict(kind='tent', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw, 1), w=sz, d=sz if sz < 5 else 4.5, h=2.4, wall=[0.96, 0.96, 0.95], roof=[0.97, 0.97, 0.96]))
    c = LP(192, 58)[0]; yaw, dwt = face_water(c)
    buildings.append(dict(kind='tower', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw, 1), w=4.2, d=4.2, h=7.6, wall=[0.95, 0.95, 0.93], roof=[0.60, 0.62, 0.64]))
    c = LP(222, 86)[0]; yaw, _ = face_water(c)
    buildings.append(dict(kind='scoreboard', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw, 1), w=4.4, d=0.3, h=4.6, wall=[0.08, 0.08, 0.09], roof=[0.95, 0.95, 0.95]))
    c = LP(276, 56)[0]; yaw, _ = face_water(c)
    buildings.append(dict(kind='arch', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw + 90, 1), w=9.0, d=0.9, h=5.0, wall=[0.95, 0.95, 0.96], roof=[0.18, 0.36, 0.62]))
    print('saltwhite: %d estate lots, %d docks, judges tower %.0f m from the water' % (len(lots), sum(b['kind'] in ('pier', 'commdock') for b in buildings), dwt))
if LID == 'villaserena':
    # single-storey Mediterranean villas, cream stucco, terracotta barrel-tile hip roofs (#c8714e, brief [DA]); one white
    # two-storey farmhouse-style house with a grey metal roof as the one-off; short docks with covered lifts on the west lots
    WALLS = [[0.945, 0.918, 0.847], [0.910, 0.863, 0.753], [0.890, 0.894, 0.875]]      # #f1ead8, #e8dcc0, #e3e4df
    TERRA = [0.784, 0.443, 0.306]                                                       # #c8714e
    lr = np.random.default_rng(1844)
    for i, c in enumerate(lots):
        yaw, dw = face_water(c)
        if i < 12 or True:
            # east-lake villas face the east lake (face_water uses the main shore): aim them at the nearest OTHER water instead
            if i >= 12 and OTHER:
                q_ = min((q for o_ in OTHER for q in o_), key=lambda q: np.hypot(*(q - c))); v_ = q_ - c; yaw = math.degrees(math.atan2(v_[0], v_[1]))
        one_off = i == 6
        w_ = float(lr.uniform(20, 25)); d_ = float(lr.uniform(14, 17))
        lot_dims.append((yaw, w_, d_))
        buildings.append(dict(kind='estate', x=float(c[0]), z=float(c[1]), y=hgt(*c), yaw=round(yaw, 1), w=round(w_, 1), d=round(d_, 1),
                              h=7.0 if one_off else 4.0, wall=[0.95, 0.95, 0.94] if one_off else WALLS[i % 3], roof=[0.42, 0.44, 0.46] if one_off else TERRA))
    for i, c in enumerate(lots[:12]):
        j = np.argmin(np.hypot(*(shore - c).T)); q_ = shore[j]; v_ = q_ - c; yaw = math.degrees(math.atan2(v_[0], v_[1]))
        buildings.append(dict(kind='pier', x=float(q_[0]), z=float(q_[1]), y=0.0, yaw=round(yaw, 1), w=6.0, d=1.6, h=1.0, wall=[0.9, 0.9, 0.9], roof=[0.20, 0.21, 0.22]))
    print('villaserena: %d villas (%d west, %d east lake), 12 docks with covered lifts' % (len(lots), len(LOTS), len(EAST)))
if LID == 'tallpine':
    yaw, _ = face_water(boathouse)
    buildings.append(dict(kind='boathouse', x=float(boathouse[0]), z=float(boathouse[1]), y=0.0, yaw=round(yaw, 1), w=22.0, d=10.0, h=6.2, wall=[0.95, 0.95, 0.93], roof=[0.36, 0.37, 0.38]))
    fp = boathouse + np.array([19.0, -2.0]); j = np.argmin(np.hypot(*(shore - fp).T)); q_ = shore[j]
    buildings.append(dict(kind='pier', x=float(q_[0]), z=float(q_[1]), y=0.0, yaw=round(yaw, 1), w=8.0, d=1.6, h=1.0, wall=[0.9, 0.9, 0.9], roof=[0.95, 0.95, 0.94]))
    yaw, _ = face_water(club)
    buildings.append(dict(kind='farmhouse', x=float(club[0]), z=float(club[1]), y=hgt(*club), yaw=round(yaw, 1), w=16.0, d=10.0, h=6.0, wall=[0.93, 0.92, 0.88], roof=[0.30, 0.31, 0.33]))
    buildings.append(dict(kind='shed', x=float(service[0]), z=float(service[1]), y=hgt(*service), yaw=round(yaw, 1), w=14.0, d=9.0, h=4.2, wall=[0.86, 0.85, 0.80], roof=[0.92, 0.92, 0.90]))
    print('tallpine: boathouse at %s, clubhouse, service shed' % boathouse.round(1))
if LID == 'heronmarsh':
    # one short timber dock with a white covered lift on the north bank (mid channel), two small white utility sheds on the verge
    for al_, kind_ in ((-60.0, 'pier'), (-20.0, 'shed'), (40.0, 'shed')):
        c0 = W(al_, -60.0)[0]
        j = np.argmin(np.hypot(*(shore - c0).T) + 1e3 * (AL(shore)[:, 1] > 0)); q_ = shore[j]
        v_ = q_ - (q_ - R_ * 10); yaw = math.degrees(math.atan2(R_[0], R_[1]))
        if kind_ == 'pier': buildings.append(dict(kind='pier', x=float(q_[0]), z=float(q_[1]), y=0.0, yaw=round(yaw, 1), w=7.0, d=1.8, h=1.0, wall=[0.9, 0.9, 0.9], roof=[0.96, 0.96, 0.95]))
        else:
            c_ = q_ - R_ * 7.0; buildings.append(dict(kind='shed', x=float(c_[0]), z=float(c_[1]), y=hgt(*c_), yaw=round(yaw, 1), w=4.0, d=3.2, h=2.6, wall=[0.93, 0.93, 0.91], roof=[0.90, 0.90, 0.88]))
# ---------- pass 19: Christopher's regional kits (Assets/SkiView/Kits/<kit>/, README pivots: street = Z 0 at the street edge, house
# extends to +Z (+Z faces the lake); shore = Z 0 at the shoreline, Y 0 = bank top, design water at Y -0.45 -> placed at y 0.45) ----------
import re
KIT_ROOT = '/workspace/skiview-unity/Assets/SkiView/Kits/'
KIT_DIRS = {'desert': 'desert', 'gulf': 'gulf', 'med': 'mediterranean', 'se': 'southeast-midwest', 'tour': 'tournament'}
_KI = {}
def kit_info(piece):
    if piece not in _KI:
        kd = KIT_DIRS[piece.split('_')[0]]
        for line in open(KIT_ROOT + kd + '/README.txt'):
            if line.startswith('| `%s.fbx`' % piece):
                cells = [c.strip() for c in line.split('|')]
                sz = next(tuple(float(v_) for v_ in m_.groups()) for m_ in (re.match(r'([\d.]+) x ([\d.]+) x ([\d.]+)$', c) for c in cells) if m_)
                _KI[piece] = (kd, sz, 'shore' if 'shore' in cells else 'street')
                break
        else: raise SystemExit('kit piece not in README: ' + piece)
    return _KI[piece]
def KIT(piece, at, face, mirror=False, back=0.0):
    """at = footprint centre (street pieces) or the shoreline point (shore pieces); face = unit vector toward the lake.
    Returns the building dict (street pivot = footprint centre - face * depth / 2)."""
    kd, (sx, sz, sh), pv = kit_info(piece)
    f_ = np.asarray(face, float); f_ = f_ / (np.linalg.norm(f_) + 1e-9); at = np.asarray(at, float)
    piv = at - f_ * (sz / 2 if pv == 'street' else back)
    y_ = 0.45 if pv == 'shore' else hgt(*at)
    return dict(kind='kit', kit=kd, piece=piece, x=round(float(piv[0]), 2), z=round(float(piv[1]), 2), y=round(float(y_), 2),
                yaw=round(math.degrees(math.atan2(f_[0], f_[1])), 1), w=sx, d=sz, h=sh, wall=[1, 1, 1], roof=[1, 1, 1], mirror=bool(mirror), shore=pv == 'shore')
def _yawv(yaw): return np.array([math.sin(math.radians(yaw)), math.cos(math.radians(yaw))])
def kitify(bl):
    """swap the procedural stand-ins for kit pieces where the kit is better (README "Recommended placement")"""
    out_, nsw = [], 0
    for i, b in enumerate(bl):
        f_ = _yawv(b['yaw']); c_ = np.array([b['x'], b['z']]); k_ = b['kind']; nb = None
        if LID == 'saltwhite':
            if k_ == 'estate': nb = KIT('gulf_estate_2s' if b['h'] > 5 else 'gulf_estate_1s', c_, f_, mirror=i % 2 == 1)
            elif k_ == 'commdock': nb = KIT('gulf_dock_lift', c_, f_)
            elif k_ == 'tent': nb = KIT('tour_tent', c_, f_)
            elif k_ == 'tower': nb = KIT('tour_judges_tower', c_, f_)
            elif k_ == 'scoreboard': nb = KIT('tour_scoreboard', c_, f_)
            elif k_ == 'arch': nb = KIT('tour_arch', c_, _yawv(b['yaw'] - 90))
        elif LID == 'villaserena':
            if k_ == 'estate': nb = KIT('se_farmhouse_bnb' if b['h'] > 5 else ('med_villa_cage' if i % 2 == 0 else 'med_villa_tower'), c_, f_, mirror=i % 4 >= 2)
            elif k_ == 'pier': nb = KIT('med_dock_pergola_lift' if i % 4 == 1 else 'med_dock_lift', c_, f_)
        elif LID == 'tallpine':
            if k_ == 'boathouse':
                j = np.argmin(np.hypot(*(shore - c_).T)); nb = KIT('se_boathouse_2storey', shore[j] - f_ * 1.5, f_)
            elif k_ == 'pier' and any(o['kind'] == 'boathouse' for o in bl): nsw += 1; continue    # the boathouse kit has its own finger pier + lift
            elif k_ == 'farmhouse': nb = KIT('se_clubhouse', c_, f_)
            elif k_ == 'shed': nb = KIT('se_shed_white', c_, f_)
        elif LID == 'heronmarsh':
            if k_ == 'pier': nb = KIT('se_dock_lift_white', c_, f_)
            elif k_ == 'shed': nb = KIT('se_shed_white', c_, f_)
        if nb is not None: nsw += 1
        out_.append(nb if nb is not None else b)
    if nsw: print('%s: %d procedural pieces swapped for kit pieces' % (LID, nsw))
    return out_
if LID in ('saltwhite', 'villaserena', 'tallpine', 'heronmarsh'): buildings = kitify(buildings)
if LID in ('saguaro', 'harrowpond', 'gulfwind', 'marrowind'):
    for k_, (q_, n_, pc, mir) in enumerate(NEWLOTS):
        f_ = -n_   # toward the lake
        if LID == 'saguaro':
            if pc: buildings.append(KIT(pc, q_ + n_ * 36.0, f_, mirror=mir))
            buildings.append(KIT('desert_shade_dock' if k_ in (3, 15) else ('desert_dock_lift' if k_ == 9 else 'desert_float_dock'), q_ - f_ * 0.6, f_))
        elif LID == 'gulfwind':
            buildings.append(KIT(pc, q_ + n_ * 50.0, f_, mirror=mir))
            buildings.append(KIT('gulf_boathouse_deck', q_ - f_ * 0.8, f_, mirror=mir))
            for s_ in (-1, 1):
                if k_ % 3 == 0: buildings.append(KIT('gulf_seawall_10m', q_ + np.array([s_ * 12.0, 0.0]) - f_ * 0.4, f_))
        else:
            buildings.append(KIT(pc, q_ + n_ * 44.0, f_, mirror=mir))
            buildings.append(KIT(['med_longdock_gazebo', 'med_longdock_boathouse', 'med_longdock_gazebo'][k_ % 3], q_ - f_ * 0.6, f_))
            if k_ % 4 == 1: buildings.append(KIT('med_seawall_10m', q_ + np.array([0.0, 9.0]) - f_ * 0.3, f_))
    if LID == 'saguaro':
        q_, n_ = EXTRA['club']
        buildings.append(KIT('desert_modern_pavilion', q_ + n_ * 30.0, -n_))
        buildings.append(KIT('desert_ramada', q_ + n_ * 9.0 + np.array([n_[1], -n_[0]]) * 14.0, -n_))
        buildings.append(KIT('desert_ramada', q_ + n_ * 9.0 - np.array([n_[1], -n_[0]]) * 14.0, -n_))
    elif LID == 'harrowpond':
        c_ = EXTRA['club']; f_ = R_                                 # clubhouse faces WSW over the east channel
        buildings.append(KIT('se_clubhouse_harrow', c_, f_))
        buildings.append(KIT('se_shed_white', c_ + F * 26.0 - R_ * 6.0, f_))
        buildings.append(KIT('se_dock_launch', EXTRA['ebank'] + R_ * 0.5, f_))
        buildings.append(KIT('tour_judges_tower', EXTRA['judges'], -R_))
        buildings.append(KIT('tour_start_dock', EXTRA['wbank'] - R_ * 0.5 - F * 40.0, -R_))
        q_, n_ = EXTRA['spit']; buildings.append(KIT('se_boathouse_lift', q_ - n_ * 0.6, -n_)); buildings.append(KIT('se_dock_lift_white', q_ + np.array([n_[1], -n_[0]]) * 18.0 - n_ * 0.6, -n_))
        for b_ in EXTRA['bridges']:
            yaw = math.degrees(math.atan2(EXTRA['hwy_dir'][0], EXTRA['hwy_dir'][1]))
            buildings.append(dict(kind='bridge', x=float(b_[0]), z=float(b_[1]), y=5.2, yaw=round(yaw, 1), w=22.0, d=60.0, h=5.2, wall=[0.66, 0.66, 0.64], roof=[0.34, 0.35, 0.36]))
    elif LID == 'gulfwind':
        buildings.append(KIT('tour_start_dock', W(-200, 0)[0] + R_ * 0, -R_))
        # start dock on the N bank at the west pre-gate: snap to the bank
        b_ = buildings[-1]; c_ = np.array([b_['x'], b_['z']])
        j = np.argmin(np.hypot(*(shore - c_).T) + 1e3 * ((shore @ R_) > 0)); buildings[-1] = KIT('tour_start_dock', shore[j] + R_ * 0.5, R_)
        for k_, (x_, z_) in enumerate(((-1000.0, 150.0), (-960.0, 175.0), (-1060.0, 190.0))):   # distant industrial sheds NW (low grey roofs)
            buildings.append(dict(kind='shed', x=x_, z=z_, y=hgt(x_, z_), yaw=0.0, w=36.0 - k_ * 6, d=22.0, h=6.0, wall=[0.70, 0.71, 0.72], roof=[0.62, 0.64, 0.66]))
    else:
        pq = EXTRA['parkShore']
        if len(pq):
            q0 = pq[len(pq) // 2]; j = np.argmin(np.hypot(*(SR - q0).T)); n0 = SN[j]; t0 = np.array([n0[1], -n0[0]])
            c_ = q0 + n0 * 22.0; yaw = math.degrees(math.atan2(-n0[0], -n0[1]))
            buildings.append(dict(kind='grandstand', x=float(c_[0]), z=float(c_[1]), y=hgt(*c_), yaw=round(yaw, 1), w=48.0, d=14.0, h=7.0, wall=[0.92, 0.92, 0.90], roof=[0.20, 0.42, 0.56]))
            bw = q0 - n0 * 45.0; buildings.append(dict(kind='breakwater', x=float(bw[0]), z=float(bw[1]), y=0.0, yaw=round(yaw + 90, 1), w=1.6, d=90.0, h=0.4, wall=[0.95, 0.95, 0.94], roof=[0.92, 0.52, 0.22]))
            for k_, s_ in enumerate((-60.0, 60.0)):
                p_ = q0 + n0 * 40.0 + t0 * s_; buildings.append(dict(kind='shed', x=float(p_[0]), z=float(p_[1]), y=hgt(*p_), yaw=round(yaw, 1), w=16.0, d=10.0, h=4.0, wall=[0.95, 0.94, 0.90], roof=[0.86, 0.50, 0.36]))
            sp = q0 + n0 * 160.0 + t0 * 40.0
            buildings.append(dict(kind='spire', x=float(sp[0]), z=float(sp[1]), y=hgt(*sp), yaw=0.0, w=6.0, d=6.0, h=72.0, wall=[0.86, 0.86, 0.84], roof=[0.80, 0.30, 0.22]))
    print('%s: %d buildings (%d kit pieces), lots %d' % (LID, len(buildings), sum(b['kind'] == 'kit' for b in buildings), len(NEWLOTS)))
riprap = []
if LID == 'cedarbend':
    for q in chaikin(slip, 1):
        pass
    # rocks along the slip mouth and both slip walls (short riprap patches at the moorage)
    for a_ in np.arange(MOOR_A0 - 8, MOOR_A1 + 8, 1.2):
        for l_ in (MOOR_M + 1, MOOR_M):
            p = W(a_, l_)[0]
            if sample(water, *p) == 0 or sample(dist_in, *p) < 1.6: riprap += [round(float(p[0]), 2), round(float(p[1]), 2)]
    for l_ in np.arange(MOOR_M + 1, MOOR_P, 1.2):
        for a_ in (MOOR_A0 - 0.6, MOOR_A1 + 0.6):
            p = W(a_, l_)[0]; riprap += [round(float(p[0]), 2), round(float(p[1]), 2)]
# ---------- trees: [x, y, z, scale, kind] ----------
# kind 0 conifer wall (< 40 m from the open edge), 1 broadleaf (alder / bigleaf maple), 2 lawn specimen fir, 3 shrub (islands),
# 4 interior forest, 5 tall-grass clump, 6 reed clump, 7 sword fern, 8 blackberry bramble, 9 black cottonwood.
# Pass 16: a continuous multi-layer PNW canopy (no tan gaps from the air): denser everywhere, small clearings only, and
# understory layers (ferns under the edge, blackberry along the lawn edges, reeds / unmowed grass at the water).
trees = []
def TT(x, z, sc, k, y=None): trees.append([round(float(x), 1), round(hgt(x, z) if y is None else y, 2), round(float(z), 1), round(float(sc), 2), k])
if LID == 'tallpine':
    # kinds: 10 coast redwood (windbreak row), 0 low conifer row (south bank, scale < 1 = 12-20 m), 4 far-band pine, 1 willow (islands,
    # clubhouse), 5 straw tufts on the field edges
    # the two bank rows walk the water edge: redwoods every ~6.5 m, 17 m back (behind the bank road), a staggered second row at 22 m;
    # the south conifer row every ~5 m, 3.5 m back. Then orchard blocks in the straw farmland.
    sr = resample(shore, 1.0); nrm = []
    for i in range(len(sr)):
        t_ = sr[(i + 1) % len(sr)] - sr[i - 1]; n_ = np.array([t_[1], -t_[0]]); n_ /= np.linalg.norm(n_) + 1e-9
        if sample(water, *(sr[i] + n_ * 2.0)): n_ = -n_          # outward (away from the water)
        nrm.append(n_)
    nrm = np.array(nrm); acc_ = 0.0
    for i in range(len(sr)):
        a_, p_ = sr[i] @ fS, sr[i] @ R_
        if abs(a_) > 340: continue
        if p_ < 0 and i % 6 == 0:
            for off, ph in ((17.0, 0.0), (22.5, 3.0)):
                if off > 20 and rng.random() < 0.5: continue          # pass 19: lighter canopy - the staggered second row is half as dense
                q_ = sr[i] + nrm[i] * (off + rng.normal(0, 0.8)) + np.array([nrm[i][1], -nrm[i][0]]) * ph
                if not sample(water, *q_) and not sample(gravel, *q_) and not sample(houseYard, *q_): TT(q_[0], q_[1], rng.uniform(0.9, 1.15), 10)
        if p_ > 0 and i % 5 == 0:
            q_ = sr[i] + nrm[i] * (3.5 + rng.normal(0, 0.6))
            if not sample(water, *q_): TT(q_[0], q_[1], rng.uniform(0.6, 0.85), 19) if rng.random() < 0.5 else TT(q_[0], q_[1], rng.uniform(0.75, 1.0), 15)   # pass 19: south bank - red cedar / maple (vegetation pack)
    for (a0, p0, na, np_) in ((-120, 160, 22, 9), (140, 190, 18, 8), (-60, -140, 20, 6)):
        for i_ in range(na):
            for j_ in range(np_):
                q_ = W(a0 + i_ * 6.0, p0 + j_ * 7.0 * np.sign(p0))[0]
                if not sample(water, *q_) and not sample(houseYard, *q_) and not sample(gravel, *q_): TT(q_[0], q_[1], rng.uniform(0.30, 0.40), 1)
    for x, z in rng.uniform(-560, 560, (220000, 2)):
        if sample(water, x, z) or sample(gravel, x, z) or sample(houseYard, x, z) or sample(islands, x, z): continue
        lo = sample(dist_out, x, z); a_ = x * fS[0] + z * fS[1]; p_ = x * R_[0] + z * R_[1]
        if lo > 380 and rng.random() < 0.012: TT(x, z, rng.uniform(0.5, 0.9), 4)                                               # far pines
        elif 70 < lo < 380 and math.sin(x * 0.021 + 1.3) * math.cos(z * 0.017 - 0.7) > 0.45 and rng.random() < 0.010: TT(x, z, rng.uniform(0.7, 1.0), 4)   # pass 19: loblolly stands behind the banks
        elif 21 < lo < 60 and p_ < 0 and rng.random() < 0.004: TT(x, z, rng.uniform(0.8, 1.05), 15)
    for g_ in isl:
        c = g_.mean(0); TT(c[0], c[1], 0.9, 17)                       # pass 19: one weeping willow per island
    c = club + np.array([14.0, -8.0]); TT(c[0], c[1], 1.0, 17); TT(c[0] - 22, c[1] + 4, 0.9, 17)
elif LID == 'heronmarsh':
    # kinds: 11 live oak with Spanish moss (bank band, crowns over the water), 4 loblolly pine behind, 7 low fronds (palmetto
    # stand-in) under the band, 6 reeds in a few spots
    for x, z in rng.uniform(-620, 620, (260000, 2)):
        if sample(water, x, z) or sample(gravel, x, z) or sample(lawn, x, z): continue
        lo = sample(dist_out, x, z)
        if sample(islands, x, z):
            if rng.random() < 0.02: TT(x, z, rng.uniform(0.6, 0.9), 11)
            continue
        if lo < 26 and rng.random() < (0.14 if lo < 4 else 0.09): TT(x, z, rng.uniform(0.85, 1.25), 11)
        elif 18 < lo < 420 and rng.random() < 0.07: TT(x, z, rng.uniform(0.9, 1.3), 4)
        elif lo < 40 and rng.random() < 0.15: TT(x, z, rng.uniform(0.7, 1.2), 14)   # pass 19: saw palmetto (vegetation pack; fern without it)
    for q_ in shore[::2]:
        if sample(fringe_n, *q_) > 0.4 and rng.random() < 0.3: o = rng.normal(0, 0.8, 2); TT(q_[0] + o[0], q_[1] + o[1], rng.uniform(0.5, 1.0), 6, y=max(0.1, hgt(*(q_ + o))) - 0.1)
elif LID in ('saguaro', 'harrowpond', 'gulfwind', 'marrowind'):
    blocked = water | gravel | houseYard
    def free(x, z): return not (sample(blocked, x, z) or sample(islands, x, z))
    X0, X1, Z0, Z1 = CX - HALF + 5, CX + HALF - 5, CZ - HALF + 5, CZ + HALF - 5
    cand = np.column_stack([rng.uniform(X0, X1, 240000), rng.uniform(Z0, Z1, 240000)])
    if LID == 'saguaro':
        # desert set kinds: 0 mesquite, 1 palo verde, 2 fan palm, 3 saguaro, 4 creosote / low shrub
        med = EXTRA['median']
        for x, z in cand:
            if not free(x, z) or sample(lawn, x, z): continue
            lo = sample(dist_out, x, z)
            if sample(med, x, z):
                if lo > 4 and rng.random() < (0.10 if z < -150 else 0.05): TT(x, z, rng.uniform(0.8, 1.2), 0 if rng.random() < 0.6 else 1)
                continue
            if lo < 9: continue
            r_ = rng.random()
            if x < -90 and r_ < 0.006: TT(x, z, rng.uniform(0.7, 1.25), 3)
            elif r_ < 0.03: TT(x, z, rng.uniform(0.5, 1.0), 4)
            elif z > 600 and x > 220 and r_ < 0.2: TT(x, z, rng.uniform(0.9, 1.3), 0)            # mesquite / tamarisk belt NE
            elif lo > 200 and hgt(x, z) > 8.0 and r_ < 0.06: TT(x, z, rng.uniform(0.6, 1.0), 7)   # pass 19: ponderosa on the higher ground (not the lots)
        for k_, g_ in enumerate(isl):
            if k_ != 1: c = g_.mean(0); TT(c[0], c[1], rng.uniform(0.9, 1.1), 2)
        for q_, n_, pc, mir in NEWLOTS:
            if pc:
                t_ = np.array([n_[1], -n_[0]])
                for s_ in (-1, 1):
                    p_ = q_ + n_ * 18.0 + t_ * s_ * rng.uniform(13, 17); TT(p_[0], p_[1], rng.uniform(0.7, 1.0), 3 if s_ > 0 else 1)
                    for j in range(3): p_ = q_ + n_ * rng.uniform(8, 14) + t_ * s_ * rng.uniform(4, 15); TT(p_[0], p_[1], rng.uniform(0.8, 1.1), 6) if j == 0 else TT(p_[0], p_[1], rng.uniform(0.35, 0.55), 4)   # ocotillo + creosote
            else:
                p_ = q_ + n_ * rng.uniform(14, 20); TT(p_[0], p_[1], rng.uniform(0.85, 1.1), 5)   # pass 19: a date palm on the other lots
    elif LID == 'harrowpond':
        berm = EXTRA['berm']
        for x, z in cand:
            if not free(x, z) or sample(lawn, x, z): continue
            lo = sample(dist_out, x, z)
            if sample(berm, x, z):
                if rng.random() < 0.5: TT(x, z, rng.uniform(0.55, 0.8), (15 if rng.random() < 0.5 else 16) if rng.random() < 0.8 else 8)
                continue
            if lo < 3: continue
            r_ = rng.random()
            if r_ < 0.22: TT(x, z, rng.uniform(0.8, 1.2), 15 if rng.random() < 0.5 else 16)   # pass 19: maple / oak hardwoods (vegetation pack)
            elif r_ < 0.26: TT(x, z, rng.uniform(0.8, 1.2), 4)
            elif r_ < 0.28: TT(x, z, rng.uniform(0.8, 1.1), 16)
        # weeping-willow stand-ins (cottonwood, low and wide) at the water by the club lawns; shade trees on the lawns
        for i_ in range(0, len(SR), 5):   # pass 19: weeping willows along the water's edge, 3-7 m inland (crowns over the water)
            p_ = SR[i_] + SN[i_] * rng.uniform(3.0, 7.0)
            if not sample(water, *p_) and not sample(houseYard, *p_) and not sample(gravel, *p_) and rng.random() < 0.28: TT(p_[0], p_[1], rng.uniform(0.85, 1.15), 17)
        c_ = EXTRA['club']
        for a_ in rng.uniform(0, 2 * np.pi, 6):
            p_ = c_ + np.array([math.cos(a_), math.sin(a_)]) * rng.uniform(26, 45)
            if not sample(water, *p_) and not sample(houseYard, *p_): TT(p_[0], p_[1], rng.uniform(0.85, 1.1), 15)
        for q_ in SR[::2]:
            if (q_ @ R_) < float(np.array([138.7, -10.1]) @ R_) - 10 and sample(fringe_n, *q_) > 0.35 and rng.random() < 0.4:
                o = rng.normal(0, 0.8, 2); TT(q_[0] + o[0], q_[1] + o[1], rng.uniform(0.5, 0.9), 6, y=0.05)
    elif LID == 'gulfwind':
        south = EXTRA['south']
        for x, z in cand:
            if not free(x, z): continue
            lo = sample(dist_out, x, z)
            if not sample(south, x, z):
                if lo > 25 + 12 * sample(fringe_n, x, z) + 6:
                    r_ = rng.random()
                    if r_ < 0.11: TT(x, z, rng.uniform(0.8, 1.2), 11)
                    elif r_ < 0.22: TT(x, z, rng.uniform(0.8, 1.2), 16)
                    elif r_ < 0.25: TT(x, z, rng.uniform(0.9, 1.2), 4)
                    elif r_ < 0.33: TT(x, z, rng.uniform(0.7, 1.1), 14)
                    elif r_ < 0.335: TT(x, z, rng.uniform(0.85, 1.1), 13)
            elif sample(landS, x, z) > 90 and rng.random() < 0.0025: TT(x, z, rng.uniform(0.8, 1.1), 11)    # pasture oaks
        for q_, n_, pc, mir in NEWLOTS:
            for j in range(int(rng.integers(1, 4))):
                p_ = q_ + n_ * rng.uniform(64, 76) + np.array([1.0, 0.0]) * rng.uniform(-26, 26)
                if free(*p_): TT(p_[0], p_[1], rng.uniform(0.85, 1.1), 18 if j == 0 else 13)   # pass 19: young live oak + sabal palms on the lots
        for k_, g_ in enumerate(isl): c = g_.mean(0); TT(c[0], c[1], rng.uniform(0.85, 1.05), 18 if k_ % 2 == 0 else 13)   # one oak or one palm per island
    else:
        park = EXTRA['park']
        for x, z in cand:
            if not free(x, z): continue
            lo = sample(dist_out, x, z)
            if sample(lawn, x, z):
                if sample(park, x, z) and rng.random() < 0.012: TT(x, z, rng.uniform(1.0, 1.4), 11)
                elif sample(park, x, z) and rng.random() < 0.02: TT(x, z, rng.uniform(0.5, 0.8), 14)
                elif sample(park, x, z) and rng.random() < 0.008: TT(x, z, rng.uniform(0.85, 1.1), 13)
                continue
            r_ = rng.random()
            if r_ < 0.10: TT(x, z, rng.uniform(0.85, 1.25), 11)
            elif r_ < 0.15: TT(x, z, rng.uniform(0.9, 1.3), 4)
            elif r_ < 0.22: TT(x, z, rng.uniform(0.6, 1.0), 14)
            elif r_ < 0.226: TT(x, z, rng.uniform(0.85, 1.1), 13)
        # bald-cypress fringe standing in the shallows (moss-hung; live-oak stand-in) + maidencane / sawgrass reeds in front, gaps at the docks
        dockp = np.array([[b['x'], b['z']] for b in buildings if b['kind'] == 'kit' and b.get('shore')] or [[1e9, 1e9]])
        for i, q_ in enumerate(SR):
            if not (CX - HALF < q_[0] < CX + HALF and CZ - HALF < q_[1] < CZ + HALF): continue
            if np.hypot(*(dockp - q_).T).min() < 12: continue
            if i % 3 == 0 and sample(fringe_n, *q_) > -0.2:
                p_ = q_ - SN[i] * rng.uniform(1.5, 5); TT(p_[0], p_[1], rng.uniform(0.9, 1.15), 12, y=0.1)   # pass 19: bald cypress (vegetation pack)
            for j in range(2):
                p_ = q_ - SN[i] * rng.uniform(4, 10) + np.array([SN[i][1], -SN[i][0]]) * rng.uniform(-1.5, 1.5)
                if rng.random() < 0.5: TT(p_[0], p_[1], rng.uniform(0.6, 1.0), 6, y=0.05)
elif STYLE == 'pnw':
    open_d = ndimage.distance_transform_edt(~(lawn | water)) * PX   # metres into the forest from the open lawn / water edge
    lowN = fbm(3, 12, 31)
    cand = rng.uniform(-795, 795, (96000, 2))
    for x, z in cand:
        lo = sample(dist_out, x, z)
        if sample(water, x, z) or lo < 4: continue
        if sample(gravel, x, z) or sample(houseYard, x, z): continue
        a_ = x * fS[0] + z * fS[1]; p_ = x * R_[0] + z * R_[1]
        lw = sample(lawn, x, z)
        k = 0; s = rng.uniform(0.75, 1.3)
        if sample(islands, x, z): continue
        if lw:
            # open fairway: a few broadleaf and specimen firs, none within 14 m of the water on the course reach
            if lo < 14 and abs(a_) < 260: continue
            if rng.random() > 0.014: continue
            k = 1 if rng.random() < 0.6 else 2
        else:
            od = sample(open_d, x, z)
            p = 0.97 if lo < 380 else (0.80 if lo < 620 else 0.5)
            if rng.random() > p: continue
            k = 0 if od < 40 else 4
            west_strip = p_ < 0 and lo < 80
            r = rng.random()
            if west_strip and r < 0.45: k = 9 if r < 0.15 else 1            # cottonwood / alder / maple along the road strip
            elif od < 25 and r < 0.16: k = 1                                    # broadleaf fringe at the forest edge
            elif sample(h, x, z) < 4.5 and lo < 160 and r < 0.12: k = 9         # cottonwood on the low ground near the water
            elif k == 4 and sample(lowN, x, z) > 0.55 and r < 0.35: k = 1       # alder / maple patches in the conifer forest
        trees.append([round(float(x), 1), round(hgt(x, z), 2), round(float(z), 1), round(float(s), 2), k])
    nmain = len(trees)
    # understory along the open edges (lawn / water side of the forest): ferns 3-35 m in, blackberry in the first 4 m
    for x, z in rng.uniform(-560, 560, (260000, 2)):
        if sample(water, x, z) or sample(lawn, x, z) or sample(gravel, x, z) or sample(houseYard, x, z) or sample(islands, x, z): continue
        od = sample(open_d, x, z)
        if od < 4.5 and rng.random() < 0.5: trees.append([round(float(x), 1), round(hgt(x, z), 2), round(float(z), 1), round(float(rng.uniform(0.7, 1.3)), 2), 8])
        elif 3 < od < 35 and rng.random() < 0.12: trees.append([round(float(x), 1), round(hgt(x, z), 2), round(float(z), 1), round(float(rng.uniform(0.7, 1.4)), 2), 7])
    # unmowed grass clumps on the unmowed strips; reeds in broken runs at the waterline (not in the moorage slip)
    for x, z in rng.uniform(-520, 520, (320000, 2)):
        if sample(water, x, z) or sample(gravel, x, z): continue
        a_ = x * fS[0] + z * fS[1]; p_ = x * R_[0] + z * R_[1]
        if MOOR_A0 - 10 < a_ < MOOR_A1 + 10 and p_ > 20: continue
        lo = sample(dist_out, x, z)
        if sample(unmowed, x, z) and rng.random() < 0.35:
            trees.append([round(float(x), 1), round(hgt(x, z), 2), round(float(z), 1), round(float(rng.uniform(0.7, 1.3)), 2), 5])
    # reeds: walk the shoreline every 1.3 m, broken runs where the fringe noise is high (none in the slip)
    for q_ in shore[::1]:
        a_ = q_ @ fS; p_ = q_ @ R_
        if MOOR_A0 - 10 < a_ < MOOR_A1 + 10 and p_ > 20: continue
        if sample(fringe_n, *q_) > 0.12 and rng.random() < 0.38:          # pass 16 r2: sparser, irregular (r1 read as a fence)
            o = rng.normal(0, 1.0, 2); x, z = q_ + o
            trees.append([round(float(x), 1), round(max(0.1, hgt(x, z)) - 0.1, 2), round(float(z), 1), round(float(rng.uniform(0.5, 1.25)), 2), 6])
    # islands: one small alder + shrubs on the north island, shrubs on the south one
    for g, poly, nt in ((Nn, isl[0], 1), (Ss, isl[1], 0)):
        c = poly.mean(0)
        for j in range(nt): trees.append([round(float(c[0] + 2), 1), round(hgt(*c), 2), round(float(c[1] - 1), 1), 0.55, 1])
        for j in range(3 if nt else 2):
            o = rng.normal(0, 2.0, 2); trees.append([round(float(c[0] + o[0]), 1), round(hgt(*c), 2), round(float(c[1] + o[1]), 1), round(float(rng.uniform(0.8, 1.3)), 2), 3])
        for q_ in poly[::2]:
            if rng.random() < 0.6: trees.append([round(float(q_[0]), 1), 0.15, round(float(q_[1]), 1), round(float(rng.uniform(0.7, 1.1)), 2), 6])
else:
    # pass 17 Gulf kinds: 0 slash pine (flatwoods), 1 slash pine on the lawns, 2 sabal palm, 3 royal palm, 4 saw palmetto,
    # 5 sea oats, 6 clipped hedge shrub
    def T(x, z, sc, k, y=None): trees.append([round(float(x), 1), round(hgt(x, z) if y is None else y, 2), round(float(z), 1), round(float(sc), 2), k])
    gapN = fbm(3, 10, 32)
    for x, z in rng.uniform(-795, 795, (78000, 2)):
        if sample(estate, x, z) or sample(water, x, z) or sample(gravel, x, z): continue
        lo = sample(dist_out, x, z)
        if lo < (2.5 if LID == 'villaserena' else 12): continue        # Villa Serena: the slash-pine wall grows to the water
        if sample(gapN, x, z) < -0.45 and LID != 'villaserena': continue                        # wet-prairie openings in the flatwoods
        if rng.random() > (0.62 if lo < 450 else 0.40): continue
        T(x, z, rng.uniform(0.8, 1.25) * (1.25 if LID == 'villaserena' and lo < 60 else 1.0), 0)
    for x, z in rng.uniform(-620, 620, (70000, 2)):
        if sample(estate, x, z) or sample(water, x, z) or sample(gravel, x, z): continue
        if sample(dist_out, x, z) > 420 or rng.random() > 0.55: continue
        T(x, z, rng.uniform(0.7, 1.3), 4)
    # palmetto + pines along the inside of the ring road (screen planting)
    for x, z in rng.uniform(-420, 420, (60000, 2)):
        if not sample(estate, x, z) or sample(gravel, x, z) or sample(houseYard, x, z) or sample(sand, x, z): continue
        lo = sample(dist_out, x, z)
        if lo < 16: continue
        e_d = sample(est_edge, x, z)
        if e_d < 14 and rng.random() < 0.30 and LID != 'villaserena': T(x, z, rng.uniform(0.75, 1.2), 0 if rng.random() < 0.45 else 4)
        elif rng.random() < 0.028: T(x, z, rng.uniform(0.7, 1.15), 1 if rng.random() < 0.72 else 2)   # scattered lawn pines / sabals
    # planted lots: royal palms flanking the entry, sabals by the lanai, a hedge along the front and sides
    for c, (yaw_, w_, d_) in zip(lots, lot_dims):
        fwd = np.array([math.sin(math.radians(yaw_)), math.cos(math.radians(yaw_))]); rt = np.array([fwd[1], -fwd[0]])
        for sx in (-1, 1):
            q_ = c - fwd * (d_ / 2 + 7) + rt * sx * 5.5; T(*q_, rng.uniform(0.9, 1.15), 3)
            q_ = c + fwd * (d_ / 2 + 12) + rt * sx * (w_ / 2 + 4 + rng.uniform(0, 4)); T(*q_, rng.uniform(0.8, 1.2), 2)
            q_ = c + rt * sx * (w_ / 2 + 6) + fwd * rng.uniform(-4, 4); T(*q_, rng.uniform(0.8, 1.2), 2)
            for t_ in np.arange(-d_ / 2, d_ / 2, 2.4): T(*(c + rt * sx * (w_ / 2 + 1.6) + fwd * t_), rng.uniform(0.45, 0.6), 6)
        for t_ in np.arange(-w_ / 2, w_ / 2, 2.4):
            if abs(t_) > 3.5: T(*(c - fwd * (d_ / 2 + 1.6) + rt * t_), rng.uniform(0.45, 0.6), 6)
        # a few sabals along the drive
        q0 = est_pts[np.argmin(np.hypot(*(est_pts - c).T))]
        for t_ in np.linspace(0.35, 0.85, 3): T(*(c + (q0 - c) * t_ + rng.normal(0, 1, 2) + rt * 4.5), rng.uniform(0.8, 1.2), 2 if rng.random() < 0.7 else 3)
    # islands: white sand with 2-3 palms and sea oats
    for k_, poly in enumerate(isl):
        c = poly.mean(0)
        for j in range(3 if k_ == 0 else 2):
            o = rng.normal(0, 3.0, 2); T(c[0] + o[0], c[1] + o[1], rng.uniform(0.85, 1.2), 2 if j else 3)
        for q_ in poly:
            if rng.random() < 0.55: o = (c - q_) / np.linalg.norm(c - q_) * rng.uniform(1.5, 4.5); T(q_[0] + o[0], q_[1] + o[1], rng.uniform(0.7, 1.1), 5)
    # sea oats in thin runs where the sand rim is widest, and round the community dock
    for q_ in (shore[::2] if LID == 'saltwhite' else []):
        if sample(fringe_n, *q_) > 0.35 and rng.random() < 0.25:
            x, z = q_ + rng.normal(0, 1.2, 2)
            if not sample(water, x, z) and sample(dist_out, x, z) > 1.5: T(x, z, rng.uniform(0.6, 1.0), 5)
print('trees', len(trees), 'by kind', np.bincount([t[4] for t in trees], minlength=12))
if STYLE == 'gulf': print('gulf: flatwoods pines %d, lawn pines %d, sabal %d, royal %d, palmetto %d, sea oats %d, hedge %d' % tuple(np.bincount([t[4] for t in trees], minlength=7)[:7]))

if RAMP:
    # jump fit check on the built water: ramp clearance, landing line (X axis + 12 m past... the skier lands ~15 m off the boat
    # lane on the ramp side) at 20/40/60/80 m past the top edge, water before the 210 m buoy
    lip = np.array(RAMP['lip']); fw = np.array(RAMP['fwd']); al0 = lip @ fw; side = (lip - fw * al0) / np.linalg.norm(lip - fw * al0)
    ramp_c = fw * al0 + side * RAMP_LAT - fw * 3.3
    land_c = [sample(dist_in, *(fw * (al0 + s_) + side * (RAMP_LAT - 3.0))) for s_ in (20, 40, 60, 80)]
    b210 = fw * (al0 - 3.3 - 10 - 210) + side * 7.0
    print('jump: ramp centre %.1f m to shore, landing line clearance %s m, 210 m buoy %.1f m to shore, boat lane 7 m off the line' % (
        sample(dist_in, *ramp_c), [round(float(c), 1) for c in land_c], sample(dist_in, *b210)))
# pass 19: lakes bigger than the terrain (Marrowind): the water grid is clamped to the terrain and a far-water ring carries on; the
# shore outside the terrain becomes a silhouette strip (treeline + house impostor blocks within 1 km of the play box)
TB = [CX - HALF, CX + HALF, CZ - HALF, CZ + HALF]
class _PIP:
    def __init__(self, poly): self.p = np.asarray(poly, float)
    def contains_points(self, pts):
        pts = np.atleast_2d(pts); x, y = pts[:, 0], pts[:, 1]; inside = np.zeros(len(pts), bool); P = self.p; n = len(P)
        for i in range(n):
            x1, y1 = P[i]; x2, y2 = P[(i + 1) % n]
            c = ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / (y2 - y1 + 1e-12) + x1); inside ^= c
        return inside
    def contains_point(self, q): return bool(self.contains_points(np.asarray(q)[None])[0])
_allw = np.vstack([shore] + list(OTHER))   # pass 19 punch list #1 (Villa Serena): the water grid must cover the east lake too
LB = [float(_allw[:, 0].min() - 15), float(_allw[:, 0].max() + 15), float(_allw[:, 1].min() - 15), float(_allw[:, 1].max() + 15)]
FAR_WATER = LB[0] < TB[0] - 30 or LB[1] > TB[1] + 30 or LB[2] < TB[2] - 30 or LB[3] > TB[3] + 30
farSil = []; farHouses = []
if FAR_WATER:
    LB = [max(LB[0], TB[0]), min(LB[1], TB[1]), max(LB[2], TB[2]), min(LB[3], TB[3])]
    fr = resample(shore, 20.0)
    _sp = _PIP(shore)
    def _box_d(q):
        if PLAY is None: return 1e9
        pb = np.array(PLAY); c0 = pb.mean(0); return max(0.0, float(np.hypot(*(q - c0))) - 300.0)
    for i, q_ in enumerate(fr):
        if TB[0] + 5 < q_[0] < TB[1] - 5 and TB[2] + 5 < q_[1] < TB[3] - 5: continue
        t_ = fr[(i + 1) % len(fr)] - fr[i - 1]; n_ = np.array([t_[1], -t_[0]]); n_ /= np.linalg.norm(n_) + 1e-9
        if _sp.contains_point(q_ + n_ * 8.0): n_ = -n_
        farSil += [round(float(q_[0]), 1), round(float(q_[1]), 1), round(float(n_[0]), 3), round(float(n_[1]), 3)]
        if i % 2 == 0 and 150 < _box_d(q_) < 1000:
            hq = q_ + n_ * 28.0; farHouses += [round(float(hq[0]), 1), round(float(hq[1]), 1), round(math.degrees(math.atan2(-n_[0], -n_[1])), 1)]
    print('far shore: %d silhouette points, %d house impostors, water grid clamped to the terrain' % (len(farSil) // 4, len(farHouses) // 3))
if PLAY is not None:
    inside = _PIP(np.array(PLAY)).contains_points(path)
    print('play box %s: boat path %.0f%% inside (%d of %d samples outside)' % (LID, 100 * inside.mean(), int((~inside).sum()), len(path)))
pal = json.load(open(PAL))['lakes'][LID]
boat = {'decal': 'swlm', 'deck': pal['deck'], 'band': pal['band'], 'accent': pal['accent'], 'livery': pal.get('livery', '#FFFFFF'),
        'accentMetallic': float(pal.get('accent_metallic', 0.0))}
venue = {'note': 'Generated by Tools/gen_venue_lake.py %s from roadmap/venue/lakes/%s (shore: %s). Unity frame: +X east, +Z north.' % (LID, os.path.basename(SRC), SHORE_SRC),
 'lake': {'id': LID, 'name': NAME, 'hasRamp': RAMP is not None, 'style': STYLE, 'trees': TREESET or '', 'boat': boat, 'water': WATER, 'mood': MOOD},
 'ramp': RAMP, 'rampFromSlalom': RAMP_LAT if RAMP else 18.5, 'jumpDir': JUMP_DIR if RAMP else 1, 'passWindowLate': PASS_LATE, 'jumpOnly': JUMP_ONLY,
 'aerialCentre': [float((shore[:, 0].min() + shore[:, 0].max()) / 2), float((shore[:, 1].min() + shore[:, 1].max()) / 2)],
 'terrain': {'size': TS, 'res': RES, 'ymin': YMIN, 'hrange': HRANGE, 'cx': CX, 'cz': CZ},
 'farWater': FAR_WATER, 'waterCell': 4.0 if FAR_WATER else 2.0, 'farSil': farSil, 'farHouses': farHouses,
 'playBox': [v_ for c_ in (PLAY or []) for v_ in c_],
 'course': {'fS': [float(fS[0]), float(fS[1])], 'length': 259, 'firstBuoy': 27, 'spacing': 41, 'buoyOffset': 11.5, 'guideOffset': 1.15, 'gateOffset': 1.25, 'preGate': 55},
 'path': {'step': 1.0, 'x': [round(float(a), 2) for a in px], 'z': [round(float(a), 2) for a in pz], 'v': [round(float(a), 2) for a in v],
          'along': [round(float(a), 2) for a in along], 'dir': [int(a) for a in sgn],
          'spin': [int(a) for a in (hv_sp if USE_HV else np.zeros(len(px), bool))]},
 'turns': {e_: dict(type=HV[e_]['turn_type'], direction=HV[e_]['direction'], source=HT_NAME) for e_ in HV} if USE_HV else {},
 'lakeBounds': LB,
 'islandCentres': [float(v_) for p in isl for v_ in p.mean(0)],
 'islandPolys': [round(float(v_), 2) for p in isl for q_ in p for v_ in q_], 'islandCounts': [len(p) for p in isl],
 'shore': [round(float(v_), 2) for p in shore for v_ in p],
 'buildings': buildings, 'riprap': riprap, 'treesFlat': [v_ for t in trees for v_ in t]}
json.dump(venue, open(OUT + 'venue.json', 'w'))
# preview (not shipped)
a = np.zeros((RES, RES, 3), np.uint8)
cG = [220, 213, 185] if STYLE == 'gulf' else [160, 150, 90]; cB = [120, 115, 80] if STYLE == 'gulf' else [25, 55, 30]
a[...] = (spl[..., 0:1] * [70, 140, 50] + spl[..., 1:2] * cG + spl[..., 2:3] * cB + spl[..., 3:4] * [150, 150, 145]).astype(np.uint8)
a[water] = [30, 60, 90]
im = Image.fromarray(a); dr = ImageDraw.Draw(im)
dr.line(to_px(path) + to_px(path[:1]), fill=(255, 255, 255), width=1)
for t in trees[::3]:
    q = to_px([(t[0], t[2])])[0]; dr.point(q, fill=(10, 30, 10) if t[4] not in (1, 2, 3) else (90, 120, 40))
for b in buildings:
    q = to_px([(b['x'], b['z'])])[0]; dr.rectangle([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], fill=(255, 0, 0))
os.makedirs('/tmp/p17', exist_ok=True); im.save('/tmp/p17/%s_preview.png' % LID)
print('wrote', OUT)
