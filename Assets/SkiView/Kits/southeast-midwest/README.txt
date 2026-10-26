# SKI VIEW regional kit: Southeast / Midwest lake

Lakes: heronmarsh, harrowpond, tallpine. Built Oct 18, 2026 to the same format as the v2 lot FBXs in `houses/` (see houses/README.md), following Holly's per-lake briefs in `waterski-game/roadmap/LAKE-LAYOUTS.md`.

## Assets

| FBX | what | tris LOD0 / LOD1 / LOD2 | size X x Z x H (m) | pivot | lakes | verify |
|---|---|---|---|---|---|---|
| `se_farmhouse_bnb.fbx` | 2-storey board-and-batten farmhouse, white, black standing-seam roof, wrap porch | 22,551 / 9,723 / 42 | 29.6 x 25.7 x 11.3 | street | harrowpond, heronmarsh | OK |
| `se_brick_ranch.fbx` | Brick ranch, hip shingle roof, raised lake deck, brick chimney | 23,922 / 10,370 / 42 | 35.6 x 24.3 x 5.7 | street | heronmarsh, harrowpond, tallpine | OK |
| `se_cottage_red.fbx` | 1.5-storey clapboard cottage, barn red, slate roof, dormers, front porch | 22,491 / 8,774 / 28 | 22.7 x 31.2 x 8.5 | street | harrowpond | OK |
| `se_suburban_sage.fbx` | 2-storey suburban, sage lap siding, grey shingle roof, garage, rear deck | 23,417 / 9,559 / 42 | 25.7 x 26.5 x 11.0 | street | heronmarsh | OK |
| `se_clubhouse.fbx` | Tallpine 2-storey clubhouse, cream board siding, white-railed lake deck | 23,260 / 9,525 / 42 | 27.0 x 26.6 x 10.7 | street | tallpine (any lake as a club building) | OK |
| `se_clubhouse_harrow.fbx` | Harrow Pond 2-storey clubhouse, grey-white board siding, brown shingle roof, lake deck | 24,320 / 9,710 / 42 | 27.0 x 29.6 x 10.7 | street | harrowpond | OK |
| `se_boathouse_lift.fbx` | Enclosed board-and-batten boathouse, green metal roof, hung lift + sun deck | 7,750 / 3,376 / 24 | 13.7 x 12.3 x 6.7 | shore | harrowpond (south end), heronmarsh, marrowind | OK |
| `se_dock_launch.fbx` | Timber launch dock with T platform (no lift) | 1,856 / 440 / 20 | 4.6 x 14.6 x 2.6 | shore | harrowpond (west bank, clubhouse, south spit) | OK |
| `se_dock_lift_white.fbx` | Heron Marsh short timber dock + white flat-canopy boat lift | 2,834 / 1,192 / 20 | 5.9 x 10.6 x 4.6 | shore | heronmarsh (north bank) | OK |
| `se_boathouse_2storey.fbx` | Tallpine two-storey covered dock: white columns, baluster upper deck, hip roof + finger pier lift | 22,430 / 9,307 / 24 | 30.3 x 11.5 x 10.1 | shore | tallpine | OK |
| `se_shed_white.fbx` | Small white utility shed (verge / service yard) | 1,112 / 412 / 14 | 3.7 x 5.1 x 3.0 | street | heronmarsh, harrowpond, tallpine | OK |
| `se_dock_lift_canopy.fbx` | 15 x 3 m timber dock + canopy boat lift | 2,856 / 1,006 / 20 | 7.2 x 16.6 x 4.8 | shore | tallpine (finger pier option), harrowpond, any lake | OK |

Houses are 20-25k tris at LOD0 like the v2 lots. Docks, seawalls, lifts and small structures are well under that by design (they have far less surface); they still have the full 3-LOD set.

## Format (same as v2 lots)

- One FBX per asset: root empty `<id>` with children `<id>_LOD0`, `<id>_LOD1`, `<id>_LOD2` (+ marker empties, below). Identity transforms everywhere, 1 unit = 1 m, Y up (UnitScaleFactor 100 in the file, imports at scale 1 in Unity with "Convert Units" on).
- LOD0 / LOD1: two materials, `kit_<id>_body` (albedo + OpenGL normal atlas, `textures/kit_<id>_albedo.jpg`, `_normal.jpg`) and `kit_<id>_glass` (dark tinted, smooth, OPAQUE; `_glass.jpg`). Pieces without windows carry only the body material.
- LOD2: one material `kit_<id>_impostor` (`_impostor.jpg`, 5 baked views of LOD0 on a box/roof hull).
- Suggested LODGroup transitions (as v2): LOD0 > 0.70, LOD1 > 0.28, LOD2 > 0.02, then culled.
- Axes: X runs along the shore; **+Z points to the lake**; Y up.
- **Pivots**: `street` = centred on the footprint in X, on the street-most edge of the asset (Z = 0), at street grade (Y = 0); everything extends toward +Z. `shore` = on the shoreline (Z = 0) at the bank top / grade (Y = 0), on the dock centreline; the dock runs out to +Z and the design water level is Y = -0.45 m.
- Marker empties (children of the root): `<id>_palm_NN` = palm / feature-tree spots at grade; `<id>_boat_slip` = centre of the boat lift / slip at water level; `<id>_skier_start` = skier spawn on the start dock.
- Grade: house pieces assume flat ground around the footprint; let terrain overlap the small plinth. Pools sit below grade (cut the terrain or keep it under the pool).

## Recommended placement per lake

- **Heron Marsh (§2 + heronmarsh_look_brief)**: no houses on the water. The two-storey vinyl houses (`se_suburban_sage`, `se_farmhouse_bnb`; grey #9a9d9b / white #eceae4 variants are a material swap) go in a row about 150 m back beyond the north trees, at 15-20 m pitch. On the water: one `se_dock_lift_white` (short timber dock + white flat-canopy lift) on the north bank, middle third of the channel, plus 1-2 `se_shed_white` on the north verge. The south condo blocks are not in this kit.
- **Harrow Pond (§6 + harrowpond_look_brief)**: `se_clubhouse_harrow` (two storeys, brown roof, deck) on the east-bank lawn of the east channel, with `se_shed_white` x1-2 nearby and a `se_dock_launch` at the clubhouse and on the south spit; `se_boathouse_lift` (green roof) at the south end; 0-3 cottages (`se_cottage_red`, `se_farmhouse_bnb`). One timber launch dock on the west bank. No lifts on the course docks. Judges tower: tournament kit.
- **Tallpine (§5 + tallpine_look_brief)**: no houses. `se_boathouse_2storey` (about 22 x 10 m, white columns, baluster upper deck, hip roof, includes the finger pier + lift on its +X side) at the waterline around v2 x 222-245; `se_clubhouse` as the clubhouse at the east end of the north bank; `se_shed_white` (white roof) 20-25 m behind the bank road as the service shed. `se_dock_lift_canopy` is a spare 15 x 3 m lift dock.
- `se_brick_ranch` is a Midwest filler for any background house row.

## Files

- `<id>.fbx`, `textures/kit_<id>_{albedo,normal,glass,impostor}.jpg`
- `renders/<id>_turntable.png` (4 views of LOD0), `contact-sheet.png` (whole kit)
- Build scripts: `../_scripts/` (`build_kit.py`, `kit_specs.py`, `kit_pieces.py`, `house_k.py`, `texgen_k.py`, `verify_kit.py`).

## Gaps and notes

- Cedar Bend (§1, PNW) is not covered by any kit; the southeast-midwest and v2 Ski View lots are the nearest match.
- Heron Marsh condo blocks (3-4 storeys, beige #cfc3ab) are not modelled; they are background-only [custom, low-poly] in LAKE-LAYOUTS.
- Harrow club sign: not modelled (needs a generic in-game name).
- Clubhouses are counted with the houses (20-25k budget); 4 house variants + 2 clubhouses.
- No real brand logos, signage, house numbers or names anywhere in the geometry or textures.

## Textures and licenses

See `LICENSES.md`. All are CC0 (Poly Haven, ambientCG) or procedural textures made for this project (self-made, CC0).
