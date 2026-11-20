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

Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch   # regenerate Bootstrap+City scenes (5–15 min)
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml   # acceptance suite (~5 min)
grep -E '\[AutoTest\] (PASS|FAIL)' Logs/playmode.log
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml   # 18 rule tests
Tools/unity.sh photos -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch   # 15 preset renders → Logs/photos/*.png (add -photoFilter 07)
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux       # Builds/Linux (first build after new shaders ≈ 30 min: shader variants)
Tools/unity.sh build-win -executeMethod Parasite.EditorTools.BuildAutomation.BuildWindows       # Builds/Windows/Parasite.exe
./play.sh -showfps                  # play; F3 FPS, F1 controls, Esc pause
Builds/Linux/Parasite.x86_64 -autotest -autotestOut /tmp/report.txt    # run the acceptance scenarios inside the real build
Builds/Linux/Parasite.x86_64 -benchmark -autotestOut /tmp/bench.txt    # walk the city, log FPS/memory, screenshots
```

**Order after changing anything in `Assets/Parasite/Editor/**` or `MissionLayout/CityLayout`:** compile_check → buildscenes → playmode → build-linux.
Scenes are *generated*; never hand-edit `City.unity`/`Bootstrap.unity` (they're rebuilt from code).

Interactive testing: the `screen-control` MCP works against the running game window ("PARASITE"): `screenshot` with
`crop_to_window: "PARASITE"`, `hold` key_down/release_all for WASD, `batch` move for mouse look, `press_key` for E/Q/V/Tab/F.

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
  `Resources/Materials/*Template` so shader variants survive stripping), `GraphicsPreset.cs`, `AutoTest.cs` (acceptance scenarios).
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
  `MainMenuController.cs` (Bootstrap scene).
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

## 6. Current state (end of session, 21 Nov 2026)

- Acceptance suite last full run: **12/12 PASS** (before CCTV/abilities/fabric humans/traffic/journal were added). Scenario 13
  (CCTV + abilities) is written but **not yet run** — run playmode first thing and fix anything red.
- Traffic, graphics presets, journal, civilian flee: compiled, **not yet run in Unity**.
- **A visual-overhaul subagent was still running** when the session stopped. Its uncommitted changes live in the working tree
  (`Editor/CityBuilder.cs`, `Editor/World/*` incl. `Vehicles.cs`, `SignAtlas.g.cs`, `Art/**` metas, `Materials/**`,
  `Settings/CityVolume.asset`, `Assets/Settings/PC_*`, `Tools/fetch_acg.sh`). Review with `git status`/`git diff`, run compile_check +
  buildscenes + photos, then commit what works. It added ambientCG/Poly Haven assets — make sure `Docs/THIRD_PARTY_ASSETS.md` lists them.
- Last scene build: 1330 renderers, **5.8 M triangles (5.6 M from props)** — too heavy. Decimate/LOD or drop the heaviest Poly Haven
  props; measure with `-benchmark` on the RTX (target 60 FPS @1080p; earlier lighter city ran ~150–180 FPS).

## 7. Known issues / next steps (priority order)

1. Run the PlayMode suite; fix failures (scenario 13 new; traffic may block test walks across roads → if so disable traffic when
   `AutoTest.Running`).
2. Visual pass follow-ups (user wants "GTA-like"): mirrored shop-sign text on some facades (TextMesh faces −Z — check yaw), bus shelter
   blocking the start-alley exit (~(-33,-42)), floating/shiny storm-drain grate, triangle budget, LODs, decals/puddles, better trees.
3. Humans are still procedural primitives. Real rigged CC0 humans + Humanoid animations would be the biggest visual win
   (Mixamo/Asset Store need the user's login — ask them to download; then swap `HumanoidRig` for an Animator-driven model).
4. Balance: blob sighting rate (`SuspicionRules.BaseAmount(BlobSighted)` = 6/s), guard speed vs blob, camera ranges.
5. Windows build for judges (`BuildWindows`), README with screenshots, `Docs/KNOWN_ISSUES.md`, `Docs/TEST_REPORT.md` with real
   benchmark numbers (never invent numbers).
6. Nice-to-have: settings persistence for audio volume, minimap, more side missions, NPC conversation pairs, rain/wet streets.

## 8. Gotchas learned

- Unity 6 batch needs the licence: if runs fail with licensing errors, open Unity Hub and sign in.
- One MonoBehaviour per file (else "missing script" in generated scenes).
- `Physics.IgnoreCollision` resets when a collider is disabled — that's why HostCharacter toggles CC vs HitBox instead.
- Unity `?.`/`??` on UnityEngine.Object is unsafe (fake null) — use `if (!obj)`.
- Python urllib gets 403 from Poly Haven/ambientCG — use `curl -A "Mozilla/5.0"`.
- `pkill -f <pattern>` inside the same shell command line kills that shell — use `pgrep` + `kill <pid>` or a different pattern.
- First build after shader/material changes compiles ~1,600 shader variants (CPU, ~30 min); later builds use the cache.
