# SKI VIEW regional kit: Tournament dressing (all lakes)

Lakes: all. Built Oct 18, 2026 to the same format as the v2 lot FBXs in `houses/` (see houses/README.md), following Holly's per-lake briefs in `waterski-game/roadmap/LAKE-LAYOUTS.md`.

## Assets

| FBX | what | tris LOD0 / LOD1 / LOD2 | size X x Z x H (m) | pivot | lakes | verify |
|---|---|---|---|---|---|---|
| `tour_judges_tower.fbx` | Two-storey judges' tower: steel base, glazed booth, balcony, stair, PA horns | 6,570 / 2,620 / 40 | 6.3 x 5.6 x 8.9 | street | all (Saltwhite SE-end lawn per LAKE-LAYOUTS §4) | OK |
| `tour_start_dock.fbx` | Start dock: wide platform, low skier step-off, ski rack, benches, flag pole | 2,754 / 714 / 30 | 8.2 x 16.0 x 8.3 | shore | all | OK |
| `tour_boathouse.fbx` | Small tournament boathouse (single slip), metal panel walls + roof | 5,494 / 2,834 / 14 | 13.3 x 9.9 x 5.4 | shore | all | OK |
| `tour_scoreboard.fbx` | Scoreboard: steel legs, generic LED digit face (no logos/text), flood lights | 758 / 238 / 30 | 4.2 x 1.5 x 5.3 | street | all | OK |
| `tour_tent.fbx` | 3 x 3 m white pop-up tent with table and chairs | 1,142 / 202 / 22 | 3.2 x 3.2 x 3.0 | street | all | OK |
| `tour_arch.fbx` | Generic inflatable arch, 8 m span, white/blue, blank banner (no logos or text) | 980 / 444 / 30 | 11.2 x 2.4 x 6.6 | street | all (Saltwhite SE-end lawn per LAKE-LAYOUTS §4) | OK |

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

- All lakes. Typical tournament layout (Saltwhite §4 SE-end lawn): `tour_judges_tower` on the bank beside the course mid-point (open judging deck faces +Z = the lake), `tour_start_dock` at the course start end, `tour_scoreboard` 20-40 m inland facing the lake/spectators (face = +Z), 2-3 `tour_tent` on the lawn, `tour_arch` (generic inflatable arch, blank banner) on the lawn or at the dock approach, `tour_boathouse` for the tow boat near the start dock. Harrow Pond: judges tower + start dock on the west channel (harrowpond_look_brief).
- The `skier_start` / `boat_slip` empties on the start dock and boathouse mark where the skier and tow boat spawn. The scoreboard face is a generic amber 7-segment texture with a blank title band: no logos or names.

## Files

- `<id>.fbx`, `textures/kit_<id>_{albedo,normal,glass,impostor}.jpg`
- `renders/<id>_turntable.png` (4 views of LOD0), `contact-sheet.png` (whole kit)
- Build scripts: `../_scripts/` (`build_kit.py`, `kit_specs.py`, `kit_pieces.py`, `house_k.py`, `texgen_k.py`, `verify_kit.py`).

## Gaps and notes

- The jump ramp and buoys are not in this kit (Harrow wants a red/yellow ramp scheme, Marrowind a white deck: use the generic ramp model).
- The judges tower is a raised booth on an open steel base (booth level + ground level) rather than a fully enclosed two-storey building.
- Ski-show grandstand / breakwater floats (Marrowind east-shore park) are not modelled.
- The scoreboard digits are baked into the texture (static). For live scores, overlay a UI/TextMeshPro quad on the face.
- No real brand logos, signage, house numbers or names anywhere in the geometry or textures.

## Textures and licenses

See `LICENSES.md`. All are CC0 (Poly Haven, ambientCG) or procedural textures made for this project (self-made, CC0).
