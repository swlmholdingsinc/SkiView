
### Pass 13 (7:19–8:12 AM CT, Oct 18): tournament jump ramp and a first jump

- **Research (IWWF tournament rules 10.x, AWSA rule 9; numbers used in the game):**
  - Ramp ratio (height / length out of water) 0.235, 0.255 or 0.266; 6.50–7.00 m out of the water, at least 0.5 m under. Maximum heights 1.645 / 1.785 / 1.86 m. We use 0.255 at 1.75 m over 6.85 m (14.8°).
  - Width 3.7–4.3 m (4.20–4.25 recommended): 4.22 m. Surface flat (no convex; up to 5 cm concave), fibreglass or plywood, waxed and kept wet by a watering system (spray bar + pump).
  - Aprons along the full length on both sides, 28–60° from vertical (45° used), reaching at least 20 cm under the water, in a contrasting colour. Marks at 6.50 m and 7.00 m from the top edge; a front water line 455 mm below the high point.
  - Height is set by raising the top edge on hydraulic jacks / winches from the front deck ("kneeler"). Anchored at the corner eyes, front and rear lines, levelling weights.
  - Course: ramp centreline 15 m from the X axis, opened 1.0–2.5° (1.75° used). Distance buoys at 210 / 180 / 150 m, ST −72, MT +10, ET +51, EC +100 m, pre-gates at −210 m; boat lane 15–19 m off the ramp (17 m used).
  - Jumping is on two jump skis (max width 30% of length; ours 2.03 m long, ~21 cm wide), helmet mandatory, 23 m line. Boat speeds 45–57 km/h (54 km/h used).
  - Technique: pull out wide on the ramp side, counter-cut across the wakes, turn on the far side, then a progressive double-wake cut so the skier reaches the ramp at 1.3–2× boat speed on a diagonal; compress through the ramp, stand up ("pop") in the last third, then fly body-forward with the skis tips-up and the handle in. Land tips-up with the knees absorbing; tips down = out the front.
- **Placement (Holly's LAKE-LAYOUTS §0.2).** The ramp moved from 15 m to 18.5 m off the slalom line (≥ 18 m rule). The apron edge has ~16 m of clear water to the east (house-side) shore. Touchdown in the test jump is 20.7 m off the slalom line, so the landing lane keeps ~18 m to the shore. The jump course buoys only appear in jump mode.
- **13.1 Ramp (53e3984).** Modelled ramp with a wet waxed surface (generated albedo / normal / smoothness: panel seams, wax streaks, faint ski tracks, 6.50/7.00 m marks), navy 45° aprons with a front skirt and water line, aluminium top edge, kneeler deck on two HDPE floats, two hydraulic jacks, safety chains, a spray bar with 7 nozzles and a pump float, corner eyes and yellow anchor lines. Water fx: a film of streaks running down the surface, drips off the edges, foot splash and foam along the waterline.
- **13.2 Jump run (5cfc2d8, 7dd3fad).**
  - Gear: two 80 in jump skis (generated, plain red topsheet, no graphics) with the same high-wrap boots, 32 cm stance, a dark helmet on the head bone. Swapped in for the slalom ski in jump mode (HUD "JUMP" button in the turn).
  - Approach: scripted rope pendulum on the 23 m line with the boat at 54 km/h in the jump lane: pull-out (+36°), counter-cut, far-side turn (−34°), cut to the ramp. Result: 19.3 m/s at the ramp (1.28× boat), 16.5° across the ramp axis.
  - **Alignment vs speed:** the spec's ≤ 12° across the ramp and 1.5× boat speed can't both come out of a 23 m rope pendulum (1.5× needs ~15–19° of cut). I chose ~1.3× and ~16°, so the landing stays well away from the house-side shore.
  - Ramp contact: gravity + waxed friction along the 14.8° surface, crouched, pop at 82% of the ramp (+0.6 m/s), spray burst at the toe and a skirt of drops off the top edge.
  - Flight: point mass with posture-driven lift and drag (ClA 0.42, CdA 0.27) and a slack-capable rope spring. Body forward ~40°, skis tips-up in a slight V, handle at the hips.
  - Test jump: off the top edge at 18.8 m/s (68 km/h), 1.38 s in the air, apex 3.1 m, **29.4 m / 96 ft** (from the top-edge centre). Banner "29.4 m · 96 ft · 0.255 · 54 km/h", "SCORED" after a 2 s ride-out, then back into the turn.
  - Landing: tips +7°, knee absorb, landing spray fan and a foam/ring splash. Below +2° tips it's an out-the-front fall: crash burst, both skis come off (one pops up and over, one skids off), treading, then the deep-water start.
- **13.3 Stills + sheet (f420fef).** Screens/jump_1–9 (boat camera approach, cut, low ramp-side angle, take-off, shore-side airborne / landing, ride-out, crash). Tools/make_jump_sheet.py → Screens/jump_v13.png (not in git). The ramp close-up sits beside study-only reference frames whose paths come from a file outside the repo.
- **Mixamo batch-3 flips skipped.** Backflip / Front Flip are land gymnastics clips (take-off from the feet, tucked, no skis or rope). Tournament jumping has no flips (a flip in distance jumping is a fall), so they wouldn't look believable here.
- **Two skis vs slalom in the game:** slalom = one 67 in ski, front/rear boots, 15–43 off ropes at 34 mph through the 6-buoy course. Jump = two 80 in skis side by side, helmet, 23 m line at 54 km/h, scored on distance, not buoys.
- **Rider sheet** unchanged (the slalom rider didn't change). **env_compare_v13** regenerated because the ramp moved; the ramp is tiny or out of frame in those angles.
- **Builds:** APK 70.3 MB (8:08 AM CT), WebGL 67.4 MB (8:11 AM CT), 0 errors and 0 warnings, both after the ride-out fix (7dd3fad).
- **Sheets:** jump_v13, env_compare_v13 (not in git).

### Pass 14 (8:12–8:50 AM CT, Oct 18): realistic ramp, volumetric spray, placeholder tree, player-controlled jump

- **14.1 Ramp realism (62802dc).**
  - Surface: red waxed fibreglass (the default; easy to change in Tools/gen_ramp_textures.py). A paler, matte, scratched ski track is worn diagonally into the wax, with skid marks and wax build-up at the edges. Panel seams every 1.22 m with fastener rows and a centre seam, white 6.50 / 7.00 m marks, algae below the waterline. Normal map, and a smoothness mask (wet 0.93, worn ~0.62).
  - Running water: a new SkiView/RampWaterFilm overlay shader. Two scrolling rivulet layers give a film thickness; the slope comes from the thickness, plus sky reflection with Fresnel and sun glints. Thin water barely darkens the surface; thicker rivulets read.
  - Aprons: white gelcoat on an aluminium frame (stiffeners with bolt heads and rust weeps), grime streaks, an alu top trim, a darker wet splash band, a green algae line at the water, and murky below.
  - A foam / contact band runs along the waterline polyline. The ramp bobs on its floats: about ±1.5 cm heave, 0.1° pitch, 0.07° roll, and it settles a little while the skier is on it.
- **14.2 Spray (4d9ce38).**
  - New SkiView/SprayParticle shader: sky ambient plus sun with backlit forward scatter, soft particles (depth), and a fade near the water plane.
  - Generated textures: wispy mist with no solid core, droplets with a soft halo (motion-stretched streaks), and lacy torn sheets.
  - A mist layer was added to the ski spray, the crash, and the toe / landing bursts.
  - Crash sheets are now soft rotated billboards; before, they were stretched "paper" needles. Droplet streaks are shorter.
- **14.3 Placeholder geometry (1e90f3e).**
  - The cartoon tree was a faceted low-poly crown (6 lumps, about 10.5 × 7.4 × 11.4 m) on a trunk. It came with blocky hedge rows (102 blocks), and both were merged into the lot-8409 FBX body mesh.
  - At build time, the LOD0/LOD1 body meshes are split into connected parts and coloured from the lot atlas. Green crowns, their trunk and the hedge blocks are dropped, and a card bigleaf maple plus understory shrubs are planted in their place. No other lot matched.
  - Primitive audit: only legitimate small parts remain (buoys, boot straps, fins, ramp caps / floats / pump, handle).
- **14.4 Player control + jump camera (8013ed3).**
  - **CUT:** a tap (Space / click / touch below the HUD) on the far side starts the progressive cut. The cut is rebuilt from the current rope angle and rate to the toe; its end rate is 0.8 × the average rate needed, clamped to 8–19°/s. That makes a late cut harder, faster and steeper, and an early cut softer and slower. No cut by −2.6 s → "NO CUT · MISSED THE RAMP": he swings back behind the boat, past the ramp.
  - **POP:** a tap on the ramp.
    - Best at 0.78–0.95 of the ramp (+0.6 m/s).
    - Early: less pop and tips high; below 0.6 → "FELL BACK".
    - Late: tips low; after the lip → "OUT THE FRONT".
    - No pop: a short, safe jump.
    - A crossing steeper than 19° also lowers the landing pitch.
  - Autopilot uses −4.3 s / 0.86. The HUD shows the prompts and the control line.
  - **Entry aim:** aimed 0.5 m to the course side of centre. The real path lands at +1.5 m (pass 13 hit +2.53 m, outside the 2.11 m half-width); a clamp to ±1.81 m is a safety net.
  - **Jump camera:** a shore stand 17 m to the ramp side and 7 m downstream of the lip, 2.4 m up. It pans with the skier and zooms so the skier stays ~40% of the frame. It takes over 0.6 s before the toe, holds through flight, landing, ride-out or crash, then snaps back to the boat camera.
  - **Flight pose:** in the air the shoulders-back pull is removed, and the spine/chest fold forward (0.45 × the jump pitch; the flight pitch is now 68°). The side view is no longer upright.
  - **Approach framing:** the boat cam is tighter on the jump approach (0.72×, min FOV 10°, follows up to 72° yaw).
- **Timing sample** (Screens/jump_samples_v14.json):

  | Run | Cut | Pop | Toe speed | Crossing | Distance | Touchdown off the slalom line | Result |
  |---|---|---|---|---|---|---|---|
  | early | −5.7 s | 0.72 | 16.9 m/s (1.12×) | 9.5° | **23.0 m / 75 ft** | 18.7 m | ride out |
  | ideal | −4.3 s | 0.90 | 19.0 m/s (1.26×) | 14.7° | **28.2 m / 93 ft** | 20.8 m | ride out |
  | late | −3.0 s | 0.93 | 20.6 m/s (1.37×) | 17.8° | **31.6 m / 104 ft** | 21.9 m | ride out |
  | late pop | −4.3 s | after the lip | 19.0 m/s | 14.7° | 26.4 m | 20.9 m | out the front |
  | no cut | – | – | – | – | – | – | missed the ramp |

  - The landing lane stays ≥ 17 m from the east shore (~39 m off the slalom line).
- **Sheets (not in git):** jump_v14, env_compare_v14, rider_compare_closeup_v14, dws_fall_v14.
- **Builds:** APK 70.5 MB (8:44 AM CT, 87 s), WebGL 67.6 MB (8:47 AM CT, 163 s). Both had 0 errors and 0 warnings.

## Pass 15: other lakes, starting with Cedar Bend (Oct 18, 2026)

- **Lake data path.** Each lake is its own scene, `Assets/SkiView/Scenes/SkiViewLake_<id>.unity`, built by `SkiViewBuilderLakes.BuildLake(id)` from `Assets/SkiView/Lakes/<id>/venue.json` + `heightmap.r16` + `splat.png` (written by `Tools/gen_venue_lake.py <id>` from `roadmap/venue/lakes/<id>_layout_m.json`). All lake assets (terrain, meshes, materials, volume, sky) live under `Assets/SkiView/Lakes/<id>/`, so Ski View's assets are never touched. venue.json carries a `lake` block: id, display name, hasRamp, boat palette (band/accent/deck from boat_palettes.json, colours only), water colours and sky/light mood. The boat, skier, rope, handle and spray rigs are shared. `BuildAll` builds the home scene, then every lake, and sets the build scene list (home first). HUD: a LAKE button (turn mode) cycles lakes; the title shows the lake name; JUMP is hidden where the lake has no ramp. Ski View's venue.json / gen_venue.py are unchanged.
- **Cedar Bend (`cedarbend`).** 259 m course on bearing 22.3° (SSW → NNE), turn islands at both ends, boat loop round both islands (min clearance 8.0 m, loop 1280 m). Shoreline re-read from the aerial on a 25 m grid: the layout's `shore_build` is ~35% too wide and has a reflection notch in the north neck (for Holly to re-trace); water is 83–90 m wide in game vs 91–130 m stated. Slalom only (LAKE-LAYOUTS §1), no ramp. Mowed lawn with stripes to the water on both banks, fir/cedar wall with a broadleaf row on the west strip, moorage slip with a shed-roof boat cover and lift, white two-storey farmhouse (dark shingle roof), blue-roof barn and shed, gravel pad and lane. Mood: overcast HDRI, sun el 48°, soft shadows, mirror water with green-grey shallows. Boat: #B3202A / #1C1D21 / #F3F2EE.
- **Polish from v14.** Ramp deck gloss × 0.74 and water-film sky reflection 0.55 (less pink at low angles); apron normal map (stiffeners, bolts, oil-canned panels) + per-panel gelcoat tone; crash core softened (births over a 0.6 m disc, fewer zero-spread particles, sheet alpha 0.30 → 0.22, mist 0.14 → 0.11, fewer sheets/mist on the jump crash).
- **Saltwhite (not built this pass).** The ~15 m SW-bank widening from 60 m up-course of the ramp to 120 m past MT (25 m tapers) is computed: `Docs/lakes/saltwhite_shore_build_widened15.json`. Landing lane (12 m off the jump boat path) to shore at 20/40/60/80 m past MT goes from 16.3/13.7/11.1/8.4 m to 31.2/28.5/25.9/23.3 m; ramp to shore 14.6 → 29.6 m; width at the ramp 63 → 78 m; the Estero ends and islands are unchanged.
- **Villa Serena.** 39–54 m wide; the course line is only 10.1 m from the shore at the narrowest point (turn buoys sit 11.5 m off the line), so it needs the course shifted or ~8–10 m widening even for slalom; no jump.
- **Builds:** APK 76.6 MB (9:29 AM CT, 88 s), WebGL 76.3 MB (9:32 AM CT, 151 s), 0 errors / 0 warnings. One lake costs about +6 MB APK / +9 MB WebGL.
- **Sheets (not in git):** lake_cedarbend_v15, boats_v15, jump_v15, env_compare_v15, rider_compare_closeup_v15, dws_fall_v15.

## Pass 16: Cedar Bend realism, per-lake decals, per-lake content packs (Oct 18, 2026)

- **Cedar Bend on Holly's v2 re-trace.** `Tools/lake_data/cedarbend_shore.json` now has `"source": "layout"`, `"layout": "cedarbend_layout_m_v2.json"`. The generator takes shore_build, islands and course from that file (bearing 24.2°, 662 m long, 61–94 m wide, median 68 m). The north island keeps its loop (gaps 25–26 m). Per LAKE-LAYOUTS §1, the south island sits about 8 m off the bank, so the boat turns on a teardrop in the south bowl (R 34 m, centre 252 m south of the course centre) and the island stays as dressing. Generator check: loop 1239 m, boat path at least 9.4 m from shore and island, skier envelope (±18 m, gate to gate) at least 9.5 m from shore, boat line at least 27.3 m. The moorage slip moved out with the bank (mouth 37 m, back wall 63 m). The by-eye edge table stays in the file as a fallback. **A newer re-trace drops in two ways:** change `"layout"`, or put `roadmap/venue/lakes/cedarbend_shore_retrace.json` `{"shore_build": [[x_east, y_south], ...]}` (this wins when present). Then re-run `Tools/gen_venue_lake.py cedarbend` and the pipeline.
- **Realism.**
  - Multi-layer PNW forest: fir/cedar wall, alder/maple, black cottonwood, understory shrubs, sword fern, blackberry at the edges. About 92.7k tree/plant instances, with no tan gaps from the air.
  - Edges: unmowed strips at the forest edge and waterline, sparse reeds and tall grass, a wet bank band.
  - Backdrop: rolling foothills plus 5 hazed ridge layers from 1.1 to 6.2 km (`SkiView/BackdropHaze`). Fog is 0.00082 and the overcast mood is kept.
  - Farmhouse: corner boards, frieze, rakes, gutters/downspouts, deep windows, gable windows, porch posts/beam/rails/balusters, chimney with cap, rear wing, grime. Barn: trim, wainscot, gutters, louvres, lean-to, grime.
- **Boat decal.** "Ski View" appears only on the home boat. Other lakes use `T_Livery_SWLM.png` (SWLM + fictional hull number). The start banner shows the lake name instead of "SKI VIEW".
- **Content split (Addressables 2.10.3 + addressables.android 1.1.0, both bundled with the editor).**
  - The player has only `SkiViewBoot.unity` plus code. `SkiViewBoot` initialises Addressables and loads the start lake (`?lake=<id>` on WebGL, `-lake <id>` on the command line). The LAKE button goes through `SkiViewBoot.Load`.
  - Groups: Core_Home (home scene), Core_Shared (204 assets used by 2 or more lake scenes), Lake_<id> (one per lake). All use LZ4, pack together, and build with the player.
  - Android builds both a sideload APK (all content inside) and an AAB with Play Asset Delivery. Core is install-time; each lake is on-demand (`Lake_<id>`). WebGL fetches bundles from StreamingAssets/aa only when they are needed.
- **Verification.**
  - WebGL, headless Chrome (software GL), `?lake=cedarbend`: the server log shows only lake_cedarbend + core_shared + builtins fetched (no core_home), and "LAKE READY SkiViewLake_cedarbend (Addressables)" in 2.8 s. Home start fetches core_home + core_shared, with no lake bundle.
  - AAB, checked with bundletool 1.18.1: `Lake_cedarbend` is `dist:on-demand`; AddressablesAssetPack and UnityDataAssetPack are install-time. The catalog uses PlayAssetDeliveryAssetBundleProvider.
  - **Not run on a device** (no emulator or KVM on the box).
- **Sizes.**
  - Android: AAB 80.9 MB. Base module download 30.5 MB. Install-time packs add 45.4 MB, so the first install is 75.9 MB (bundletool, arm64, SDK 33). The Cedar Bend on-demand pack is 8.1 MB to download (9.8 MB APK slice). Sideload APK 89.4 MB.
  - WebGL: base `Build/` 16.7 MB. Core bundles: shared 29.1 MB, home 36.2 MB. Cedar Bend bundle 13.8 MB.
- **Builds (CT):** APK 10:57 AM (53 s), AAB 10:57 AM (33 s), WebGL 11:02 AM (24 s incremental; the full WebGL build at 10:33 took 300 s). All had 0 errors and 0 warnings.
- **Sheets (not in git):** lake_cedarbend_v16, env_compare_v16, boats_v16.

## Pass 17: Saltwhite (Gulf lake), Cedar Bend fixes, phone LOD tiers, player cameras, landscape auto-rotate (Oct 18, 2026)

- **Cedar Bend fixes.**
  - The west lawn strip is now 9–14 m of mown grass from the water. The road runs about 18 m back and the rail-trail about 31 m back, both following the bank, as on the aerial.
  - The barn sits in a 36 m mown yard, so there are no ferns or brambles in the barn shot.
  - Regenerated: loop 1239 m, clearances unchanged (boat at least 9.4 m, skier envelope at least 9.5 m). There are 75,446 generator trees plus about 19k builder fillers, for 94.4k instances.
- **HUD.** At non-home lakes the top bar shows only the lake name; "Ski View N1" no longer appears there.
- **LakeDetail (density + LOD tiers), added to every lake root by the builder.** Tier 0 is desktop, tier 1 is WebGL and tier 2 is phone.
  - On tiers 1 and 2, a runtime copy of the TerrainData keeps every tree within 140 m of the lake bounds.
  - The far forest is thinned to 60% (WebGL) or 30% (phone), and the survivors are scaled up so the canopy stays closed.
  - Small plants are dropped beyond 90 m (WebGL) or 40 m (phone) from the water.
  - Tree distance drops to 2000 m (WebGL) or 1300 m (phone). Tree LOD bias drops to 0.8 (WebGL) or 0.55 (phone), and the reflection camera uses LOD bias 0.3.
  - On phones, lake shadows are limited to 70 m with 1 cascade (the URP asset is restored when the lake unloads).
  - Each building's pieces are merged into one mesh per material, cutting draw items from 764 to 40 at Cedar Bend and from 4307 to 186 at Saltwhite.
  - The planar reflection renders at half resolution on WebGL.
- **Per-tier estimate** (`Screens/perf_tiers_v17.txt`, from `SkiViewCapture.LakePerfAll`).
  - Method: terrain trees are counted per prototype, LOD and submesh as instanced batches. The URP shadow pass and the planar reflection pass are added, and scene meshes are counted per visible submesh.
  - Phone tier, from the boat camera and the high chase mid-pass:

| Lake | Plant instances kept | Draws | Triangles |
|---|---|---|---|
| Cedar Bend | 46.6k of 94.4k | about 443–486 | 0.38–0.47 M |
| Saltwhite | 18.2k of 52.2k | about 446 (boat cam) and 632 (high chase) | 0.40–0.46 M |

  - Against the mid-range-phone budget for 30 fps (about 300–500 draws, 0.5–1 M triangles), Cedar Bend fits. Saltwhite fits from the boat camera but is over the draw budget from the high chase.
  - The biggest fixed cost is the skier's 99 mesh renderers, drawn in both the main and reflection passes. These are estimates only; nothing was profiled on a device.
- **Saltwhite Lake** (fictional Gulf-coast estate lake, Estero-style). It is its own `Lake_saltwhite` pack, on-demand in Play Asset Delivery, and its own WebGL bundle.
  - Shore: Holly's layout plus the about 15 m SW bank widening (`saltwhite_shore_build_widened15`). Holly's shore_build leaves out the NW and SE end bulbs, so those come from the aerial water mask, blended over 20 m (`Tools/make_saltwhite_shore.py`; the reference path is read from the staging folder).
  - Islands: the SE island is moved 2.5 m SW to give the boat 8.4 m of clearance.
  - Course and boat path: 259 m course; the boat loops round both islands (loop 1182 m, lap 97 s). The skier envelope (±18 m) is at least 6.6 m from shore, at its tightest in the NW reach.
  - Jump: on the widened SW bank, 22 m off the line, run NW → SE (jumpDir −1). The ramp is 30.9 m from shore and the landing line at least 24.7 m from shore. A headless jump run with ideal timing gave RIDE OUT at 29.3 m.
  - Gulf kit:
    - Plants: slash-pine flatwoods, lawn pines, sabal and royal palms, saw palmetto, sea oats and hedges (52.2k instances).
    - Homes: 13 white stucco estate homes with hip tile roofs, lanais and screened pool cages.
    - Waterfront: 6 piers (2 with lifts), a community dock, a white sand rim and a wet band.
    - Event dressing and backdrop: judges' tower, 3 tents, a scoreboard with no text and an arch; 5 flatwoods treelines from 0.9 to 5.2 km.
    - Look: clear-sky HDRI with a low morning sun, and teal-green Gulf water.
  - Boat palette: white deck, grey band, black accent, with the SWLM livery.
- **Player cameras.**
  - The CAM button (C on desktop) cycles HIGH → CLOSE → 1ST → BOAT. The choice is saved in PlayerPrefs `skiview.cam`. High chase is the default.
  - Switching blends over 0.7 s with smoothstep, and the camera is kept at least 0.3 m above the water.
  - First person: helmet-cam from the head bone, yaw follows the rope (0.12 s), head bob is damped, roll is 0.3 × lean (max 9°, 0.5 s), FOV 88, pitch 34. A fall lowers the eye and levels the roll. The jump shore camera is skipped while first person is on.
  - Close chase: 5 m back and 1.55 m high, kept at least 3.5 m from the body and 0.6 m above the water, looking 7 m past the skier.
  - Sheet: `cameras_v17`.
- **Orientation.**
  - The build script's `ConfigureProject` sets AutoRotation with both landscape directions on and both portraits off (`defaultScreenOrientation: 4`). `SkiViewBoot` sets the same at runtime.
  - The built APK manifest has `screenOrientation=11` (userLandscape: either landscape direction, following the user's rotation lock), and the AAB matches.
  - The HUD is inset by `Screen.safeArea` on the left, right and top. Only the background bar runs full width.
- **Verification.**
  - WebGL (headless Chrome, CDP): `?lake=saltwhite` was LAKE READY in 1.4 s, fetching only lake_saltwhite + core_shared + builtins. `?lake=cedarbend` was READY in 1.3 s. No core_home was fetched for either.
  - AAB (bundletool 1.17.2): `Lake_cedarbend` and `Lake_saltwhite` are on-demand; AddressablesAssetPack and UnityDataAssetPack are install-time.
  - **Not run on a device.**
- **Sizes** (MB as Unity/bundletool report them; arm64, SDK 33).
  - Android: APK 99.1, AAB 87.5. Base download 30.5, install-time packs about 45.9, so first install is about 76.4.
  - On-demand packs: Cedar Bend 7.8 to download (9.6 MB slice); Saltwhite 6.8 to download (10.0 MB slice).
  - WebGL: `Build/` 16 MB, core_shared 32.1, core_home 32.0, lake_cedarbend 11.6, lake_saltwhite 12.1.
- **Builds (CT).**
  - The full pipeline ran 12:27–12:39 PM.
  - APK at 12:34 PM (191 s) and AAB at 12:34 PM (32 s), 0 errors and 0 warnings.
  - WebGL at 12:39 PM (244 s including editor start).
- **Sheets (not in git):** lake_cedarbend_v17, lake_saltwhite_v17, cameras_v17, env_compare_v17, boats_v17.

## Pass 18: first person + turnaround, phone perf, Saltwhite polish, three new lakes, UI kit, audio (Oct 18, 2026)
- First-person camera; end-of-pass turnaround (sink holding the handle -> DWS -> round the island -> set-up; tap skips). Ski View: CW round the north island, CCW round the south.
- Phone tier 2: <= 500 draws per camera at every lake (max 389, Villa Serena close chase). Table in Screens/perf_tiers_v18.txt.
- New lakes Villa Serena, Tall Pine, Heron Marsh: each its own Addressables group / on-demand PAD pack; WebGL loads only that lake's bundle + core_shared.
- UI Toolkit kit (Julia): CAM control + toast, Settings (Graphics: camera motion, quality; Audio: volume; Controls: default camera + bindings), Credits + font licence. `?ui=graphics|audio|controls|credits|license` opens a screen.
- Audio (James, pass 1): event system `SkiView.SkiAudio` (Assets/SkiView/Scripts/SkiAudio.cs), bank Assets/SkiView/Audio/Resources/SkiViewAudio/Bank.asset, built from manifest.json by Editor/SkiViewBuilderAudio.cs. No AudioMixer asset: group faders are script values (Boat -3, Skier 0, Amb -6, UI -4, Music -2 dB). Per-lake ambient beds ship in the lake packs.
- Builds 14:01-14:14 CT: APK 132.3 MB, AAB 111.4 MB, WebGL 143.4 MB, 0 errors. Tools/webgl_check.py = headless Chrome checker (`--query`, `--keys`, `--fill`, `--shot`).
- Note: the Addressables build deletes Assets/AddressableAssetsData/link.xml; restore it with git checkout after every build.
- Next: audio pass 2 (10 per-lake WebGL ambients, ~20 new sounds), remaining capture URL params, UI overflow at 16:9 and missing arrow glyphs.
