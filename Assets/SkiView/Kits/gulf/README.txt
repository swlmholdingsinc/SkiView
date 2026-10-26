# SKI VIEW regional kit: Gulf Florida / Texas

Lakes: saltwhite, gulfwind. Built Oct 18, 2026 to the same format as the v2 lot FBXs in `houses/` (see houses/README.md), following Holly's per-lake briefs in `waterski-game/roadmap/LAKE-LAYOUTS.md`.

## Assets

| FBX | what | tris LOD0 / LOD1 / LOD2 | size X x Z x H (m) | pivot | lakes | verify |
|---|---|---|---|---|---|---|
| `gulf_estate_2s.fbx` | Saltwhite 2-storey estate, ivory stucco, grey flat-tile hip roof, screened pool cage | 21,802 / 11,428 / 66 | 32.3 x 25.7 x 9.2 | street | saltwhite (gulfwind ok) | OK |
| `gulf_estate_1s.fbx` | Saltwhite 1-storey estate, cream stucco, white flat-tile hip roof, screened pool cage + spa | 22,388 / 11,550 / 66 | 34.3 x 26.5 x 6.4 | street | saltwhite, gulfwind | OK |
| `gulf_brick_ranch.fbx` | Gulfwind brick ranch, standing-seam metal gable roof, sided gables, front porch | 21,949 / 9,254 / 42 | 29.7 x 26.7 x 7.2 | street | gulfwind (marrowind ok) | OK |
| `gulf_siding_ranch.fbx` | Gulfwind siding ranch, blue-grey lap, dark standing-seam hip roof, screened lake porch | 22,270 / 9,347 / 42 | 30.4 x 27.5 x 6.2 | street | gulfwind | OK |
| `gulf_seawall_dock.fbx` | Concrete seawall (20 m) + concrete T-dock with lift | 2,880 / 1,435 / 50 | 20.1 x 16.0 x 4.2 | shore | saltwhite, gulfwind | OK |
| `gulf_seawall_10m.fbx` | Modular concrete seawall segment, 10 m, riprap toe | 512 / 302 / 10 | 10.2 x 1.8 x 1.8 | shore | saltwhite, gulfwind, villaserena, marrowind | OK |
| `gulf_boathouse_deck.fbx` | Gulfwind covered boathouse: lift, dark metal roof with railed roof deck, outside stair, walkway | 8,078 / 3,372 / 20 | 14.4 x 16.4 x 6.3 | shore | gulfwind | OK |
| `gulf_dock_lift.fbx` | Saltwhite short timber dock + canopy lift (sand-beach entry) | 2,816 / 1,026 / 30 | 8.0 x 13.0 x 4.7 | shore | saltwhite | OK |

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

- **Saltwhite (§4)**: 13 estate lots on both banks, about 70 m frontage. Use `gulf_estate_2s` / `gulf_estate_1s` (alternate, mirror X for variety) set back about 35-45 m from the sand rim, with the pivot on the street line and +Z toward the lake. Drop palms (sabal / royal) on the `_palm_NN` empties. Docks: mostly none (sand-beach entry) or a short `gulf_seawall_dock` with the seawall pieces removed if the bank is sand; one community `gulf_dock_lift` near the SE end. White sand rim #dcd5b9 is terrain, not part of the kit.
- **Gulfwind (§7)**: about 13 single-storey ranches on the south bank at 65-75 m pitch, set back 35-45 m. Alternate `gulf_brick_ranch` and `gulf_siding_ranch` (mirror X). Every lot gets a `gulf_boathouse_deck` (covered lift, dark metal roof with railed roof deck, outside stair, walkway to the lawn; per the v2 Gulfwind look brief) in its dug slip notch at the bank, on or near the house centreline; houses sit 35-60 m back on 65-100 m lots; use `gulf_seawall_10m` runs either side of it, or riprap terrain. `gulf_estate_1s` also fits Gulfwind as an occasional larger lot.
- Seawall pieces are 10 m long and snap end to end along X; `gulf_seawall_dock` includes 10 m of seawall each side of the dock.

## Files

- `<id>.fbx`, `textures/kit_<id>_{albedo,normal,glass,impostor}.jpg`
- `renders/<id>_turntable.png` (4 views of LOD0), `contact-sheet.png` (whole kit)
- Build scripts: `../_scripts/` (`build_kit.py`, `kit_specs.py`, `kit_pieces.py`, `house_k.py`, `texgen_k.py`, `verify_kit.py`).

## Gaps and notes

- Pool-cage screens are frame only (the brief allows 2 opaque materials). For mesh screening add a third, alpha-blended material in Unity on the cage faces, or leave it open.
- Palms are not modelled: the `_palm_NN` empties mark spots for the Quaternius/Unity palm prefabs.
- Boat lifts and canopies are custom low-detail models (LAKE-LAYOUTS lists them as a GAP).
- No real brand logos, signage, house numbers or names anywhere in the geometry or textures.

## Textures and licenses

See `LICENSES.md`. All are CC0 (Poly Haven, ambientCG) or procedural textures made for this project (self-made, CC0).
