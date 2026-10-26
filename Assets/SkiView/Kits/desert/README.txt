# SKI VIEW regional kit: Desert Southwest

Lakes: saguaro. Built Oct 18, 2026 to the same format as the v2 lot FBXs in `houses/` (see houses/README.md), following Holly's per-lake briefs in `waterski-game/roadmap/LAKE-LAYOUTS.md`.

## Assets

| FBX | what | tris LOD0 / LOD1 / LOD2 | size X x Z x H (m) | pivot | lakes | verify |
|---|---|---|---|---|---|---|
| `desert_modern_1s.fbx` | Modern desert 1-storey: flat roofs, deep overhangs, glass walls, stone fin, lap pool | 21,205 / 11,238 / 40 | 29.0 x 28.0 x 3.7 | street | saguaro | OK |
| `desert_pueblo.fbx` | Pueblo-revival adobe: stepped parapet roofs, vigas, canales, timber portal, courtyard wall | 22,190 / 10,079 / 30 | 25.3 x 26.6 x 6.5 | street | saguaro | OK |
| `desert_modern_pavilion.fbx` | Modern desert 1-storey pavilion: stone-clad street wing, glass lake pavilion under a deep floating roof, pool | 22,815 / 11,116 / 40 | 27.8 x 29.6 x 4.1 | street | saguaro | OK |
| `desert_territorial.fbx` | Territorial adobe ranch: parapet with brick coping, lake-side timber portal, courtyard walls | 22,108 / 8,796 / 30 | 30.2 x 29.1 x 4.1 | street | saguaro | OK |
| `desert_ramada.fbx` | Adobe-column ramada with vigas + latilla shade, banco bench, kiva fireplace, table | 3,210 / 816 / 30 | 7.4 x 5.5 x 3.5 | street | saguaro | OK |
| `desert_float_dock.fbx` | Floating timber dock 6 x 3 m: abutment, aluminium gangway, pile guides (no roof) | 1,448 / 782 / 20 | 3.9 x 13.5 x 3.4 | shore | saguaro (most lots) | OK |
| `desert_shade_dock.fbx` | Floating timber dock 6 x 3 m with a small steel shade roof | 1,932 / 1,114 / 30 | 3.9 x 13.5 x 4.9 | shore | saguaro (a couple of lots) | OK |
| `desert_dock_lift.fbx` | Floating dock + freestanding canopy lift | 2,420 / 1,291 / 30 | 7.6 x 13.9 x 5.0 | shore | saguaro (optional; the v2 brief no longer mentions lifts) | OK |

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

- **Saguaro Flats (§8 v2 + saguaro_look_brief)**: lots at about 53 m frontage on the outer banks, but only **4-5 built houses per outer bank**; leave the rest as graded empty pads. Houses: `desert_modern_1s` and `desert_modern_pavilion` (single-storey modern, the brief style) as the majority, with `desert_pueblo` / `desert_territorial` (adobe, per Karthik) as at most one or two. Docks: `desert_float_dock` (6 x 3 m floating timber dock) on every lot including empty pads, about every 50 m; `desert_shade_dock` on a couple; `desert_dock_lift` optional. `desert_ramada` fits the small clubhouse/shade at the south end or a lot-side shade on the DG bank (pivot = its land-side edge). DG bank #d1ae98 and the riprap/wet band are terrain.

## Files

- `<id>.fbx`, `textures/kit_<id>_{albedo,normal,glass,impostor}.jpg`
- `renders/<id>_turntable.png` (4 views of LOD0), `contact-sheet.png` (whole kit)
- Build scripts: `../_scripts/` (`build_kit.py`, `kit_specs.py`, `kit_pieces.py`, `house_k.py`, `texgen_k.py`, `verify_kit.py`).

## Gaps and notes

- Desert planting (saguaro, palo verde, mesquite, the island fan palms) is not modelled; place plant prefabs on the `_palm_NN` empties and the gravel beds.
- The desert houses are simpler forms than the wooded-lake houses; they reach the 20-25k LOD0 budget partly through denser shrub/rock geometry and a finer atlas cut on trim faces, not added architectural detail.
- `desert_pueblo` has a small second-storey block; the v2 brief says single-storey, so use it sparingly.
- Gated entry, perimeter road and the south-end clubhouse are not modelled (the ramada can stand in for the shade).
- No real brand logos, signage, house numbers or names anywhere in the geometry or textures.

## Textures and licenses

See `LICENSES.md`. All are CC0 (Poly Haven, ambientCG) or procedural textures made for this project (self-made, CC0).
