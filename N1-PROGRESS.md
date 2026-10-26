# SKI VIEW: Unity N1 vertical slice, progress log

_Last updated: Sat Oct 17, 2026, ~9:15 PM CT (session 1: setup plus first playable greybox)_

## Status at a glance
| Item | State |
|---|---|
| Unity license, headless | **Works.** Scott's Personal entitlement resolves through the shared Unity licensing client in `-batchmode -nographics`. A tiny test project at `/tmp/skiview-licensetest` was created in 13 s and reopened in 6 s, both exit 0. The log line "Access token is unavailable" is harmless: the entitlement still resolves. |
| Project | `/workspace/skiview-unity` (Unity 6000.3.26f1, URP 17.3.0, linear colour, legacy Input Manager). It's a git repo, with a Unity `.gitignore`. |
| Playable greybox | **Yes** (details below) |
| Android APK | `Builds/Android/SkiView-N1-greybox-debug.apk`: **46.2 MB**, IL2CPP arm64, debug-signed, built in **392 s** (about 7.3 min wall time including the platform switch). Package id `com.swlm.skiview.n1`, so it can install next to the live CUTLINE app. Latest build is from 9:08 PM CT (incremental, 86 s, 46.1 MB) and includes the splat fix and the HUD font fix. A clean first build took 392 s. |
| WebGL | Latest build is from 9:12 PM CT (incremental, 243 s, 35.6 MB), re-verified in headless Chrome. **Attempt 2 succeeded at 8:56 PM CT: 35.6 MB** (`Builds/WebGL`: wasm 10.4 MB, data 26.7 MB, gzip), built in 564 s. It boots in headless Chrome (SwiftShader) in about 3 s from localhost and plays on autopilot (`Screens/09_webgl_running.png`). Attempt 1 failed at 8:42 PM CT: `il2cpp --convert-to-cpp` was OOM-killed (exit 137) after 16.5 min, because the box was short on RAM when Mamta's full PK build started. Her processes were unaffected. `Tools/run_builds.sh` is queued: it waits for PK builds to finish, then runs scene + screens, Android, and WebGL in that order. |
| Screenshots | `Screens/01…08_*.png` (1600×900, rendered by Unity under Xvfb with Mesa llvmpipe GL 4.5) |

## What's in the slice
- **Lake at real scale.** `Tools/gen_venue.py` builds it from `roadmap/venue/skiview_layout_m.json` (the aerial trace at 0.38 m/px, ±15%).
  - A 1.6 × 1.6 km Unity Terrain with a 1025² heightmap.
  - The lake bed shelves to about 3.8 m deep.
  - Both islands are included.
  - The east (house) bank rises toward Ski View Ln. The west bank has the tan levee path seen in the aerial, then forest, then a fir ridge further out.
  - Four splat layers: lawn, dry grass/levee, forest floor and mud. The textures are procedural, matte and CC0-equivalent (ours).
- **Slalom course** at IWWF spacing:
  - 259 m from gate to gate. The first ball is 27 m in, then every 41 m, offset 11.5 m. Boat guides sit at ±1.15 m and gates at ±1.25 m. Pre-gates are 55 m outside each gate.
  - The course is centred on the channel at the fitted -9.5° axis.
- **Boat loop:** a baked 1 m spline, 1,791 m long.
  1. Southbound pass at **34 mph**, held constant from 50 m before the entry pre-gates to the exit pre-gates.
  2. Ease off over about 60 m.
  3. **South island turn:** in through the wide east gap, around the far side, out through the west gap. Gap speed is about 25 km/h, with at least 7.8 m clearance from shore everywhere.
  4. Line up and run the northbound pass.
  5. North island turn the same way, then repeat.
  - One full lap (two passes and two turns) takes about 162 s. Each turn takes about 60 s; a **Skip turn ▶▶** button and the Space key run it at 3×.
- **Skier (Felderpuss) placeholder:** capsule, vest, ski and rope.
  - Runs a C# port of the cutline `sim.ts` pendulum and scoring: lean torque, cut-in and go-out boosts, wake pop, edge-catch falls, gate, ball and half-ball scoring, and an exit-gate check.
  - Uses cutline's Amateur tuning at 34 mph and the §10.3 Felderpuss stats.
  - In turns the skier lags the boat's heading, and auto-assist keeps him in the boat's track.
  - The rope reels in to the next length during the slow part of the turn, following the §11 ladder (15→22→28→32→35→38→39.5→41→43 off).
  - The HUD shows "Next: 22 OFF · 34 MPH".
  - **Controls:** hold the left or right half of the screen, or ←/→ or A/D. **AUTO** button or P for autopilot.
  - The autopilot was tuned in `Tools/ai_tune.py`. Headless sim test (`SkiViewCapture.SimTest`): **6 of 6 clean passes in 440 s**, 15 off through 38 off, alternating south- and northbound, with correct buoy sides each direction.
- **Water** (`Assets/SkiView/Shaders/SkiViewWater.shader`, self-written URP HLSL):
  - Four Gerstner waves (vertex and per-pixel) plus capillary ripples.
  - Depth colour and soft shoreline from the camera depth texture.
  - Fresnel reflection of an analytic sky, with a dark, jagged fir-line band near the horizon (the lake's real look).
  - Sun glints, main-light shadows on the water, and fog.
  - **Foam wake** fed from script: prop-wash trail, Kelvin V arms (19.5°) and the skier's spray trail.
- **Houses:** 17 lots on the **east** bank, 8017 to 8505 north to south.
  - Uses archetypes A–G from `CATALOG.md`, with filler variants for undocumented lots. Placement is evenly spaced along the shore, set back about 34 m, each house facing the water, with docks.
  - **8017 hero lot:** three-level charcoal, glass and stacked-stone massing, glass rails, terraced stone beds, central stair, and a long dock with a fire pit and four chairs.
  - No numbers, names or signage in-game.
- **Trees:** 12,306 procedural firs. Five-tier firs are used near the lake and low-poly firs on the far ridge, combined into 196 static chunk meshes. Islands are dense.
- **Boat:** lofted, unbranded tournament-style hull in white with a blue band. It has a cockpit, seats, windshield, and a chrome pylon at 1.46 m. **"SWLM CUTLINE"** is on both sides, with no OEM marks.
- **Lighting:** one preset, a late-summer afternoon with the sun in the SW. Procedural sky, trilight ambient, soft shadows to 220 m, ACES tonemapping, bloom and vignette.

## How to rebuild (all headless; check `pgrep -fa Unity` first and use `nice -n 15`)
```
U=~/Unity/6000.3.26f1/Editor/Unity; P=/workspace/skiview-unity
/workspace/skiview-tools-venv/bin/python $P/Tools/gen_venue.py               # venue data, heightmap, splat, textures
LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a -s "-screen 0 1920x1080x24" $U -batchmode -force-glcore -quit -projectPath $P \
   -executeMethod SkiViewCapture.BuildAndCapture -logFile $P/Logs-capture.log  # rebuild scene + Screens/*.png (~40 s)
$U -batchmode -nographics -quit -projectPath $P -executeMethod SkiViewCapture.SimTest -logFile $P/Logs-simtest.log
$U -batchmode -nographics -quit -projectPath $P -buildTarget Android -executeMethod SkiViewBuild.Android -logFile $P/Logs-android.log
$U -batchmode -nographics -quit -projectPath $P -buildTarget WebGL   -executeMethod SkiViewBuild.WebGL   -logFile $P/Logs-webgl.log
```
Build results are appended to `Builds/build-log.txt`.

## Screenshots (`Screens/`)
01 aerial overview · 02 chase cam, pass 1 at ball 3 · 03 low side shot of the carve at ball 4 · 04 boat and Kelvin wake · 05 south-island turn (drone) · 06 chase cam, pass 2 northbound · 07 east-bank houses from the water · 08 hero lot 8017

## Next (N1, weeks 2–8)
1. **Water:** planar or SSR reflections of the real shoreline, FFT or more Gerstner bands, a compute or RT wake heightfield with foam advection (replacing the point arrays), prop wash and rooster-tail spray particles, and skier spray sheets at the edge change.
2. **Houses:** a modelled 8017 hero mesh (stacked-stone material, glass with interior cards, night glow), a proper modular kit for archetypes A–G, lot landscaping, boathouse and lift, gazebos and props.
3. **Firs:** real Douglas fir and cedar meshes with card foliage, LODs and impostors, wind sway, and GPU instancing instead of combined chunks. Also an understory and a shoreline rock band.
4. **Boat:** a Blender model to the §M4 specs (unbranded) with the SWLM / SKI VIEW livery, driver and prop wash.
5. **Skier rig:** Felderpuss rigged with a Mixamo-style placeholder, then mocap. IK to the handle and ski, edge-angle and counter-rotation poses, and falls.
6. Rope sag and catenary, handle physics, on-device performance captures on three tiers, dynamic resolution, and a signed AAB with the upload key (versionCode above the live one) when Scott wants it on the Play track.
7. Port the rest of `sim.ts`: difficulty levels, other speeds, and the jump and tricks events (N2).

## Known issues and decisions
- The WebGL console shows three non-fatal `ERROR: Shader` lines (probably URP variants that WebGL2 doesn't support). Rendering is fine; to be traced in week 2.
- The web/IMGUI font has no arrow or ▶ glyphs, so the HUD uses plain text. The real UI (uGUI/UI Toolkit with our own font) comes in N2.
- The task brief said houses go on the **west** shore. The aerial and `CATALOG.md` put Ski View Ln and all the houses on the **east** bank, so they're placed east.
- The brief names the boat "SWLM CUTLINE". TOP-TIER-PLAN §10.1 proposes "SWLM" plus "SKI VIEW" livery. The slice uses SWLM CUTLINE for now. **Scott: pick one.**
- The turn loop direction (in through the east gap, out through the west gap) and the course position are fitted from the aerial. **Scott: please confirm the loop direction at each end and where the buoys actually sit.**
- The aerial scale is ±15%, so one measured distance (for example dock to dock) would lock it.
- The demo package id is `com.swlm.skiview.n1`, a separate debug app. The Play release will switch to `com.swlm.cutline` with the upload key.
- Each turn takes about 60 s versus the 45 s target. Tighten it when we tune the spline.
- Unity re-added default packages (purchasing, analytics and others) on first import. They've been removed from `Packages/manifest.json` so the APK carries no billing or analytics.
- Water and foam were tuned on software-GL renders. They need checking on a real phone (no emulator, per the box rules).

## Scott's answers (Oct 17, 2026, 10:09 PM CT). Apply these next session.
- Island turnaround: the boat always goes around the island from the TREE side (west) first. Heading north, it turns left: it goes up the west gap of the north island, around the tip, and back south through the east gap. Heading south, it turns right: it goes down the west gap of the south island, around the tip, and back north through the east gap. The current greybox enters the south island through the east gap, which is reversed and needs fixing.
- Jump ramp: at the south end, on the house side (east bank).
- Scale (Scott, 10:12 PM CT): "Water ski lakes are 2200 feet long by 350 feet wide." That's 670.6 m by 106.7 m. Rescale the Ski View lake trace so the main water body matches (it's currently ±15% from the aerial). Check that the 259 m course plus pre-gates and both island turnarounds still fit.
- Course placement (Scott, 10:13 PM CT): the slalom course sits centered between the two turn islands, so both ends get equal turnaround room.
- Name: Scott confirmed that using "Ski View" is fine.

---
## Session 2 (started 11:11 PM CT, Oct 17): applying Scott's answers. *Live log, updated as I go.*

- 11:27 PM CT: Box overloaded by Mamta's PK Android build. Did code and scene edits only, with no Unity runs.
- 11:30 PM CT: `Tools/gen_venue.py` rescaled and rerun.
  - Main water is now 670.6 × 106.7 m (2,200 × 350 ft). The aerial trace measured 840.5 m tip to tip with an 80.8 m mean channel. I scaled anisotropically: along ×0.798, across ×1.321 (see the decision note in the report).
  - Course centred between the islands: both island near edges are 256 m from the course centre.
  - North island moved 4 m toward the course. Its far-side (tip) gap was only 16 m at real scale and is now 20 m.
  - West gaps: north 24 m, south 33.5 m. Minimum boat-to-shore clearance on the whole loop is 6.6 m.
  - Island turns now go out the west gap and back through the east gap at both ends. Measured from exit pre-gate to the next entry pre-gate: south 44.6 s, north 45.8 s (was about 60 s).
  - The boat reaches 34 mph about 9 m before each entry pre-gate. The lake is too short for the 50 m settle-in on top of that.
- 11:33 PM CT: Livery PNG generated (SWLM + "Ski View" + swoosh + WN 272), `Tools/gen_livery.py`.
- 11:35 PM CT: Ramp placeholder, Mixamo rider (`SkierRig.cs`, Playables), water bounds from venue.json, and new capture shots are all coded. Committed bd6c934.
- 11:36 PM CT: Mamta's build finished, load 3.4. Started BuildAndCapture at nice 15.
- 11:37–11:42 PM CT: Scene rebuilt and captured. Rider pose took four quick headless iterations.
  - Remy's textures had to be extracted from the FBX.
  - Skinned meshes needed `forceMatrixRecalculationPerRender` for the editor capture.
  - The HumanPoseHandler muscle overlay gave a broken kneeling pose. It was replaced with additive bone rotations.
- 11:45 PM CT: Android APK rebuilt: 54.1 MB, 99 s. It's bigger because of the Remy textures; they're now capped at 1024 px for the next build.
- 11:47 PM CT: WebGL build OOM-killed (il2cpp exit 137) when Mamta's PK PlayMode `-runTests` and a Blender job started at 11:48 PM CT. Only my process was killed; hers were untouched.
  - `Tools/run_builds.sh` now also waits for load < 12 and ≥ 6 GB free RAM.
  - It gives up after 2 h instead of competing with her jobs.
  - Requeued at 11:50 PM CT.

### Session 2 changes (detail)
| Item | What changed |
|---|---|
| Scale | `gen_venue.py` rescales the aerial trace to 670.6 m tip to tip × 106.7 m mean channel width (2,200 × 350 ft). **Anisotropic**: along ×0.798, across ×1.321, because the trace's own proportions (840 × 81 m) don't match 2,200 × 350 ft. Written to `venue.json → scale`. A uniform scale is an easy switch if Scott prefers it. |
| Course | The course is shifted 19.8 m along the axis so it sits exactly between the islands. Both near edges are 256 m from the centre, leaving 71.5 m of turnaround water past each pre-gate. |
| Island turns | Both turns go out the **west (tree-side) gap**, round the tip and return through the **east gap**: north is a left turn (clockwise), south a right turn (counterclockwise). The south turn is no longer reversed. |
| Gap clearance | West gaps are 24 m (north) and 33.5 m (south). The north island's far-side tip gap was 16 m at real scale, so I moved that island 4 m toward the course, giving 20 m. The tightest boat-to-shore clearance on the loop is 6.6 m. |
| Turn time | Turn speed is 23 mph with a 3.6 m/s² lateral limit (was 20 mph and 3.2). Exit pre-gate to the next entry pre-gate: **south 44.6 s, north 45.8 s** (was about 60 s). Pass 24.3 s, lap 139 s. |
| Run-in | The boat reaches 34 mph about 9 m before each entry pre-gate and holds it to the exit pre-gate. The 670 m lake leaves no room for a 50 m settle-in on top of that. The pass-start window moved to prog −214…−186 and the spawn to −232. |
| Ramp | Placeholder `JumpRamp_Placeholder` built to JUMP-TRICKS-SPEC: ratio 0.255, 6.85 m of surface above water plus 0.5 m submerged, 1.75 m lip, 4.22 m wide, white surface, navy 45° aprons, grey posts and boom. Lip is at a = +158 m (south end), 15 m east (house side) of the course line, skied southbound, opened 1.75° with the low end toward the course. It is 15 m from the boat path and 55 m past the last turn ball. Not jumpable. |
| Livery | The TextMesh "SWLM CUTLINE" is gone. Both sides now carry the 1.2.0 three.js art: "SWLM" in Barlow Condensed ExtraBold, a slanted italic "Ski View", a pale teal swoosh and "WN 272", on the blue band, reading correctly from both sides. Art is generated by `Tools/gen_livery.py`, output `Assets/SkiView/Generated/livery_swlm_skiview.png`. The boat object was renamed `Boat_SWLM_SkiView`. |
| Rider | Mixamo Remy (Humanoid) replaces the capsule, with a navy life vest. `SkierRig.cs` runs a manual PlayableGraph blending Breathing Idle (slack or slow), Pulling A Rope (loaded), Edge Slip (mixed in with the carve rate \|ω\|) and Treading Water (after a fall). On top of that, an additive ski stance is weighted by rope load: hips back, knees bent, feet planted, arms straight to the handle. The handle follows the hands. Root roll and pitch still come from the slalom physics. If the FBX files are missing, it falls back to the capsule. |
| Mixamo files | Copied to `Assets/SkiView/ThirdParty/Mixamo/` and **git-ignored**: raw files mustn't be redistributed on their own. The restore command is in `.gitignore`. |
| Water | Water mesh bounds come from `venue.json → lakeBounds` (were hardcoded). |
| Capture | New shot list: 01 overview, 02/03 chase and carve, 04 rider close-up, 05 boat livery, 06 ramp, 07 south turn, 08 northbound chase, 09 houses, 10 north turn, 11 hero lot. Also `SkiViewCapture.RiderTest`. |

### Session 2: rider rework (Scott's 12:01 AM and 12:03 AM notes)
- 12:03–12:10 AM CT: Research pass written to `Docs/SLALOM-SKIER-LOOK.md`, before any more modelling. It covers ski shape, bindings, stance and gear, using manufacturer spec pages and the study-only reference keyframes. It contains no pro names.
- 12:10–12:25 AM CT: Rider rebuilt to that research, in `SkiViewRiderKit.cs` (new) and `SkierRig.cs` (rewritten):
  - **Ski**: procedural 67 in (1.702 m) slalom ski.
    - 7.0 in max width at 62 % of the length, narrower tail, elliptic tip.
    - 62 mm tip rocker and 6 mm tail rocker; 9–18 mm thick.
    - Tunnel concave, bevels, an aluminium fin with an 8° wing, and a graphic carbon top sheet with no logos (`Tools/gen_rider_textures.py`).
  - **Bindings**: double high-wrap (the tournament look), front heel 30 in from the tail, rear boot tandem with a 12° toe-out. Each has a plate, shell, collar, teal power strap and laces. Sneakers are hidden.
  - **Gear**: segmented navy vest (2 × 10 foam panels on a neoprene base, waist strap and buckle, zip, shoulder straps), navy and teal board shorts, charcoal rash guard. The textures are derived from the Mixamo textures and git-ignored.
  - **Stance**: IK, not muscle curves.
    - Pelvis over the front foot, crouched to 75–85 % of leg length.
    - Stacked lean away from the boat (6–18° plus the sim's edge roll); shoulders back.
    - Upper body counter-rotated toward the boat.
    - Two-bone leg IK into the boots, two-bone arm IK to a low handle at the hips.
    - The outside hand releases and reaches at the turn.
  - **Rope**: the handle bar follows the hands, with a V bridle to the pylon line.
- Bugs fixed on the way:
  - Boot loft winding was inside-out.
  - `Pow(sin π)` gave NaN at the panel edges (white stripes on the vest) and at the boot collar.
  - Lean direction was toward the boat.
- `Screens/rider_compare.png`: 4 study-only reference keyframes above 4 game renders. Git-ignored, and the reference frames are never imported into Assets. Script: `Tools/make_rider_compare.py`.
- 12:15 AM CT: Mamta's PK Linux build and QA runs started. Unity work was held (rule: no Unity runs during her PoliticalKombat/Filibuster jobs). Two ~35 s nice-15 rider test renders did overlap her jobs: the start of her PK Linux build at about 12:15 AM CT, and her `FilibusterFighters` screenshot player at 12:23 AM CT. My inline pgrep check matched its own shell, so I wrongly treated the BUSY flag as false. The runner script's check is not affected. No large build ran alongside her jobs.
- 12:24 AM CT: Full pipeline (`Tools/run_builds.sh`: capture → Android → WebGL, nice 15) queued; it waits for her jobs to finish.
- 12:28–12:33 AM CT: Mamta's jobs finished, load 6.8, 8 GB free. `run_builds.sh` ran at nice 15:
  - Capture: 33 s, Screens 01–11.
  - **Android APK: 50.9 MB** (73 s, `Builds/Android/SkiView-N1-greybox-debug.apk`), down from 54.1 MB thanks to the 1024 px texture caps.
  - **WebGL: 41.9 MB** (161 s, `Builds/WebGL`), succeeded this time (the 11:47 PM attempt was OOM-killed).
  - Mamta started PK PlayMode `-runTests` at about 12:35 AM CT, after my builds were done.
- 12:36 AM CT: WebGL smoke test in headless Chrome (SwiftShader). It loads in about 3 s, autopilot runs (`Screens/12_webgl_boot.png`, `13_webgl_running.png`), and there are no exceptions. **The three "ERROR: Shader" lines** are URP-internal hidden shaders with no WebGL2 subshader:
  - `Hidden/CoreSRP/CoreCopy`: render-graph copy pass; needs framebuffer fetch or compute.
  - `Hidden/Universal Render Pipeline/StencilDitherMaskSeed`: LOD cross-fade stencil dither.
  - `Hidden/Universal/HDRDebugView`: HDR debug view only.
  - None of these is used by SKI VIEW rendering. They are a known benign URP 17 startup message on WebGL, and nothing renders wrong. Leaving them alone; they can only be silenced by patching or stripping URP package shaders.
- Stale `12_webgl_*_session1.png` shots removed.

### Pass 3 (Scott's 12:37 AM review of rider_compare.png / rider_pull_side.png)
| # | Note | Fix |
|---|---|---|
| 1 | Head thrown back, chest not over hips | `leanPullDeg` 18 → **9°** (glide 4°), chest extra lean 5 → 0°, shoulders back 18 → 8°. New `LevelHead()`: neck brought halfway back toward vertical, head set level, eyes forward and 20 % toward the boat, pitched 6° down, yaw clamped to ±55° from the chest. |
| 2 | Boots like ski-boot blocks | Re-lofted as a low-profile rubber high-wrap that follows the foot: 6–9.6 cm wide (was 11.6), 20.5 cm wrap at the ankle dropping to 3.5 cm at the toe, rounder section, matte rubber. Plate, power strap and laces slimmed to match. |
| 3 | Ski floats flat and pale | `SkierRig.PoseSki()` pivots the ski about the ankle midpoint: tail down and tip up 2.5–5° net in the world, sunk up to 3.5 cm at speed, so the tail and deck are under the water while the tip and rocker stay out. It rolls up to 14° further onto its edge in carves (the boots ride the ski, so the leg IK follows). New saturated red-orange top sheet with a dark graphic (no marks), smoothness 0.82 → 0.50, base dark grey (was near-white). |
| 4 | No spray | New `SkiSpray.cs`, built onto the ski by the builder (`Materials/SkiSpray.mat`, URP Particles/Unlit, 64 px puff from `gen_rider_textures.py`). Three world-space systems: **spray sheet** off the ski's base side (opposite the lean), streaked droplets, rate and speed scaled by edge angle × carve rate × speed; **rooster tail** up and back from the tail, grows with edge; **tail foam**, flat horizontal billboards trailing as wake foam. Driven from `SkiViewGame.ApplyVisuals`; edit-mode capture steps it with `Simulate()`. |
| 5 | Too straight-legged | Crouch 75–85 % → **66–72 %** of leg length, pelvis moved back over the ankle midpoint (was 7 cm forward), so the front knee comes forward over the toes. |
| — | Busy check | New `Tools/mamta_busy.sh`. It reads `ps` with its own PID and parent excluded, using bracket-trick patterns so it can never match itself. It treats as busy: PoliticalKombat/Filibuster processes, `pk_run`, `-pkOut` and `-pkScreens` (screenshot and QA players), any batchmode Unity not on skiview-unity (PlayMode `-runTests` included) and Blender. `run_builds.sh` and the rider waiter both use it; the rider waiter also re-checks 20 s later in case she's chaining jobs. |
- 12:44 AM and 1:01 AM CT: two RiderTest iterations ran in gaps between her jobs: after her PlayMode tests, and between her screenshot player and pk_qa10, which started at 1:04:49 AM CT; mine had ended at 1:02:08 AM CT. No overlap. The first spray pass was a fuzzy cloud; it's now fine streaked droplets, a fan thrown outward and back, and flat foam.

### Pass 4: whole-body lean (Scott's 1:27 AM note) + Christopher's boat (parent's 1:58 AM note)
- 1:28 AM CT: the pass 3 Android build that had started at 1:27 was stopped (superseded). Pass 3 committed as 1c0ab45.
- **Lean as one line from the force balance on the ski.** `SkiViewGame` now rolls the whole skier root by
  `lean = atan((v·ψ̇·1.25 − sign(side)·min(1, |side|/0.45)·1.6·g·pull) / g)`, smoothed over 0.12 s:
  - `v·ψ̇` is the path curvature (heading rate measured from the skier's velocity), which leans the body into the turn.
  - The rope term leans it away from the boat while loaded; `pull` = boat load × (1 − buoy reach).
  - Clamp is ±70°, and the limit backs off automatically when any body part would reach the water.
  - The rig's own torso lean is gone (0°). The pelvis sits on the rolled axis above the ankles, so ski edge, hips and shoulders form one line, with the feet IK'd onto the tilted ski. Hips drop 5 % of leg length at a full edge. Arms stay straight to the handle.
- **Head:** level with the horizon, keeping 25 % of the body roll so the neck isn't snapped sideways at 60°. The neck takes half of the correction.
- **Water clearance:** `SkierRig.MeasureClearance()` checks hips, glutes, spine, chest, neck, head, elbows, hands and knees (bone centre minus a flesh radius) against the lake surface. Below 3 cm, the roll limit backs off.
  - Knee poles were re-aimed from the hip–ankle midpoint, biased to the high side. The old poles, from the clip's knee, dragged the rear knee down to the ski.
  - The free hand's reach is now forward and above the water.
  - Crouch went back to 76–80 % of leg length: at 66 % the knees were 6–12 cm off the water.
- **Measured in the rider shots:** pull −35°, buoy +60° (hips 0.27 m above the water, lowest point the inside hand at 9 cm clearance), carve exit +42°. Head level, nothing in the water.
- New `rider_pull_behind` shot (sun behind the camera, so the lean reads as tilt). The boat-view front shot is backlit.
- `Screens/rider_compare_v4.png`: the same 4 reference frames over pull side, pull from behind, carve at buoy and carve exit.
- **Boat:** `Assets/SkiView/Boat/SkiBoat.fbx`, Christopher's model, with textures and `boat_palettes.json`; staging README followed.
  - `SkiBoatImport.cs`: Generate Lightmap UVs on, no cameras, lights or animation, textures capped at 1024, data maps linear, normal maps typed as normal.
  - URP Lit materials for all 12 slots (`Materials/Boat/`) with base, normal and metallic-smoothness maps hooked up. Glass is transparent, livery alpha-clipped at 0.4.
  - LODGroup set to LOD0 35 % and LOD1 8 %, then cull. Shadows from LOD0 only.
  - `BoatPalette` (his MaterialPropertyBlock snippet, re-applied on enable) sets Ski View #1C3F9E band, #B9C0C9 accent (metallic 0.35) and #F4F5F2 deck.
  - The rope attaches to the `TowPoint` empty (1.285 m, 0.36 m forward of the pivot); `PYLON_Y` and `PYLON_BACK` match it in the sim.
  - The wake origin is `WakeEmitter.z`. A new prop-wash particle system sits on `SprayEmitter`.
  - The pivot is at the waterline: the boat lifts 0–6 cm and trims bow-up 1° to 3.5° as it gets on plane.
  - The procedural boat remains as the fallback.
- New shot `14_chase_boat_and_skier`; `05_boat_wake_livery` recaptured.
- **One editor session** (`SkiViewCapture.FullPipeline` via `Tools/run_pipeline.sh`): scene build, all captures, rider shots and the Android player, so the boat was imported once. WebGL runs as a second session because it needs the platform switch.
  - 2:14–2:18 AM CT, all at nice 15, with no Mamta jobs running. **APK 55.5 MB, WebGL 47.5 MB**: about +5 MB from the boat (27k + 7k tris, 1024 textures).

### Environment realism pass v5 (Oct 18, 2026, 2:26 AM to 3:17 AM CT; commit 75d2d2a)
Goal: "as life like as possible", judged against Scott's lake photos (study-only). Sheet: `Screens/env_compare_v5.png` (git-ignored).
- **Water** (`Shaders/SkiViewWater.shader`, `Scripts/PlanarReflection.cs`): planar mirror reflection (proper-rotation mirrored
  camera, oblique near plane at the water, water on layer 4 excluded; RT at 1/4 res on the Mobile tier, 1/2 on PC) with
  Schlick Fresnel and an HDRI cubemap fallback. Swell cut to ~1/4, small capillary ripples gathered into drifting breeze
  patches with glassy lanes between them. Dark PNW body colour. Wake: Kelvin-style V crests with real slope normals (they bend
  the mirror image), prop-wash foam thresholded against lace noise so it breaks up and fades (~1.3 s), a jade aerated
  trail (~7 s), and a rougher ripple strip that calms over ~12 s. The stern now comes from the WakeEmitter (`_BoatFwd.w`).
  The prop-wash particles were cut back (life 0.9 s, rate 260/s) because the shader foam carries the trail now.
- **Trees**: no CC0 tree meshes in staging, so I made procedural card trees: Douglas fir (narrow, high crown base, gappy
  5-branch whorls drooping 8–30°) and western red cedar (~20%, fuller, drooping lacy sprays), 2 variants each. Bark is CC0 and the
  spray and impostor textures are generated. They are terrain tree prototypes (GPU instanced, 12,021 instances) with a
  LODGroup: cards above 9% of screen height, then 3 crossed impostor quads that are never culled. LOD bias is 0.6 on Mobile
  and 1.2 on PC. The old combined cone-chunk meshes were removed (~390 files).
- **Ground**: CC0 PBR terrain layers with normal maps: Grass004 lawn tinted toward late-summer, Grass005 recoloured to straw,
  forest_ground_04 duff, and Rocks011 rip-rap at the waterline. Smoothness now comes from the layer constant (albedo alpha made the ground mirror-glossy).
  The hero terrace beds are lawn-textured.
- **Docks**: low plank docks (~0.32 m above the water, planks across, world-metre UVs) on dark piles. The hero lot has a U-slip with
  a boat lift, a peaked green canopy, and a fire pit with chairs.
- **Sky and lighting**: Poly Haven kloofendal_48d partly-cloudy HDRI (cumulus) as a Panoramic skybox. It is rotated so the HDRI
  sun sits SW over the west bank and the directional light matches it (elevation 47.9°, checked in `env_sky_suncheck`).
  Custom reflection cubemap from the same HDRI, trilight ambient fitted to it, exp2 haze 0.00078 for aerial perspective,
  ACES, bloom 0.22, slight warm white balance and split-tone grade, vignette 0.16. Soft shadows on PC only (Mobile is hard, 160 m).
- **Houses**: not swapped. Christopher's 18 FBXs in staging/houses have no README or QA note marking them ready, only `_work/qa`
  renders, so they stay greybox until they're marked ready.
- New tools: `Tools/gen_env_assets.py` (texture and card generation, HDRI sun finder), `Tools/make_env_compare.py`, and
  `SkiViewCapture.EnvShots` / `EnvTest` (also part of `FullPipeline`).
- Builds (02:51 / 02:56 AM CT, from the final runtime code): Android APK 43.7 MB (was 55.5), WebGL 40.4 MB (was 47.5). Both got smaller because the combined fir chunk meshes were replaced by instanced terrain trees.
  Later re-captures only changed the editor capture angles. Known gaps: the shoreline rock strip only appears where the venue splat paints it; prop-wash particles are still a little puffy; the far-bank stands are uniform; Vulkan RT orientation of the reflection isn't verified on a device (the captures used GLCore).

### Pass 6 polish (Oct 18, 2026, 3:18 AM CT onward). One commit per sub-step: e743dd9, ea4a694, 654f0b7, 2d4398e, 0bbb75a
1. **Shoreline** (`Tools/gen_shoreline.py`, `Editor/SkiViewBuilderShore.cs`): the waterline contour from the heightmap is chained
   and smoothed into 4 polylines (lake + islands, ~2.3 km). A strip runs from 0.9 m under water to 2.6 m inland with a 16 cm
   bank lip, a dark wet band at the waterline and a ragged alpha edge into the lawn. Each 30–55 m section is rock rip-rap
   or a grass bank (75% grass in front of houses, 30% elsewhere). No lawn meets the water bare any more.
2. **Tree line**: 9% of firs are 40–60 m emergents. Red alder and bigleaf maple card trees (the maple has ~20% early-fall
   yellow/orange leaves) make up ~10% of the trees, ~30% on low ground by the water. Clumped Perlin clearings remove ~27% of the instances.
   A far ring of forested hills (1.15–2.6 km, up to ~270 m) with a ragged alpha skyline fringe sits behind the terrain, hazed by the fog.
   The reflection far clip went up to 3 km so the hills show in the mirror.
3. **Lawns**: greenness comes from distance to the nearest house (green within ~22 m, straw beyond ~75 m), scaled by a
   per-lot factor (0.35–1.0, some owners water and some don't) and noise mottling.
4. **Boat spray**: the puffs are replaced by streaked droplets (cone, 650/s at plane) and a thin full-width transom
   sheet (1400/s, 0.5 s life) that arcs back into the water. The shader foam carries the trail.
5. **Reflection flip safeguard**: on top-left-origin APIs (`UNITY_UV_STARTS_AT_TOP`: Vulkan, Metal, D3D) the shader flips v when
   the water draws straight to the back buffer (`_ProjectionParams.x > 0`), which is when the RT and screen conventions differ.
   GLES and WebGL never flip. `PlanarReflection.forceFlipY` is a manual override. The mirror camera is a proper rotation and its oblique
   projection is left API-neutral for URP to convert. Android uses Vulkan first, then GLES3. **Untested on a device.**
- Houses: still no `houses/README.md` marking lots ready, so they stay greybox.
- Sheet: `Screens/env_compare_v6.png` (5 pairs, adds a lawn-to-waterline pair; git-ignored).
- Builds at 3:34/3:36 AM CT: APK 44.2 MB (was 43.7), WebGL 41.8 MB (was 40.4). 0 errors, 0 warnings.

### Pass 7: realism + gameplay feel, plus Christopher's houses (Oct 18, 2026, 3:45 AM CT onward)
Commits: f074f40, 437d298, ae84cc8, 0ed7b82, 5d19f81 (7.1–7.4), fc4dde7 + 38cd58e (houses), d70f3a4 (7.5), 385658c + 4721e8a (7.6).
- **Houses (top priority, done first once the README marked all 18 QA-ready)**: `Assets/SkiView/Houses/` holds the 18 `lot-<addr>.fbx`
  with `textures/` beside them. `Editor/SkiViewHouseImport.cs` applies the README import settings: scale 1, Convert Units, no axis bake,
  no compression, lightmap UVs, no rig/animation, materials via MaterialDescription, `nrm_*` as normal maps. Android/WebGL textures
  are capped at 512, impostors at 1024; the README's 1024/2048 added ~38 MB to the APK. `Editor/SkiViewBuilderHouses.cs`:
  - Placement: n is the smoothed shoreline normal pointing into the water. Rotation is `LookRotation(n)` and pivot = S − n·shoreZ,
    with S the lot's venue shore point. 8409 (vacant Lot 15) sits halfway between 8405 and 8415. Lots keep catalog order (N→S)
    and slide along the shoreline until the modelled footprints stop overlapping. The ends (8017, 8505) are pinned, and the hero
    gets 14 m clearance for its grade change. The biggest shifts are 8311 (−32 m) and 8405 (−30 m).
  - Each lot gets URP Lit copies of its slots with the matching `nrm_<kind>` (208 materials, 189 LOD0 slots in total, 6–15 per house).
    Glass is glossy, and 8017's `interior` is emissive #FFB566 (kept low for the afternoon preset).
  - LOD groups use 0.60 / 0.24 / 0.012 transitions. Roots are static (GI, batching, occlusion). A `Dock_<lot>` marker is added
    for camera framing. LOD0 totals 59.5k tris.
  - Terrain grading: each lot gets a flat pad 0.45 m above the water, which blends back to natural ground over 18 m behind, 8 m at
    the sides and near the shore. 8017's street grade is 6.55 m: the terrain steps to the −3.2 m walkout, then down the terrace
    treads to −6.2 m at its shore. Trees and fillers stay off the yards, and rip-rap stays off the dock heads and 8017's built edge.
- 7.1 trees: Perlin clumps (taller cores, smaller edges, fillers, canopy fill), understory shrubs, and fuller dark impostors (`Tools/gen_trees_v7.py`).
- 7.2 rocks: 4 instanced angular rip-rap variants along the rock edging (about 2.1k instances).
- 7.3 lawns: watered green within ~25 m of each house, straw by ~55 m, per-lot factor.
- 7.4 far hills: rings pulled in to 1.0–1.95 km with a darker canopy, so they read through the haze.
- 7.5 rider:
  - Skin: wet skin (smoothness/mottle/droplet map plus a fake-SSS emission) and darker clumped wet hair, warmed back to dark blond.
  - Clothing: the vest's lower row is darker and glossier, and the shorts/rash guard are wet.
  - Spray and rope: a leg-spray emitter at the boots, and rope sag/bounce on a spring.
  - Remy's separate eyeball/eyelash meshes skinned wrongly once the head is levelled toward the boat (one eye sat on the temple),
    so they are hidden; the face texture carries the eyes. Maps come from `Tools/gen_rider_wet.py` (Derived, git-ignored).
- 7.6 camera: the default broadcast camera sits on the boat just ahead of and above the pylon, looking back down the rope
  (TWBC style). It follows with smooth yaw/pitch/zoom (skier ~1/4 of the frame, small lead, wider in turns) and is gimbal-level.
  `C` or the CAM button switches to the old skier chase.
- Sheets (git-ignored): `Screens/env_compare_v7.png` (7 rows, adding the hero 8017 from the lake and an in-game row of 8017's terraces
  plus the house row) and `Screens/rider_compare_closeup_v7.png` (TWBC f_043 ↔ close-up and face, f_011 ↔ broadcast cam).
- Builds 04:29 / 04:30 AM CT: APK 56.4 MB (was 44.2), WebGL 54.0 MB (was 41.8). The growth is the house textures. 0 errors, 0 warnings.

### Pass 8: boat cam, lean check, terraces/docks, sky seam, lot table, hair + Christopher's v2 houses (Oct 18, 2026, 4:33 AM CT onward)
- 8.1 broadcast camera: the camera now sits low in the boat, 0.9 m ahead of the pylon on the rope line extended. It slides round the
  pylon (max 55°) so the pylon top is always directly under the skier. Its height is solved each frame so the pylon ball pokes into
  the bottom centre with the rope running up to the hands. A smoothed look-at keeps the skier centred, and the zoom holds the skier
  at ~38% of the frame height (it goes wider in the turns).
  - **FOV deviation:** at 18–23 m a 50–60° lens makes the skier only ~7–10% of the frame, so the lens is a telephoto (~10° in the
    pass). That matches the reference look; a wide lens can't.
  - **Stern:** at this zoom the stern gunwale sits just below the frame. Showing it needs ~16° and a skier of ~25%.
- 8.2 lean: not a regression. `LogBodyTilt` before and after the close-up reads head-over-ski 32.0°, root roll 33.1° and sim lean 33.0°.
  The pass 7 close-up camera sat in the lean plane, so the lean read as upright. `04_rider_closeup` now shoots across the lean.
- 8.3 terraces/docks: solved by Christopher's v2 houses. 8017 has stacked-stone terraced retaining walls with planted beds, a lit
  stone stair and two-row riprap, and every dock and deck has plank texture and railings. My interim v1 texture overrides were dropped.
- 8.4 white vertical line: this was the Skybox/Panoramic u-wrap seam. Its azimuth (76.3°) equals 180° + the 256.2° sky rotation;
  `LineDiag` ruled out particles and the rope. The background now draws from a cubemap import of the same HDRI (no wrap seam).
  `SkyAlign` image-matched the rotation offset (−90°), so the sun and clouds stay where they were.
  - A very faint (~2–3% contrast) dark streak remains in the water of `env_b` only. The cause is unidentified; it is not the rope.
- 8.6 hair: a strand/clump normal map (`hair_wet_n.png`, bump 1.15) breaks up the single "helmet" highlight. It adds no geometry or
  draw calls. Strand cards were skipped as costlier.
- **v2 houses** (Christopher, drop-in):
  - **Assets:** hv2_lot01..18 textures and 3 materials per lot: body (albedo + normal atlas, smoothness 0.2), glass (opaque,
    smoothness 0.9, metallic 0.2) and impostor. All v1 textures, materials and overrides are removed.
  - **Glass reflections:** 3 baked 128 px reflection probes over the house row feed the glass.
  - **Mobile/web caps:** albedo 1024, normal 512, impostor 1024.
  - **Lot widths:** the ±19.5 m mowing curb/shore edging widens the grading, tree exclusion and rip-rap exclusion per lot.
  - **Lot lines:** neighbours sit 28–43 m apart, so curb ends can overlap. Alternate lots are lifted 1.5 cm to stop z-fighting.
  - **Detail:** LOD0 now totals 444k tris across the 18 lots; the LOD groups still use 0.60/0.24/0.012.
- Sheets (git-ignored): `Screens/env_compare_v8.png` (re-captured with v2: H = 8017 from the lake, I = 8017 terraces + house row)
  and `Screens/rider_compare_closeup_v8.png` (A = close-up across the lean, B = face/hair, C = new boat cam).
- Builds 05:23 / 05:26 AM CT: APK 75.6 MB (was 56.4, +19.2), WebGL 75.4 MB (was 54.0, +21.4). 0 errors, 0 warnings.
  - **Why it grew:** mostly v2 meshes (uncompressed meshes are 31 MB in the build report, and 8017 alone is 8.4 MB) plus
    18 × 1024 albedo atlases and impostors.
  - **Cheap cuts if wanted:** drop the lightmap UV2 on the houses (we light them with probes, no bake), turn on mesh
    compression for the house FBXs (the README asked for none), or take the albedo atlases to 512 on mobile.

#### 8.5 Lot shifts for Scott to confirm (venue.json unchanged)
Lots that the builder moved more than 10 m along the shore to clear neighbouring footprints. The ends (8017, 8505) are pinned.
Positions are world metres (x, z) on the shoreline; "+" along the shore is southward. The gap is measured along the shore to the
lot to the north.

| Lot | venue.json shore | Final shore | Moved | Gap to north neighbour (venue → final) |
|---|---|---|---|---|
| 8035 | (89.0, 241.0) | (99.2, 233.6) | +13.8 m (S) | 28.8 → 42.6 m (to 8017) |
| 8235 | (37.1, −95.4) | (40.2, −77.1) | −18.8 m (N) | 41.7 → 28.0 m (to 8229) |
| 8311 | (31.8, −134.9) | (35.7, −104.5) | −32.0 m (N) | 41.2 → 28.0 m (to 8235) |
| 8317 | (33.9, −153.1) | (26.9, −139.4) | −16.9 m (N) | 24.4 → 39.5 m (to 8311) |
| 8405 | (32.4, −194.8) | (34.0, −164.7) | −30.3 m (N) | 41.9 → 28.5 m (to 8317) |
| 8409 | (29.7, −214.6)* | (31.4, −201.4) | −13.5 m (N) | 20.1 → 36.9 m (to 8405) |
| 8419 | (21.0, −250.9) | (16.7, −260.9) | +10.9 m (S) | 25.5 → 28.5 m (to 8415) |

\* 8409 has no venue.json entry; its start point is halfway between 8405 and 8415. Smaller moves: 8229 −5.1 m and 8415 +7.9 m.

Compared with `reference/ski-view-houses/CATALOG.md`:
- **Order:** N→S lot order matches the street numbers and is preserved.
- **Frontage:** the catalog gives ~40 m (131 ft) per lot (8017: 130 ft = 39.6 m) on ~0.6 ac lots along the 834 m channel.
- **Spacing:** venue.json spaces several pairs only 20–29 m apart (8017–8035, 8311–8317, 8405–8409–8415, 8415–8419). That is too
  tight for 40 m frontage and is why those lots had to move.
- **Span:** the row from 8017 to 8505 spans 606 m, while 18 lots × 40 m would need ~700 m. So either the venue lot row is short,
  or the 8505 end belongs further south along the bowl.
- **Please check** against `aerial-row-esri-z19.jpg` / `roadmap/venue/skiview_layout_diagram.jpg`. Parcel centroids aren't in the
  reference files, so this is the best comparison available.

### Pass 9 (5:27–6:00 AM CT, Oct 18): prop wash, 16° boat cam, close-up lean, size

- **9.1 Prop wash and wake (65f8c7f).** The water shader now finds the nearest point on the wake polyline for each pixel, so there are no more rings at the path points. From that point it builds:
  - **The trough:** a band of turbulent prop wash behind the transom, about 1.2 m wide and growing to about 3.5 m. It fades over about 45 m and has its own foam texture (Tools/gen_foam.py → Environment/Water/foam.png, 512 px, procedural).
    - Its normals boil up a little.
    - It has a turquoise churn colour.
    - Its reflection is cut by up to 95%.
  - **Rolling V crests:** they spread at about 0.17 × boat speed per metre, with an asymmetric slope and foam caps that fade over about 16 m.
  - **Debug view:** SV_WATER_DEBUG=1 outputs a wash/caps/track mask (dbg_15_wake_masks).
  - It shows from the chase cam (14) and the boat cam (15).
- **9.2 Boat cam ~16° (b7ab541).** The mount is 2.2 m behind the pylon, with a 1.3 m near clip and a 1.3° downward tilt. FOV clamps to 14–34°, and the logged FOV is 16.4° at 4:3. The pylon post sits bottom-centre, the stern edge shows at the bottom of the frame, and the skier fills about 25% of the frame height. Yaw is allowed up to 50°, so the seat no longer blocks the skier at the buoy.
- **9.3 Close-up lean (7ac3e3a).** Shot 04 searches azimuths in 5° steps across the front of the chest (three-quarter front view) and keeps the angle with the largest on-screen ski-to-head lean. The log shows 31.9° on screen with the camera 29° off the chest normal.
- **9.4 Size (1156035).** House FBX imports no longer generate UV2: the houses are lit by probes and never lightmapped, so they also lost ContributeGI. Mesh compression is now Low, and the importer version is 4.

  | | before (pass 8) | after (pass 9) |
  |---|---|---|
  | APK (IL2CPP arm64) | 75.6 MB | **69.3 MB** |
  | WebGL (gzip) | 75.4 MB | **66.3 MB** |
  | Meshes (uncompressed) | 31.0 MB | 15.8 MB |
  | lot-8017.fbx | 8.4 MB | 3.7 MB |
  | Textures | 44.1 MB | 44.2 MB |

  The APK is under 70 MB, so the 1024 mobile albedo cap stays (normals stay 512). Lowering albedo to 512 is the next lever if needed.
- **Sheets:** Screens/rider_compare_closeup_v9.png and Screens/env_compare_v9.png (not in git).
- **Still open:**
  - From the 16° boat cam, the trough foam still reads soft and airbrushed next to the reference. It needs finer, higher-contrast foam streaks and more turbulence in the first 10 m.
  - In 15b (at the buoy, with the camera yawed toward the skier), the frame looks across glassy water away from the track, so little wash shows there.
  - A faint streak remains in env_b.

### Assets available (not imported): Mixamo batch 3

Christopher staged these in /workspace/skiview-assets-staging/mixamo/ (see its README):
- **Pull Heavy Object:** in-place. It is probably a better heavy-lean base layer than Pulling A Rope.
- **Pull Heavy Object Start / Stop**
- **Climbing, Stand Up:** for the deep-water start and getting up after a fall.
- **Backflip, Front Flip:** for falls and tricks.

Root motion has to be baked in place on import (SkiViewMixamoImport.cs). Raw and derived files stay out of git. They were not imported in pass 9.

### Pass 10 (5:59–6:32 AM CT, Oct 18): wake break-up, streaky trough, dark pylon, env_b streak hunt

- **10.1 + 10.2 Wake crests and trough (4df7f10).** These are one commit because both steps live in the same Wake() function and foam block of the water shader.
  - **Crests:** they now follow the Kelvin angle (19.5° half-angle, tan = 0.354 per metre behind the transom). They wander a little and get wider as they age.
    - Crest height decays over about 22 m of path.
    - The foam lip is a narrow band about 0.3 m wide, broken into patches by noise and the foam texture. It breaks up more with distance and is gone by about 45 m.
  - **Old path segments decay:** every wake term fades with both path distance and age (exp(-age/6 s) × a cut-off from 40 to 75 m). Nothing persists across the lake as lane lines any more, even if the boat idles.
  - **Trough:** a boat-wide band (1.1 m half-width growing by 0.05 m per metre) that fades by about 60 m.
    - The foam is fine, high-contrast streaks: lateral × along-track noise stretched about 28:1, plus foam-texture lace squashed about 4:1 along the track.
    - It is contrast-stretched so the threshold carves streaks with turquoise gaps rather than one white mass.
    - It is densest and boiling in the first 10 m.
    - The foam pattern is anchored to the water: the along-track coordinate is (time the boat passed) × 15 m/s, so the foam doesn't slide with the boat.
    - A darker, glassy slick lane opens down the centre after about 8–20 m.
  - **Chop:** the hull zone and the water inside the V get choppy normals and about 60% less mirror reflection.
  - **What the two soft blobs were:** in v9 they were the near crest caps. Those caps were a solid 1.3 m-wide band and huge in the 16° lens. The narrow, frothy lip fixes this.
- **10.3 Pylon (b44d878).**
  - At build time, the pylon's chrome triangles (post, swivel cap, tow ball, brace: 680 tris across LOD0 and LOD1) move to their own dark anodised submesh (M_PylonAnodized, #2B2E33). The boat FBX is unchanged.
  - The boat cam crops lower (bcPylonInFrame 0.35 → 0.70), so only the dark pylon top and the rope show at the bottom centre.
  - Trade-off: the stern edge from pass 9 is now out of frame.
- **10.4 env_b streak (diagnostic commit).**
  - The streak is a 2–3 px dark line in the *reflection* only, near (1288, 600–780) px.
  - ReflDiag reflects the eye ray at those pixels. It also lists every renderer smaller than 40 m whose mirror image falls in that column. None does.
  - So the cause is either part of a big mesh (lot-8017 or lot-8035 FBX, ShorelineEdging, terrain) or the sky cubemap. I didn't find it in this pass; the diagnostic stays in the capture.
- **Builds:** APK 69.4 MB (6:27 AM), WebGL 66.3 MB (6:30 AM), with 0 errors and 0 warnings. Sheets: Screens/rider_compare_closeup_v10.png and Screens/env_compare_v10.png (not in git).
- **Still open:**
  - The trough's turquoise churn still shows as a long band across the telephoto close-ups (rows A and B), about 45–60 m of path.
  - In the boat cam, the left crest lip falls just outside the frame.

### Pass 11 (6:31–6:55 AM CT, Oct 18): trough colour, colour fade, boat-cam crop, Mixamo batch 3

- **11.1 Trough colour (6832a06).** The luminous cyan is gone.
  - In the first ~15 m the trough is white aerated water, broken up by textured foam (foam brightness now varies 0.68–1.1 with the foam texture).
  - The gaps show darker, slightly green-brown lake water: (0.105, 0.125, 0.095) × light.
  - The bubble tint is neutral grey-green.
- **11.2 Colour fade (c76e62a).** The trough colour and bubble tint now fade out by ~30 m (smoothstep from 32 m to 10 m), so the rows A/B telephoto backgrounds no longer show a tinted band.
- **11.3 Boat-cam crop (ca630be, bcPylonInFrame 0.42).** The frame shows the dark pylon top plus a sliver of the stern edge along the bottom.
- **11.4 Mixamo batch 3 (68b5cf7).**
  - **Imported:** Pull Heavy Object (+ Start, Stop), Stand Up, Falling Forward Death and Falling Back Death went into ThirdParty/Mixamo. Raw FBX files stay ignored; restore them with the cp line in .gitignore.
  - **Importer:** one-shot clips (starts, stops, falls, stand-up) no longer loop.
  - **Lean base:** Pull Heavy Object (in place) replaces Pulling A Rope as the lean layer. The close-up still reads 33.9° on screen.
  - **State machine:** `Mode.DeepStart` is new.
    - A fall plays Falling Forward Death upright on the water. The skier then sinks to treading depth (Treading Water, head and shoulders out) while the boat idles back to 30% throttle.
    - After 3.4 s comes the deep-water start. The skier sits back about 55° in the water facing the boat, knees to chest (stance IK crouch 0.34), with the ski tip out. Pull Heavy Object Start is the upper-body base. The boat throttles from 25% to 100% over 3.2 s while the skier rises and comes over the ski, then the game returns to Turn mode.
    - The game opens with a deep-water start in play mode. Headless captures skip it so their timing is unchanged.
  - **Fix:** the old fallen path left a PlantFeet offset on the model after recovery; the rig now resets it.
  - **Capture stills:** fall_1–3 and dws_1–4. Sheet: Screens/dws_fall_v11.png (Tools/make_dws_fall_sheet.py).
  - **Not wired yet:** Stand Up, Pull Heavy Object Stop, Climbing and the flips. Stand Up is in the mixer but has no state yet.
- **Builds:** APK 69.3 MB (6:48 AM CT) and WebGL 66.2 MB (6:52 AM CT), 0 errors and 0 warnings. Animations 0.33 MB.
- **Sheets:** env_compare_v11, rider_compare_closeup_v11, dws_fall_v11 (not in git).

### Pass 12 (6:53–7:20 AM CT, Oct 18): water reacting to falls and deep-water starts, real buoy size

- **12.1 Crash splash + treading (8656ce5).**
  - At impact (0.85 s into the fall) a burst throws up a spray sheet (soft particles) and droplets (streaks), forward along the fall.
  - The water shader has new splash sources (`_SVSplash[6]`: x, z, start time, strength). A crash leaves a short-lived broken-up foam patch (gone in ~3 s) and two expanding ring ripples.
  - The ski pops free: it arcs ~1 m up off the feet, lands ~1.5 m to the side with its own small splash, then floats flat, drifting and bobbing. The stance puts it back on the feet at the deep-water start.
  - The skier sinks faster after impact (0.75–1.9 s) to treading depth 1.32 m (water line at the shoulders), with continuous ripple rings and a thin foam collar around him.
- **12.2 Deep-water start (same commit).**
  - While seated: continuous small ripple rings. When the pull starts, a churned foam patch is left where he sat.
  - Plow spray (bow wave) off the ski tip and off the chest/knees, scaled by depth × speed, until the ski planes.
  - Start 1 sits lower (0.57 m) and tilted back 45°, the reference "ball" position: head, shoulders, knees and ski tip above the water, ski tip at about 45°, rope inside the ski, facing the boat.
- **12.3 Buoys (fb26573).** Real slalom size (IWWF 8.07 / AWSA): 23 cm balls. Turn buoys show 9 cm above the water; gates, pre-gates and boat guides 11.5 cm (half submerged). The old balls were 28–40 cm and rode high.
- **12.4 Stand Up: skipped.** The Mixamo clip is a face-down push-up to standing on land ("Looking Over Ledge To Standing Up"). It doesn't fit the water; the deep-water start is the recovery.
- **12.5 env_b streak (b9fc78e, diagnostic only, `SV_STREAK_TEST=1` env test).**
  - Rotating the sky 30° removes the streak from that spot.
  - Hiding every renderer over 40 m (house lots, the SkylineFringe ring) plus the terrain heightmap leaves it in place.
  - Turning off the reflection's oblique projection leaves it in place.
  - A direct view of the sky along the reflected ray shows no visible seam.
  - So it is tied to the sky, but not an obvious texture seam. Terrain tree billboards weren't tested (they aren't Renderers). Not fixed.
- **Builds:** APK 69.3 MB (7:11 AM CT), WebGL 66.2 MB (7:15 AM CT), 0 errors and 0 warnings.
- **Sheets:** rider_compare_closeup_v12, dws_fall_v12, env_compare_v12 (not in git).
