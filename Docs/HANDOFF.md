# PARASITE — Developer handoff (read this first)

Unity 6.3 LTS (6000.3.29f1) URP third-person stealth game. You are an amoeba (Specimen 7) that escaped Vale Biotech,
possesses humans, splits in two, and works through a 5-mission campaign to confront Dr. Vale.
Repo: https://github.com/Mevinb/gamajam-v2 (remote `origin`, branch `main`). Old repo `gamejam-old` = github.com/Mevinb/gamejam.

## 1. Environment (this laptop: Ubuntu 26.04, RTX 4050 6 GB, 16 GB RAM)

| Thing | Where |
|---|---|
| Project | `~/Data/Parasite` |
| Unity editor | `~/Data/Unity/Editor/6000.3.29f1/Editor/Unity` (Linux Mono + Windows Mono build support installed) |
| Unity Hub | `~/Data/Unity/Hub/usr/lib/unityhub/unityhub` (install path set to `~/Data/Unity/Editor`; licence = Unity Personal, active) |
| Linux build | `~/Data/Parasite/Builds/Linux/Parasite.x86_64`, launch with `./play.sh` (forces the NVIDIA GPU) |
| Disk | **`~/Data` has only ~5 GB free.** Don't download big assets; delete zips after extracting. Library ≈ 3 GB, build ≈ 640 MB. |

Never open the project in the Editor GUI while a batch run is going (one Unity per project).

## 2. Everyday commands (run from `~/Data/Parasite`)

```bash
Tools/compile_check.sh all          # 1 s Roslyn compile of Scripts + Editor (no Unity). Run after every edit.
Tools/unity.sh <log> <args>         # serialized (flock) Unity batch run on the RTX; log → Logs/<log>.log

Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch   # regenerate Bootstrap+City (~30 s warm cache)
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml   # acceptance suite (~5 min)
grep -E '\[AutoTest\] (PASS|FAIL)' Logs/playmode.log
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml   # 22 rule/audio tests
Tools/unity.sh ui-playmode -runTests -testPlatform PlayMode -testFilter Parasite.Tests.SettingsUITests -testResults Logs/ui-playmode-results.xml
Tools/unity.sh photos -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch   # 15 preset renders → Logs/photos/*.png (add -photoFilter 07)
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux       # Builds/Linux (last cached build 11.33 s; cold shaders can be much slower)
Tools/unity.sh build-win -executeMethod Parasite.EditorTools.BuildAutomation.BuildWindows       # Builds/Windows/Parasite.exe
./play.sh -showfps                  # play; F3 FPS, F1 controls, Esc pause
Builds/Linux/Parasite.x86_64 -autotest -autotestOut /tmp/report.txt    # run the acceptance scenarios inside the real build
Builds/Linux/Parasite.x86_64 -benchmark -autotestOut /tmp/bench.txt    # walk the city, log FPS/memory, screenshots
```

**Order after changing anything in `Assets/Parasite/Editor/**` or `MissionLayout/CityLayout`:** compile_check → buildscenes → playmode → build-linux.
Scenes are *generated*; never hand-edit `City.unity`/`Bootstrap.unity` (they're rebuilt from code).

Use available desktop-control tooling against the running game window ("PARASITE") for physical keyboard/mouse
playtesting. That integration may differ between agent sessions; the automated PlayMode tests do not require it.
`Tools/display.sh` supplies the desktop Xwayland environment and NVIDIA offload variables for batch runs and `play.sh`.
Respect an explicitly supplied DISPLAY/XAUTHORITY (including Xvfb); don't open another Unity process on this project.

## 3. Git rules (user's request)

Commit **only as Mevinb** (`git config user.name` is already Mevinb). **No `Co-Authored-By: Devin`/"Generated with" trailers.**
Commit + push after every working step (`git push origin main`). Don't commit `Library/`, `Builds/`, `Logs/` (in .gitignore).

## 4. Architecture (all runtime code in `Assets/Parasite/Scripts`, asmdef `Parasite.Runtime`)

- **Boot:** `World/CityDirector` (only object in City scene besides markers) → adds GameManager, SuspicionSystem, MissionManager,
  PossessionManager, AbilitySystem; bakes the NavMesh at runtime from colliders under root `World`; spawns NPCs from `NpcSpawn`
  markers via `Characters/HostFactory`; creates `PlayerCamera`, `TrafficSystem`, HUD, ambience.
- **Core:** `GameTypes.cs` (enums, ProfessionCatalog, SuspicionRules/SplitRules/AccessRules pure logic, IControllable, IInteractable,
  GameEvents hub), `GameInput.cs` (code-defined Input System actions + `Sim*` flags for tests), `GameManager.cs` (pause, checkpoints,
  `RunState` static: flags, mission index, stats — survives checkpoint reloads), `MatFactory.cs` (runtime materials cloned from
  `Resources/Materials/*Template` so shader variants survive stripping), `GraphicsPreset.cs`, `AudioPreferences.cs`,
  `AutoTest.cs` (acceptance scenarios and benchmark tour).
- **Possession:** `PossessionManager` (max 2 `AmoebaAgent`s, targeting/aim cone 15° within 5 m, E/Q/V/Tab, launch arc, capture → eject,
  containment → checkpoint), `AmoebaController` (blob), `PlayerCamera` (Cinemachine 3 ThirdPersonFollow on a pivot), `AbilitySystem` (F).
- **Characters:** `HostCharacter` (NavMeshAgent when AI, CharacterController when possessed; exactly one of CC/HitBox enabled),
  `HostBrain` (wander/patrol/stationary, notice, flee), `GuardAI` (Patrol→Investigate→Pursue→Capture), `HumanoidRig` (procedural
  body, fabric textures from `Resources/Textures/Clothes`, procedural animation).
- **Stealth:** `SuspicionSystem` (0–100; only witnessed violations count; decay; guard alerts; `ReportDirect` for cameras),
  `SecurityCamera` (sweeping CCTV; maintenance F disables).
- **Missions:** `MissionManager` (5 missions defined in `BuildCampaign()`; objectives keyed by flags; waypoints; banner),
  `MissionFlags` (string constants), `MissionProp` (generic E / hold-E interactable that sets a flag, with host/permission/prereq gates),
  `EavesdropZone`, `DualAuthReader` (two-person rule → needs split), `AccessDoor` (permission + `requiredFlag` + `openedFlag`),
  `StaffTerminal` (needs keycard flag), `DrValeInteraction` (ending).
- **World data:** `CityLayout.cs` (key coordinates — CityBuilder builds from these), `MissionLayout.cs` (mission prop + camera positions),
  marker components (one MonoBehaviour per file — **Unity requires file name == class name for scene serialization**).
- **UI:** `HUDController*.cs` (built in code, uGUI legacy Text, Rajdhani font): suspicion, objective (rich text), body/slots, prompts,
  hints incl. [F], toasts, subtitles, world markers, waypoints + compass (`.Mission.cs`), pause (+graphics preset, journal), end screen.
  `MainMenuController.cs` (Bootstrap scene), `SettingsOverlay.cs` (shared title/pause graphics, audio and sensitivity controls).
  `UIKit.EnsureEventSystem()` uses the Input System UI module; Escape closes a pause submenu before resuming gameplay.
- **World text:** `World/WorldTextFont` binds the dynamic font atlas to `Shaders/WorldText.shader` (depth tested, back-face culled,
  alpha + vertex colour). It refreshes after `Font.textureRebuilt`; runtime material clones leave UI font materials untouched.
- **FX:** `FxApi.cs` public API (Sfx/Fx), `FxBackend/SfxBackend/SfxSynth/FxAssets`, `AmoebaVisual` (+ `HostGlow`), shaders
  `Shaders/Amoeba.shader`, `AmoebaNucleus.shader`; materials generated by `Editor/FxSetup.cs`.
- **Editor (asmdef `Parasite.Editor`):** `CityBuilder.cs` + `World/*` (procedural city/lab, materials, props, vehicles, signs),
  `MissionPlacement.cs` (mission props, cameras, extra NPCs, door flag wiring — called at end of `CityBuilder.Gameplay()`),
  `ProjectSetup.cs`, `FxSetup.cs`, `PhotoTool.cs`, `BuildAutomation.cs`.

## 5. How to add things

- **New mission / objective:** add flag constant in `MissionFlags`, add a `Mission` in `MissionManager.BuildCampaign()`, place the prop in
  `MissionPlacement.Place()` (position in `MissionLayout`), call `MissionManager.SetFlag(flag)` from whatever completes it. Add an
  AutoTest scenario (`UseProp(flag, wait)` helper exists) and run the suite.
- **New NPC:** `Npc(...)` line in `CityBuilder.Gameplay()` or a `NpcSpawn` in `MissionPlacement.ExtraNpcs()`; must stand on NavMesh.
- **New interactable:** implement `IInteractable` (+ `Interactables.Register` in OnEnable). Targeting/prompt/E handling is automatic.
- **New profession ability:** `AbilitySystem.Info()` + switch in `TryUse()`.
- **New sound/effect:** extend `SfxId` (append only) and `SfxSynth`/`SfxBackend`; effects via `Fx.*`.

## 6. Current state (21 Nov 2026, verification in progress)

- CPU compile: **60 runtime files + 11 editor files PASS**. EditMode: **22/22 PASS**, including persisted audio/mute checks.
- Final regenerated-scene PlayMode suite: **2/2 test cases PASS**, including **13/13 gameplay scenarios** and the UI test.
  Covers CCTV, profession abilities, cooldowns, camera occlusion and pursuit refusal, with traffic active.
  Standalone Linux acceptance also reports **13/13 PASS**, process exit 0. See `Logs/playmode-results.xml`,
  `Logs/playmode.log` and `Logs/standalone-acceptance.txt`.
- Filtered `SettingsUITests` **PASS**: title/pause controls, live volume, saved preferences, real queued Escape input and paused
  gameplay isolation. UI captures in `Logs/ui/` were rendered from the actual canvases. This proves automated input routing,
  not a physical controller or Windows playtest.
- Visual overhaul reviewed on the RTX 4050: **15 fresh environment renders** in `Logs/photos/`, with the baseline retained in
  `Logs/photos-before/`. Photo audits report **zero missing scripts**. Gate/staff shots `07`/`08` confirm stray mirrored text
  through walls is fixed and intended signs remain readable. Main facade detail and fixture silhouettes remain intact at the
  reviewed viewpoints. Trees are still card-based; puddle edges still need refinement.
- Final generated city: **1,391 merged renderers, 482,691 triangles, 277,497 prop triangles**. Authoritative saved-scene baseline
  was 965,633 triangles / 760,253 prop triangles: **50.01% fewer total triangles, 63.50% fewer prop triangles**. The older 5.8 M
  figure predates an earlier LOD pass and is not the baseline for these reductions. `Logs/geometry-before.json` and
  `Logs/geometry-after.json` record exact scene hashes/counts; PhotoTool also counts additional gameplay mesh instances.
- Repeated lamps, hydrants and road barriers use selected imported mesh LODs; buildings and texture/normal maps are retained.
  Build logs include per-model instance/triangle totals before baking. The storm drain is now a flush dark slotted grille.
  Imported crates match requested collider dimensions; their old RNG draw is preserved to keep downstream city variation stable.
  The bus shelter is already north of Market St and leaves the start-alley mouth clear.
- Scene generation now takes **about 30 seconds with a warm cache**. Removing `ContributeGI` from static geometry, while both GI
  modes remain disabled, cut recorded light-baker scene input from **6,134.55 MB to 1.50 MB** and precompute from **64.07 s to
  0.28 s**. Environment/reflection baking still runs on the RTX; CPU compile/review work can run alongside it.
- Third-party model/texture attribution is recorded in `Docs/THIRD_PARTY_ASSETS.md`, including the previously omitted model kits.
  Visual overhaul/assets were committed and pushed as `1557cca`. Check `git status` for remaining verification/docs changes.
- Current Linux build **SUCCEEDED**, **425 MB**, **11.33 seconds** with the shader/build cache, **zero errors and zero warnings**.
- Standalone RTX benchmark: **180.0 FPS average at actual 1920×1080, High, render scale 1.00**, vSync off; 19,635 frames over
  109.1 s. P99 frame time **17.11 ms**, worst **42.1 ms**, so occasional hitches remain. All three entry approaches were reached;
  the final return-to-spawn leg timed out after 35 s. See `Logs/benchmark-1080p/report.txt` and `Docs/TEST_REPORT.md` for scope.
- **Still pending:** Windows packaging is compiling its cold Direct3D shader cache. Distinguish packaging success from actual
  Windows execution. Check its actual Unity process/log before starting another editor; do not restart because the log is quiet.

## 7. Known issues / next steps (priority order)

1. Finish the live Windows build for judges; check its real process and shader progress before any new editor run. Package builds
   with `Tools/package_build.sh linux|windows` after verifying their terminal results. Test Windows on Windows before claiming compatibility.
2. Improve occasional frame-time hitches and the timed-out return leg of the benchmark tour. Preserve the current native High
   quality while profiling; the high mean FPS is not proof of constant frame delivery.
3. Continue the ambitious visual pass (user wants "GTA-like"): humans are still procedural primitives. Real rigged CC0 humans + Humanoid animations would be the biggest visual win
   (Mixamo/Asset Store require login. Older Quaternius modular men/women packs advertise CC0 with small Humanoid FBXs; candidate
   sources and official-page evidence are in `Logs/character-candidates/provenance.md`, but included licence downloads currently
   hit Drive quota restrictions. No models were imported. Verify the exact included licence before choosing that candidate.)
4. Refine angular/card-like tree crowns, repeated storefront interior patterns and rectangular puddle/reflection edges; three older
   service/tunnel puddles still use solid rectangular geometry. Preserve geometry/material quality and compare close-up renders
   when tuning LODs. Existing 15 shots are environment views, not proof of every runtime animation or close-range angle.
5. Balance: blob sighting rate (`SuspicionRules.BaseAmount(BlobSighted)` = 6/s), guard speed vs blob, camera ranges; verify through
   real play sessions as well as scripted acceptance.
6. Nice-to-have: minimap, more side missions, NPC conversation pairs, rain/wet streets. Audio volume persistence is implemented.

## 8. Gotchas learned

- Unity 6 batch needs the licence: if runs fail with licensing errors, open Unity Hub and sign in.
- One MonoBehaviour per file (else "missing script" in generated scenes).
- `Physics.IgnoreCollision` resets when a collider is disabled — that's why HostCharacter toggles CC vs HitBox instead.
- Unity `?.`/`??` on UnityEngine.Object is unsafe (fake null) — use `if (!obj)`.
- Python urllib gets 403 from Poly Haven/ambientCG — use `curl -A "Mozilla/5.0"`.
- `pkill -f <pattern>` inside the same shell command line kills that shell — use `pgrep` + `kill <pid>` or a different pattern.
- First build after shader/material changes compiles ~1,600 shader variants (CPU, ~30 min); later builds use the cache.
- Both GI modes are disabled. Keep `ContributeGI` off generated geometry unless intentionally introducing baked GI; enabling it
  sends all city geometry through expensive light-baker preprocessing even when no lightmaps are requested. Do not skip the
  remaining environment/reflection bake to save time: it changes visible lighting/reflections.
- Run GPU jobs only via `Tools/unity.sh`; its `flock --no-fork` serializes the project without a separate flock parent process.
  Poll the actual process/session rather than treating an old lock file as proof Unity is running.
- `WorldKit.Text()` uses a separate depth-tested world-text material. The builtin font material can draw through opaque walls;
  don't switch world signs back to it or mutate it globally (UI shares the font). Handle font-atlas rebuilds when changing fonts.
- Keep generated decoration deterministic: use `ShopSeed`, not process-dependent string hashing, and preserve shared RNG draw
  counts when removing random decoration so later trees/roofs/skyline do not unexpectedly change.
- Batch UI tests have no focused GameView. `SettingsUITests` temporarily routes queued keyboard events to dynamic player updates
  (`IgnoreFocus`, Editor `AllDeviceInputAlwaysGoesToGameView`) and restores those settings afterward. Assert the real input action
  fires; do not bypass it by directly calling pause/menu methods and call that a keyboard test.
- `ScreenCapture` / `WaitForEndOfFrame` do not produce dependable Editor batch UI captures. The UI test temporarily renders actual
  overlay canvases through the scene camera with a URP `SingleCameraRequest`, then restores all canvas/camera state. PhotoTool
  captures the environment only and logs GPU, quality, pipeline, triangle counts and missing-script checks.
