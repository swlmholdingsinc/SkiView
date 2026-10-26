# SKI VIEW regional kit: Mediterranean / estate

Lakes: villaserena, marrowind. Built Oct 18, 2026 to the same format as the v2 lot FBXs in `houses/` (see houses/README.md), following Holly's per-lake briefs in `waterski-game/roadmap/LAKE-LAYOUTS.md`.

## Assets

| FBX | what | tris LOD0 / LOD1 / LOD2 | size X x Z x H (m) | pivot | lakes | verify |
|---|---|---|---|---|---|---|
| `med_villa_cage.fbx` | Villa Serena 1-storey villa, cream stucco, terracotta barrel hip roof, arched entry, pool cage | 21,759 / 11,713 / 52 | 27.2 x 25.2 x 6.2 | street | villaserena | OK |
| `med_villa_tower.fbx` | Villa with 2-storey entry tower, barrel tile, solid terrace with balustrade, lawn pool | 22,148 / 9,815 / 50 | 25.3 x 29.0 x 7.8 | street | villaserena, marrowind | OK |
| `med_estate_2s.fbx` | 2-storey Mediterranean estate: loggia arcade, balcony with balustrade, terrace, barrel tile | 23,716 / 11,536 / 66 | 30.9 x 21.6 x 8.5 | street | marrowind, villaserena | OK |
| `med_ranch_marrowind.fbx` | Marrowind ranch, pale-yellow stucco, white metal hip roof, pergola patio | 21,439 / 8,462 / 42 | 28.7 x 24.9 x 5.0 | street | marrowind | OK |
| `med_dock_lift.fbx` | Villa Serena seawall + short timber dock (5.5 m) + covered lift, dark shed roof | 2,837 / 1,300 / 30 | 14.3 x 6.6 x 4.7 | shore | villaserena (most lots) | OK |
| `med_dock_pergola_lift.fbx` | Villa Serena seawall + short dock with open pergola deck + covered lift | 3,709 / 1,948 / 40 | 14.3 x 6.8 x 4.8 | shore | villaserena (a few lots) | OK |
| `med_seawall_10m.fbx` | Low seawall segment, 10 m, cream cast cap | 538 / 376 / 10 | 10.2 x 1.8 x 1.8 | shore | villaserena, marrowind | OK |
| `med_longdock_boathouse.fbx` | Marrowind 40 m timber dock + open covered slip (white hip metal roof) + lift | 5,292 / 2,160 / 34 | 7.6 x 42.0 x 5.5 | shore | marrowind | OK |
| `med_longdock_gazebo.fbx` | Marrowind 40 m timber dock on stilts ending in a hip-roofed gazebo platform | 8,040 / 2,326 / 22 | 6.3 x 47.4 x 5.6 | shore | marrowind | OK |

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

- **Villa Serena (§3 v2 + villaserena_look_brief)**: 12 lots on the west bank at about 37 m pitch, houses about 55 m back from the water (east lake: 5-6 on its east bank). Alternate `med_villa_cage` and `med_villa_tower` (both 23-25 m across; mirror X). Docks: `med_dock_lift` (short dock, ~5.5 m past the edge, covered lift with dark shed roof) on most lots, `med_dock_pergola_lift` on a few; `med_seawall_10m` runs between. No docks on the median bank. Palms (foxtail/queen at the entries, sabal clumps) on the `_palm_NN` empties. The one-off white two-storey modern farmhouse is not in this kit (use `se_farmhouse_bnb` with a grey metal roof as a stand-in, at most one).
- **Marrowind (§9)**: single-storey ranches on 25-35 m lots: `med_ranch_marrowind` (pale-yellow stucco, white metal roof) as the main house, `med_estate_2s` / `med_villa_tower` for variety; each with a long dock: `med_longdock_gazebo` (40 m dock on stilts ending in a hip-roofed gazebo platform, per marrowind_look_brief) on most lots, `med_longdock_boathouse` (40 m dock + covered slip and lift) on some (docks there run 30-60 m; length is a build parameter in kit_specs).

## Files

- `<id>.fbx`, `textures/kit_<id>_{albedo,normal,glass,impostor}.jpg`
- `renders/<id>_turntable.png` (4 views of LOD0), `contact-sheet.png` (whole kit)
- Build scripts: `../_scripts/` (`build_kit.py`, `kit_specs.py`, `kit_pieces.py`, `house_k.py`, `texgen_k.py`, `verify_kit.py`).

## Gaps and notes

- Pool-cage screens are frame only (see gulf kit).
- The pergola, shed-roof lift canopy and lift are custom low-detail models.
- Villa Serena one-off modern farmhouse and the Marrowind theme-park/garden complex, lighthouse-like tower and boardwalk are not modelled.
- Marrowind dock length is fixed at 40 m in this FBX (rebuild with P len=30-60 if needed).
- No real brand logos, signage, house numbers or names anywhere in the geometry or textures.

## Textures and licenses

See `LICENSES.md`. All are CC0 (Poly Haven, ambientCG) or procedural textures made for this project (self-made, CC0).
