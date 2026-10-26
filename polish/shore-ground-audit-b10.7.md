# Shore / turn-island ground audit — b10.7 (post–b10.5 dirt pass)

**Date:** 2026-10-23 ~22:32 America/Chicago (CDT)  
**Author:** Grok Bot (file-only re-audit + mechanical YAML mat/layer edits; no Unity Editor, no builds, no C# rewrites)  
**Ask:** Scott (via Karthik) — Ski View ground STILL looks like water/snow after b10.5 dirt pass. Karthik doing b10.7 turnaround + shores.  
**Prior audit:** `/workspace/skiview-assets-staging/polish/shore-ground-audit.md` (pre–b10.5; ShoreRock was still white on disk)  
**Scope:** live `/workspace/skiview-unity` only. Did **not** touch `/workspace/political-kombat-unity`.  
**Unity processes during audit:** none (`pgrep` clean). Box free ≈ 22 GB.

---

## 1. What still looks like snow / water / white AFTER b10.5

### 1a. Smoking guns from prior audit — **FIXED on disk by b10.5**

These match the PNW dirt/grass recipe now (verified 2026-10-23 22:28 CDT). Do **not** re-white them.

| Asset | On-disk NOW | Notes |
|---|---|---|
| `Assets/SkiView/Environment/Materials/ShoreRock.mat` | `_BaseColor (0.58, 0.46, 0.34)`, `_Smoothness 0.08`, `_EnvironmentReflections 0`, `_SpecularHighlights 0` | Was `(1,1,1)` / 0.25 |
| `Assets/SkiView/Environment/Materials/ShoreGrass.mat` | `(0.62, 0.78, 0.44)` / 0.04, env+spec off | Was pale lime |
| `Assets/SkiView/Environment/Materials/ShoreMud.mat` | `(0.50, 0.38, 0.27)` / 0.08, env+spec off | OK |
| `Assets/SkiView/Environment/Materials/ShoreSandWet.mat` | `(0.80, 0.72, 0.58)` / **0.08**, env+spec off | Was near-white / 0.35; builder still asks smooth 0.22 |
| `Assets/SkiView/Environment/Materials/RipRap.mat` | `(0.55, 0.50, 0.44)` / 0.08, env off | Was cooler/glossier |
| Home `TL_Lawn` | RemapMax `(0.70, 0.88, 0.50)`, smooth 0.04, `SmoothnessSource=Constant (1)` | Darkened in b10.5 |
| Home `TL_DryGrass` | `(0.66, 0.80, 0.46)` / 0.03 | OK |
| Home `TL_Forest` | `(0.74, 0.65, 0.55)` / 0.03 | OK (island duff) |
| Home `TL_Mud` | `(0.58, 0.46, 0.34)` / 0.06 | Matches ShoreRock dirt |
| Lake `TL_Sand` (pre-edit) | `(0.96, 0.88, 0.74)` | Was 1.14 snow; still bright on hot texels |
| Lake `TL_Gravel` / `TL_Rough` | dirt/grass remaps | Fixed in b10.5 |

Home `ShorelineEdging` in `SkiViewLake.unity` correctly references ShoreRock / ShoreGrass / ShoreMud by GUID (`025e4fcf…`, `b3e91b72…`, `5d08cd24…`). Scene mtime 11:35 CDT is older than mat rewrite 20:21, but materials are GUID refs so disk values win when loaded.

Builder intent already matches (`ShoreRockMat()` in `SkiViewBuilderShore.cs` comment `b10.5: dirt, no env reflections`). Home + lake builders set `basemapDistance = 20000`. `SkiViewTerrainFix.BASEMAP_DISTANCE = 20000`.

**Effective albedo (texture mean × tint) for home island ground is already dark** (ShoreRock ≈ 0.14 mean RGB; TL_Mud ≈ 0.23; TL_Forest ≈ 0.31). If Scott still sees **white** under trees with these mats in the player, the white is **not** coming from ShoreRock/Mud albedo alone — see residual risks.

### 1b. Still pale / bloom-capable on disk BEFORE this b10.7 YAML pass (and why)

| Asset (pre-edit) | Value | Why it reads snow/water |
|---|---|---|
| Lake `TL_Lawn` gulf-style (`gulfwind`, `marrowind`, `saltwhite`, `villaserena`) | RemapMax **`(0.98, 1.06, 0.78)`** | **Green channel > 1.0** blooms lawn bright in sun; can read frost/snow from water |
| Lake `TL_Lawn` SE (`cedarbend`, `tallpine`, `heronmarsh`, `harrowpond`) | **`(0.84, 1.02, 0.56)`** | Same bloom |
| `LawnBox.mat` | `(0.95, 0.90, 0.72)` / 0.05 | Pale straw terrace boxes; visible from lake |
| `TallGrass.mat` | `(0.92, 0.95, 0.85)` / 0.1 | Frosted grass cards (lake kit) |
| `Reeds.mat` | `(0.86, 0.92, 0.80)` / 0.15 | Pale shoreline reeds |
| `SeaOats.mat` | `(0.95, 0.95, 0.85)` / 0.1 | Gulf shore grass |
| Lake `TL_Sand` | `(0.96, 0.88, 0.74)` on `sand_white_color.jpg` (mean ≈ 0.75, max ≈ 0.96) | Worst texel × tint ≈ **(0.92, 0.83, 0.62)** — still chalky sand/snow in hard sun |
| `saguaro` `TL_DesertFloor` | `(0.98, 0.84, 0.66)` | Intentional desert; still very bright |

Builder **still writes** the bloom lawns on every lake rebuild:

```csharp
// SkiViewBuilderLakes.cs ~330 / ~337
LakeLayer("Lawn", ..., new Color(0.98f, 1.06f, 0.78f));  // gulf
LakeLayer("Lawn", ..., new Color(0.84f, 1.02f, 0.56f));  // SE
```

`LawnBoxMat()` / TallGrass / Reeds / SeaOats constructors in Env / LakeKit / GulfKit still pass the pale colors — **YAML edits will be overwritten** if those builders re-run without C# updates.

### 1c. Not the smoking gun (but can confuse the eye)

- `BackdropFarm.mat` uses `Texture2D.whiteTexture` × straw tint — far farm skirt, not island under trees.
- Building whites (`TrimWhite`, stucco, gelcoat) — not ground.
- Diag shots under `Screens/diag/ground/after3_*` are from **~06:46 CDT** (pre–b10.5); not valid for post-dirt verification. Need `after4_*` after b10.7.

---

## 2. Concrete dirt / grass material recipe (b10.7 — apply so it wins)

### Shader
- Strip meshes: `Universal Render Pipeline/Lit`
- Terrain: `Universal Render Pipeline/Terrain/Lit` via `Assets/SkiView/Materials/TerrainLit.mat`
- Always: **metallic 0**, **`smoothnessSource = Constant`** on terrain layers (never DiffuseAlpha), **env reflections + specular highlights OFF** on shore strips.

### Color / PBR table (Pacific Northwest lakeshore)

| Role | `_BaseColor` / `diffuseRemapMax` | Smoothness | Exact files |
|---|---|---|---|
| Waterline dirt / bank rock | **`(0.58, 0.46, 0.34)`** | **0.06–0.08** | `ShoreRock.mat`, `TL_Mud.terrainlayer`, lake `TL_Gravel` |
| Island / bank mud strip | **`(0.50, 0.38, 0.27)`** | **0.08** | `ShoreMud.mat` |
| Bank / field grass | **`(0.62–0.70, 0.78–0.88, 0.44–0.50)`** — **never any channel > 1.0** | **0.03–0.05** | `ShoreGrass.mat`, home `TL_Lawn` / `TL_DryGrass`, lake `TL_Rough`, `LawnBox`, `TallGrass` |
| Fir duff under island trees | **`(0.70–0.78, 0.60–0.70, 0.50–0.60)`** | **0.03** | home `TL_Forest` |
| Warm sand (gulf/FL only) | **`(0.88, 0.80, 0.66)`** (prefer over 0.96) | **≤ 0.08** | lake `TL_Sand`, `ShoreSandWet` |
| Rip-rap | **`(0.55, 0.50, 0.44)`** | **≤ 0.08** | `RipRap.mat` |

### Maps (already in project)
- Dirt/rock strip: `Environment/Textures/shore_strip_rock.png` + `shore_normal.png`
- Mud/terrain: `shore_color.jpg` + `shore_normal.png`
- Grass strip: `shore_strip_grass.png` + `lawn_normal.png`
- Lawn / drygrass / forest: `lawn_color.jpg`, `drygrass_color.jpg`, `forest_color.jpg` (+ normals)
- Gulf sand: `sand_white_color.jpg` — **only** with warm remap ≤ ~0.88; never RemapMax 1.14

### Where it must win (home island turn)
1. Terrain splat on `islandCell`: **Forest + Mud only** (already in `SkiViewBuilder.BuildTerrain`); mud near waterline 0.3–1.0 m.
2. `ShorelineEdging` islands → **ShoreMud** only; banks → ShoreRock / ShoreGrass.
3. No Lawn/DryGrass on island cells; no pale TallGrass cards on the waterline if they read frost.
4. Runtime: keep `SkiViewTerrainFix`; builder `basemapDistance = 20000`.
5. After any mat/layer change: **rebuild Addressables + WebGL** (b10.5 already did once at ~20:26 / 21:09 CDT — re-do after this YAML pass).

---

## 3. What changed on disk THIS pass vs what Karthik must apply

### 3a. Edited on disk now (backups under `/workspace/pk-backup/skiview-shore-b107/`)

| File | Was → Now |
|---|---|
| `Environment/Materials/LawnBox.mat` | `(0.95, 0.90, 0.72)` / 0.05 → **`(0.70, 0.88, 0.50)` / 0.04** |
| `Environment/Materials/TallGrass.mat` | `(0.92, 0.95, 0.85)` / 0.1 → **`(0.62, 0.78, 0.44)` / 0.05** |
| `Environment/Materials/Reeds.mat` | `(0.86, 0.92, 0.80)` / 0.15 → **`(0.55, 0.68, 0.42)` / 0.08** |
| `Environment/Materials/SeaOats.mat` | `(0.95, 0.95, 0.85)` / 0.1 → **`(0.72, 0.68, 0.48)` / 0.06** |
| `Lakes/{gulfwind,marrowind,saltwhite,villaserena}/Materials/TL_Lawn.terrainlayer` | `(0.98, 1.06, 0.78)` → **`(0.84, 0.92, 0.62)`** |
| `Lakes/{cedarbend,tallpine,heronmarsh,harrowpond}/Materials/TL_Lawn.terrainlayer` | `(0.84, 1.02, 0.56)` → **`(0.78, 0.92, 0.52)`** |
| `Lakes/{gulfwind,marrowind,saltwhite,villaserena}/Materials/TL_Sand.terrainlayer` | `(0.96, 0.88, 0.74)` → **`(0.88, 0.80, 0.66)`** |
| `Lakes/saguaro/Materials/TL_DesertFloor.terrainlayer` | `(0.98, 0.84, 0.66)` → **`(0.88, 0.74, 0.58)`** |

**Not edited (already at recipe):** ShoreRock, ShoreGrass, ShoreMud, ShoreSandWet, RipRap, home `TL_*`.

**No C# edited** (document-only below).

### 3b. Karthik must apply in the Unity / b10.7 pass

1. **Lock builders so they stop re-blooming lawn** (one-line tint changes; otherwise next lake build undoes YAML):
   - `SkiViewBuilderLakes.cs` ~330: gulf Lawn → `new Color(0.84f, 0.92f, 0.62f)` (or home `0.70, 0.88, 0.50`)
   - `SkiViewBuilderLakes.cs` ~337: SE Lawn → `new Color(0.78f, 0.92f, 0.52f)`
   - Same file Sand line ~331: `new Color(0.88f, 0.80f, 0.66f)` to match YAML
   - `SkiViewBuilderEnv.cs` `LawnBoxMat()`: tint → `(0.70f, 0.88f, 0.50f)`, smooth 0.04
   - `SkiViewBuilderLakeKit.cs` TallGrass / Reeds: match table above
   - `SkiViewBuilderGulfKit.cs` SeaOats: match table above
   - Optional: `ShoreSandWet` EnvMat smooth 0.22 → **0.08** to match disk

2. **Rebuild Addressable lake + home groups**, then WebGL/player, so Scott is not on a cached b10.5 (or older) data blob. Purge CDN / hard-refresh if hosted.

3. **Re-capture** `SkiViewDiagGates` ground set → `Screens/diag/ground/after4_*` especially `island1/2_low` from the water (Scott’s island-turn shot). Compare to live screenshot.

4. **Turnaround path (b10.7 focus):** confirm boat circle stays clear of island and camera does not skim a far basemap / BackdropFarm straw disc that reads as white ground. Ground mats alone cannot fix a path that clips the bank.

5. **Do not** call `EnvMat("ShoreRock", … Color.white …)` again — only `ShoreRockMat()`.

6. If Editor still shows white under island trees after mats load: inspect Terrain alphamap on `islandCell` (must be Forest+Mud). Re-run home `BuildTerrain` / `BuildShoreline` only if splat is wrong; mats are already correct.

---

## 4. Residual risks

| Risk | Detail |
|---|---|
| **Builders overwrite YAML** | Lake/home EnvMat / LakeLayer still encode pale lawn (>1.0 G) and old sand/LawnBox/TallGrass colors. Fix C# or every rebuild undoes §3a. |
| **WebGL / Addressables / browser cache** | b10.5 already rebuilt aa (~20:26) + WebGL.data (~21:09). Scott “still snow” may be stale CDN/browser cache, or white from §1b bloom assets that shipped in that build. Re-bundle after this pass + hard refresh. |
| **WebGL terrain basemap** | If `SkiViewTerrainFix` missing from a player assembly, far banks go white/grey again. Builder now sets 20000; keep the runtime hook. |
| **ShorelineEdging `ReflectionProbeUsage: 1`** | Mats have env reflections off; still worth setting probe usage Off on the strip renderer if banks look wet/watery. |
| **`sand_white_color.jpg`** | Intrinsically bright; remap must stay ≤ ~0.88. Never 1.14. |
| **`drygrass_color.jpg` max texel (1,1,0.8)** | × DryGrass remap can hit ~0.66–0.80 — green, not snow, but hot. |
| **No post–b10.5 diag** | Cannot prove visual from `after3_*`. Need after4. |
| **Binary TerrainData alphamaps** | Not decoded this pass. Island Forest+Mud is in builder source; cooked `SkiViewTerrain.asset` (mtime 11:34) predates evening mat pass but layer *colors* are external refs so remaps should apply without rebake. Wrong splat weights need a builder re-run. |
| **Far BackdropFarm straw** | WhiteTexture × straw — can look like pale ground at distance on some lakes; not home island under trees. |
| **Box disk** | ~22 GB free (84% used). Avoid heavy Unity if Karthik is building. |

---

## 5. Quick reference — paths

```
# Already good (b10.5)
Assets/SkiView/Environment/Materials/ShoreRock.mat
Assets/SkiView/Environment/Materials/ShoreGrass.mat
Assets/SkiView/Environment/Materials/ShoreMud.mat
Assets/SkiView/Environment/Materials/ShoreSandWet.mat
Assets/SkiView/Environment/Materials/RipRap.mat
Assets/SkiView/Materials/TL_{Lawn,DryGrass,Forest,Mud}.terrainlayer

# Edited this pass (b10.7 YAML)
Assets/SkiView/Environment/Materials/{LawnBox,TallGrass,Reeds,SeaOats}.mat
Assets/SkiView/Lakes/*/Materials/TL_Lawn.terrainlayer   (8 lakes)
Assets/SkiView/Lakes/{gulfwind,marrowind,saltwhite,villaserena}/Materials/TL_Sand.terrainlayer
Assets/SkiView/Lakes/saguaro/Materials/TL_DesertFloor.terrainlayer

# Backups
/workspace/pk-backup/skiview-shore-b107/

# Source of truth / overwrite risk
Assets/SkiView/Editor/SkiViewBuilderShore.cs   # ShoreRockMat
Assets/SkiView/Editor/SkiViewBuilderEnv.cs     # TerrainLayersV5, LawnBoxMat, EnvMat
Assets/SkiView/Editor/SkiViewBuilderLakes.cs   # LakeLayer lawn/sand (still >1.0 G until C# fix)
Assets/SkiView/Editor/SkiViewBuilderLakeKit.cs # TallGrass / Reeds
Assets/SkiView/Editor/SkiViewBuilderGulfKit.cs # SeaOats
Assets/SkiView/Scripts/SkiViewTerrainFix.cs    # WebGL basemap
```

---

## 6. Out of scope

- No Unity Editor, no builds, no Addressable rebuild, no C# commits.
- No edits under `/workspace/political-kombat-unity` or Filibuster Fighters.
- Water surface mats intentionally excluded (Scott’s report is ground under trees).
