# SKI VIEW vegetation (Unity-ready FBX)

39 regional trees and plants, each with three mesh LODs and a billboard impostor. They share two atlases (bark, leaf), with a second pair (`_b`) for species added after the first 31 (the pines so far), and each billboard has its own texture. Everything is procedural and self-made (CC0, see `LICENSES.md`). Built with Blender 4.2 LTS headless. The scripts are in `_scripts/`.

## Conventions (same as the kits)
- **Axes and scale:** FBX Y-up, metres (1 unit = 1 m), identity transforms. The kit convention is applied: the build frame is rotated 180 degrees about Z, so the kits' "lake side = +Z" rule holds. Trees have no facing, so you can rotate them freely about Y for variety.
- **Pivot:** the trunk base at ground level (Y = 0). Roots, buttresses and cypress knees go down to about -0.4 m so they sink into sloped terrain.
- **Hierarchy:** root `<id>`, with children `<id>_LOD0`, `<id>_LOD1`, `<id>_LOD2` (meshes) and `<id>_LOD3` (the billboard). Unity's importer builds a 4-level LOD Group from the `_LODn` suffixes.
- **Billboard (LOD3):** a cross-quad, two vertical cards at 90 degrees, 4 tris. Its own texture `textures/imp_<id>.png` (1024 x 512 RGBA) holds an orthographic LOD0 render from the front in the left half and from the side in the right half. It was rendered under near-flat light so Unity's lighting is not doubled. If you prefer a camera-facing card, use the left half on a quad with a billboard shader (the UVs are 0-0.5 by 0-1).
- **Suggested LOD Group transitions** (60 degree FOV, about 10-35 m trees): LOD0 0.30, LOD1 0.12, LOD2 0.05, LOD3 0.012, then cull. Use Cross Fade on LOD2 to LOD3 if the billboard pop shows.

## Materials (Unity 6 URP)
| Slot | Texture | Unity setup |
|---|---|---|
| `veg_bark` | `textures/veg_bark_atlas.png` (1024, 4 x 4 tiles of 256, tileable) | URP Lit, Opaque, Smoothness about 0.1. Also used for palm trunks, palm petioles, cypress knees and saguaro/ocotillo skin. |
| `veg_leaf` | `textures/veg_leaf_atlas.png` (2048 RGBA, 4 x 4 tiles of 512) | URP Lit (or your foliage Shader Graph), Surface Opaque, **Alpha Clipping on, threshold 0.5**, **Render Face Both**, Smoothness about 0.15. Texture import: Alpha Is Transparency on, mipmaps on, "Mip Maps Preserve Coverage" on (alpha cutoff 0.5). |
| `veg_bark_b` / `veg_leaf_b` | `textures/veg_bark_atlas_b.png` / `textures/veg_leaf_atlas_b.png` (same sizes and import settings as above) | Set up exactly like `veg_bark` / `veg_leaf`. Used by the pines (pine needle tufts, grey-brown and orange plated bark), and reserved for the later species. Added as a second pair so the first 31 assets and their atlas UVs are unchanged. |
| `veg_imp_<id>` | `textures/imp_<id>.png` | The same cutout setup as `veg_leaf` (Alpha Clipping 0.5, Render Face Both), and disable mipmap streaming bias if the cards fizzle at range. |

- Slot order is always bark, then leaf (`veg_bark_b`, `veg_leaf_b` for the pines). Saguaro uses only `veg_bark`. Each billboard uses only its own `veg_imp_<id>`.
- Normals are custom: leaf cards have normals bent outward from the crown centre, and bark has smooth radial normals. Import with **Normals: Import** and Tangents: Calculate Mikktspace. Don't let Unity recalculate the normals, or the crowns will light flat.
- Leaf-card RGB is dilated into the transparent area, so the cutout edges don't get dark mip halos.

## Wind (vertex colour, linear float, attribute `Col`)
| Channel | Meaning | Values |
|---|---|---|
| **R** | trunk/branch sway weight | 0 at the trunk base, growing with height and branch level (trunk top about 0.25-0.35, outer branches and leaf cards up to about 0.8). Palm fronds go from 0.35 at the crown to 1.0 at the tip. Saguaro is 0 (rigid). |
| **G** | leaf flutter | 0 on bark; on leaf cards from about 0.2-0.35 at the attach edge to 1.0 at the free edge or tip. Palm fronds use G = s^1.3 along the frond. |
| **B** | phase offset | A random value (0-1) per branch, card cluster or frond, so neighbouring parts don't sway in sync. |
| **A** | unused | 1 |

Suggested Shader Graph (vertex stage, object space):
- `sway = windDir * windStrength * R * sin(time * 1.1 + B * 6.283 + worldPos.x * 0.05)`
- `flutter = normal * flutterStrength * G * sin(time * 7.0 + B * 6.283 + worldPos.y * 0.7)`
- `position += sway + flutter`

Keep the sway amplitude in metres (about 0.3 m at R = 1 in a breeze). Mesh colors import into `Mesh.colors` unchanged, with no gamma conversion. Billboards carry R = 0.25 * (height/H)^1.5 and G = 0, so they can sway gently with the same graph.

## Assets
Tris are per LOD. The budget is LOD0 6k tris or less for trees, and 3k or less for plants and succulents (palmetto, ocotillo, saguaro). "QA" is the re-import check by `_scripts/verify_veg.py`, which covers names, hierarchy, identity transforms, Y-up, metres, decreasing tris, budgets, material slots, vertex colours and textures.

| File | Species / variant | Height (m) | Crown width (m) | LOD0 | LOD1 | LOD2 | LOD3 | Kit | Lakes | QA |
|---|---|---|---|---|---|---|---|---|---|---|
| `veg_sabal_palm_a.fbx` | Sabal (cabbage) palm, tall straight | 11.03 | 6.31 | 1,472 | 480 | 76 | 4 | gulf, mediterranean | Gulfwind, Villa Serena, Marrowind, Ski View | OK |
| `veg_sabal_palm_b.fbx` | Sabal palm, short curved | 7.37 | 5.86 | 1,230 | 406 | 72 | 4 | gulf, mediterranean | Gulfwind, Villa Serena, Marrowind | OK |
| `veg_royal_palm_a.fbx` | Royal palm, tall (also stands in for queen/foxtail palms) | 20.38 | 7.5 | 890 | 318 | 96 | 4 | gulf, mediterranean | Gulfwind, Villa Serena | OK |
| `veg_royal_palm_b.fbx` | Royal / queen palm, medium | 13.49 | 6.06 | 686 | 242 | 80 | 4 | gulf, mediterranean | Gulfwind, Villa Serena | OK |
| `veg_date_palm_a.fbx` | Desert date palm | 12.54 | 6.84 | 1,056 | 392 | 102 | 4 | desert | Saguaro Flats | OK |
| `veg_fan_palm_a.fbx` | Mexican fan palm with dead-frond skirt | 17.14 | 5.7 | 1,372 | 438 | 100 | 4 | desert, gulf | Saguaro Flats, Gulfwind | OK |
| `veg_fan_palm_b.fbx` | Mexican fan palm, trimmed trunk | 11.94 | 5.69 | 1,224 | 392 | 64 | 4 | desert, gulf | Saguaro Flats, Gulfwind | OK |
| `veg_saw_palmetto_a.fbx` | Saw palmetto clump, large | 1.28 | 4.03 | 2,280 | 816 | 168 | 4 | gulf, southeast-midwest | Heron Marsh, Gulfwind, Marrowind | OK |
| `veg_saw_palmetto_b.fbx` | Saw palmetto clump, small | 0.96 | 1.81 | 1,140 | 408 | 84 | 4 | gulf, southeast-midwest | Heron Marsh, Gulfwind, Marrowind | OK |
| `veg_live_oak_a.fbx` | Live oak with Spanish moss, mature | 12.88 | 27.13 | 5,754 | 1,390 | 440 | 4 | southeast-midwest, gulf, mediterranean | Heron Marsh, Marrowind, Gulfwind, Villa Serena | OK |
| `veg_live_oak_b.fbx` | Live oak, young (light moss) | 8.39 | 14.67 | 3,284 | 868 | 304 | 4 | gulf, southeast-midwest, mediterranean | Gulfwind, Villa Serena, Heron Marsh | OK |
| `veg_willow_a.fbx` | Weeping willow, large | 10.69 | 12.06 | 3,455 | 912 | 177 | 4 | southeast-midwest, cedar-bend | Harrow Pond, Tallpine, Cedar Bend | OK |
| `veg_willow_b.fbx` | Weeping willow, island size | 8.93 | 10.14 | 2,728 | 720 | 144 | 4 | southeast-midwest, cedar-bend | Tallpine (islands), Harrow Pond | OK |
| `veg_maple_a.fbx` | Eastern hardwood: sugar maple, summer | 12.21 | 11.74 | 3,635 | 960 | 337 | 4 | southeast-midwest, cedar-bend | Harrow Pond, Cedar Bend (bigleaf-maple stand-in) | OK |
| `veg_oak_a.fbx` | Eastern hardwood: red/white oak, summer | 14.49 | 16.11 | 3,695 | 976 | 345 | 4 | southeast-midwest | Harrow Pond, Heron Marsh (sweetgum stand-in) | OK |
| `veg_olive_a.fbx` | Olive, mature gnarled | 5.01 | 7.5 | 2,788 | 736 | 260 | 4 | mediterranean | Villa Serena (entry/courtyard planting), Mediterranean kit villas | OK |
| `veg_olive_b.fbx` | Olive, young | 4.22 | 5.04 | 1,806 | 516 | 183 | 4 | mediterranean | Villa Serena, Mediterranean kit villas | OK |
| `veg_palo_verde_a.fbx` | Palo verde, multi-stem | 5.01 | 8.18 | 3,762 | 1,012 | 362 | 4 | desert | Saguaro Flats (median, lots) | OK |
| `veg_douglas_fir_a.fbx` | Douglas fir, tall | 32.32 | 10.36 | 4,532 | 1,690 | 450 | 4 | cedar-bend | Cedar Bend (forest wall) | OK |
| `veg_douglas_fir_b.fbx` | Douglas fir, medium | 20.32 | 6.68 | 2,848 | 1,034 | 314 | 4 | cedar-bend | Cedar Bend | OK |
| `veg_western_red_cedar_a.fbx` | Western red cedar, tall (J-branches, buttress) | 26.32 | 9.35 | 2,800 | 1,010 | 274 | 4 | cedar-bend, southeast-midwest | Cedar Bend, Tallpine (south bank) | OK |
| `veg_western_red_cedar_b.fbx` | Western red cedar, medium | 16.32 | 6.35 | 1,756 | 622 | 178 | 4 | cedar-bend, southeast-midwest | Cedar Bend, Tallpine (south bank) | OK |
| `veg_redwood_a.fbx` | Coast redwood, tall (windbreak row) | 36.32 | 6.7 | 4,860 | 1,826 | 482 | 4 | southeast-midwest, cedar-bend | Tallpine (north bank), Cedar Bend (accent) | OK |
| `veg_redwood_b.fbx` | Coast redwood, medium | 25.32 | 5.78 | 3,328 | 1,250 | 314 | 4 | southeast-midwest, cedar-bend | Tallpine, Cedar Bend | OK |
| `veg_bald_cypress_a.fbx` | Bald cypress with knees and Spanish moss | 20.32 | 8.92 | 2,386 | 894 | 251 | 4 | mediterranean, southeast-midwest | Marrowind (in-water shore fringe), Villa Serena (west natural lake), Heron Marsh | OK |
| `veg_bald_cypress_b.fbx` | Bald cypress, younger, few knees | 14.32 | 6.55 | 1,604 | 610 | 184 | 4 | mediterranean, southeast-midwest | Marrowind, Villa Serena (west lake) | OK |
| `veg_med_cypress_a.fbx` | Mediterranean (Italian) cypress, tall | 14.41 | 2.93 | 1,192 | 504 | 28 | 4 | mediterranean | Villa Serena (entry pairs), Mediterranean kit villas | OK |
| `veg_med_cypress_b.fbx` | Mediterranean cypress, medium | 9.49 | 2.3 | 1,192 | 504 | 28 | 4 | mediterranean | Villa Serena, Mediterranean kit villas | OK |
| `veg_saguaro_a.fbx` | Saguaro with arms | 8.02 | 2.47 | 2,440 | 684 | 366 | 4 | desert | Saguaro Flats (built lots, outer desert) | OK |
| `veg_saguaro_b.fbx` | Saguaro, young (one arm) | 4.52 | 1.35 | 1,240 | 378 | 168 | 4 | desert | Saguaro Flats | OK |
| `veg_ocotillo_a.fbx` | Ocotillo (summer, in leaf) | 3.83 | 4.89 | 1,408 | 378 | 152 | 4 | desert | Saguaro Flats (lot landscaping) | OK |
| `veg_slash_pine_a.fbx` | Slash pine, mature (flatwoods) | 27.39 | 13.55 | 4,790 | 1,302 | 416 | 4 | tournament, gulf | Saltwhite (pine flatwoods), Gulfwind (woodland, optional) | OK |
| `veg_slash_pine_b.fbx` | Slash pine, young | 17.13 | 6.67 | 4,008 | 1,092 | 354 | 4 | tournament, gulf | Saltwhite, Gulfwind (optional) | OK |
| `veg_longleaf_pine_a.fbx` | Longleaf pine, mature (open crown, long needles) | 31.26 | 12.78 | 3,495 | 959 | 305 | 4 | tournament, gulf | Saltwhite (pine flatwoods), Gulfwind (woodland, optional) | OK |
| `veg_longleaf_pine_b.fbx` | Longleaf pine, young (sparse whorls) | 14.33 | 6.28 | 2,313 | 721 | 235 | 4 | tournament, gulf | Saltwhite, Gulfwind (optional) | OK |
| `veg_loblolly_pine_a.fbx` | Loblolly pine, tall (oval upper crown) | 30.46 | 10.92 | 5,512 | 1,512 | 478 | 4 | southeast-midwest, gulf | Tallpine (its namesake pines), Heron Marsh (behind the oak band) | OK |
| `veg_loblolly_pine_b.fbx` | Loblolly pine, medium | 22.17 | 7.9 | 4,750 | 1,290 | 416 | 4 | southeast-midwest, gulf | Tallpine, Heron Marsh | OK |
| `veg_ponderosa_pine_a.fbx` | Ponderosa pine, mature (orange plated bark) | 32.59 | 13.84 | 5,734 | 1,566 | 500 | 4 | desert, southeast-midwest, cedar-bend | desert/western lakes (backdrop hills), Tallpine (optional) | OK |
| `veg_ponderosa_pine_b.fbx` | Ponderosa pine, young (conical) | 18.19 | 8.27 | 5,474 | 1,482 | 476 | 4 | desert, southeast-midwest, cedar-bend | desert/western lakes, Tallpine (optional) | OK |

Totals: LOD0 109,919, LOD1 33,290, LOD2 9,763 tris across 39 assets.

## Where each species goes (per the lake look briefs and `LAKE-LAYOUTS.md`)
- **Gulfwind (gulf kit):** young live oaks (`veg_live_oak_b`) and palms on the south lots; one live oak or one palm on each island; sabal and fan palms; saw palmetto in the woodland edge.
- **Heron Marsh (southeast-midwest kit):** a bank band of live oak with moss (`veg_live_oak_a`, overhanging the water by 3-6 m), with saw palmetto understory. `veg_oak_a` can stand in for sweetgum. The loblolly pines behind the oak band: `veg_loblolly_pine_a/b`.
- **Harrow Pond (southeast-midwest kit):** maple and oak hardwoods on the hills and the berm, and weeping willows at the water.
- **Tallpine (southeast-midwest kit):** a north-bank redwood windbreak row (`veg_redwood_a/b`); a south-bank cedar/redwood mix (`veg_western_red_cedar_*`); one willow on each island (`veg_willow_b`) and more near the clubhouse. Tallpine is named for its pines, so add stands of `veg_loblolly_pine_a/b` (with some slash or ponderosa if you want variety) behind the banks.
- **Cedar Bend (cedar-bend kit):** a Douglas fir and western red cedar wall 30-45 m tall, with `veg_maple_a` standing in for bigleaf maple along the trail (red alder not modelled).
- **Villa Serena (mediterranean kit):** sabal clumps and royal palms (standing in for foxtail/queen palms) at the villas; live oaks behind the lots; bald cypress at the west natural lake; olive and Mediterranean cypress for the Mediterranean kit's villa entries and courtyards (not in the Villa Serena brief, so they are [DA]).
- **Marrowind (mediterranean kit):** a bald cypress fringe standing in the water, with knees and moss (`veg_bald_cypress_a/b`); live oaks with moss and sabal palms behind.
- **Saguaro Flats (desert kit):** palo verde on the median; a fan palm on some islands (`veg_fan_palm_a/b`); date palms; ocotillo and saguaro in lot landscaping and the outer desert.
- **Saltwhite (tournament kit):** pine flatwoods. Open stands of `veg_slash_pine_a/b` and `veg_longleaf_pine_a/b` over `veg_saw_palmetto_*` understory.
- **Desert / western lakes:** `veg_ponderosa_pine_a/b` on the backdrop hills and higher ground, not on the low desert lot landscaping.

## Gaps
- Mesquite, red alder, wax myrtle, sweetgum, creosote and agave are not done yet (they come next, on the `_b` atlases). Until then, use the stand-ins noted above or the MegaKit assets named in LAKE-LAYOUTS.
- Hardwoods are summer only. The Harrow autumn variant would need a recoloured `veg_leaf` atlas tile (the maple tile is index 0, the oak tile index 1).
- No ocotillo flowers. Palo verde has no seasonal yellow bloom beyond a few flower dots.
- The billboards are 2-view cross-quads, so they read thin from directly above. Normal maps are not included (the bark atlas is albedo only) to keep the mobile cost down.
- Royal palm doubles as the queen/foxtail palm (no separate species).
- Pines (v1.1, 8 assets): crowns are tufted whorls on a clear bole, so they read best at mid/far range. Up close the limbs show between tufts more than on a real pine. The longleaf "grass stage" seedling is not included.
- The conifer LOD2s (a single wide card per branch) read paler than LOD0 in grazing light. Unity's LOD cross-fade hides most of it; tint `veg_leaf` slightly darker on LOD2 if it shows.
- `verify_veg.py` reads vertex colours back through Blender's FBX importer, which applies an sRGB conversion. The FBX itself stores the raw values (checked by decoding the file: billboard R max 0.25, B 0.5), and Unity reads those unchanged.

## Files
- `veg_*.fbx`: the assets
- `textures/`: `veg_bark_atlas.png`, `veg_leaf_atlas.png`, `veg_bark_atlas_b.png`, `veg_leaf_atlas_b.png`, `imp_<id>.png`
- `renders/<id>_lods.png`: the LOD0 to LOD3 strips
- `contact-sheet.png`: all strips
- `LICENSES.md`
- `_scripts/`: `texgen_veg.py` (textures), `veg_specs.py` (species parameters), `veg_build.py` (geometry, billboard bake, FBX), `verify_veg.py`, `sheet_veg.py`, `veg_readme.py`, `run_veg.sh` and `run_all.sh` (guarded nice-19, one Blender at a time)
