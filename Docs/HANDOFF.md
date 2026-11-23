# PARASITE 0.2.0 — Developer handoff (read this first)

**Current local override — v0.2.2:** The user explicitly prohibited commits and pushes for the Ctrl+M fixes and reiterated
“NO PUSHING OF CODE TO GIT.” Keep this work local; this overrides the historical Git/delivery instructions below.
The local player is now v0.2.2. `PresentationShortcut` starts before the first scene and persists across loads, handling
Ctrl+M from title/overlays, the opening, pause, Vale dialogue and gameplay. Requests wait through reset/loading/body transitions.
An existing ending is preserved and Peace/Dominion is never selected automatically. Restart an already-running player to use it.
Focused PlayMode checks passed 8/8, including an 11-phase startup/pause/dialogue/reset matrix. The Linux rebuild succeeded
(425 MB, 23.54 seconds, zero errors/warnings). See the appended v0.2.2 section of `Docs/TEST_REPORT.md` for built-player evidence.
Everything below describes the committed v0.2.0 baseline unless explicitly marked otherwise.

Unity 6.3 LTS (6000.3.29f1) URP third-person stealth game. You are an amoeba (Specimen 7) that escaped Vale Biotech,
possesses humans, splits in two, and works through an 8-mission campaign: infiltrate, meet Dr. Vale, secure signed evidence,
sabotage containment, broadcast the truth and escape. An 80-second skippable new-game backstory opens the campaign;
the final sunset lake and seated-girl scene requires an explicit Peace or Dominion choice before the summary.
Scope is frozen except for the explicitly requested Ctrl+M judges presentation shortcut: deliver Linux, push and stop.
The final Linux rebuild succeeded (425 MB, zero errors/warnings); standalone campaign acceptance passed 13/13.
The final opening/shortcut focused checks passed 3/3. The illustrated Markdown guide and latest README are complete.
`Docs/TEST_REPORT.md` owns artifact-specific evidence; deliver `Builds/Releases/PARASITE-Linux.tar.gz` with its checksum.
Repo: https://github.com/Mevinb/gamajam-v2 (remote `origin`, branch `main`). Old repo `gamejam-old` = github.com/Mevinb/gamejam.

## 1. Environment (this laptop: Ubuntu 26.04, RTX 4050 6 GB, 16 GB RAM)

| Thing | Where |
|---|---|
| Project | `~/Data/Parasite` |
| Unity editor | `~/Data/Unity/Editor/6000.3.29f1/Editor/Unity` (Linux Mono build support installed) |
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
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux       # Builds/Linux (last cached build 6.56 s; cold shaders can be much slower)
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
  markers via `Characters/HostFactory`; creates `PlayerCamera`, `TrafficSystem`, HUD, ambience, then starts `OpeningCinematic` only when requested by deliberate title-menu entry.
- **Core:** `GameTypes.cs` (enums, ProfessionCatalog, SuspicionRules/SplitRules/AccessRules pure logic, IControllable, IInteractable,
  GameEvents hub), `GameInput.cs` (code-defined Input System actions + `Sim*` flags for tests), `GameManager.cs` (pause, checkpoints,
  `RunState` static: flags, mission index, stats — survives checkpoint reloads), `MatFactory.cs` (runtime materials cloned from
  `Resources/Materials/*Template` so shader variants survive stripping), `GraphicsPreset.cs`, `AudioPreferences.cs`,
  `AutoTest.cs` (acceptance scenarios and benchmark tour). `OpeningCinematic` owns the 80-second unscaled story timeline,
  keyboard/gamepad skip and restoration of gameplay camera/HUD/input. Checkpoint/restart/direct scene loads do not request it.
- **Possession:** `PossessionManager` (max 2 `AmoebaAgent`s, targeting/aim cone 15° within 5 m, E/Q/V/Tab, launch arc, capture → eject,
  containment → checkpoint), `AmoebaController` (blob), `PlayerCamera` (Cinemachine 3 ThirdPersonFollow on a pivot), `AbilitySystem` (F).
- **Characters:** `HostCharacter` (NavMeshAgent when AI, CharacterController when possessed; exactly one of CC/HitBox enabled),
  `HostBrain` (wander/patrol/stationary, notice, flee), `GuardAI` (Patrol→Investigate→Pursue→Capture), `HumanoidRig` (procedural
  body, fabric textures from `Resources/Textures/Clothes`, procedural animation).
- **Stealth:** `SuspicionSystem` (0–100; only witnessed violations count; decay; guard alerts; `ReportDirect` for cameras),
  `SecurityCamera` (sweeping CCTV; maintenance F disables).
- **Missions:** `MissionManager` (8 missions defined in `BuildCampaign()`; objectives keyed by flags; waypoints; banner),
  `MissionFlags` (string constants), `MissionProp` (generic E / hold-E interactable that sets a flag, with host/permission/prereq gates),
  `EavesdropZone`, `DualAuthReader` (two-person rule → needs split), `AccessDoor` (permission + `requiredFlag` + `openedFlag`),
  `StaffTerminal` (needs keycard flag), `DrValeInteraction` (midpoint reveal, then resumes play via `GameManager.ResumeAfterDialogue`).
  `CooperativeConsole` arms a role-specific support control while that same possessed host stays in range; a different amoeba must
  operate the paired `CampaignOperation`. Work cancels if either host leaves/captures or an uplink pursuit begins.
  `CampaignExtraction` requires broadcast evidence and a free amoeba at the original storm drain, followed by the lakeside epilogue.
  `EndingOutcome` is `Unchosen`/`Peace`/`Dominion`; `GameManager.PublishFinalEnding` publishes statistics only after the explicit choice.
  `ReannounceCurrentMission()` refreshes the objective/banner after the new-game introduction without advancing progress.
  Completed chapter flags restore opened doors/CCTV sabotage across checkpoints; transient console operators do not persist.
- **World data:** `CityLayout.cs` (key coordinates — CityBuilder builds from these), `MissionLayout.cs` (mission prop + camera positions),
  marker components (one MonoBehaviour per file — **Unity requires file name == class name for scene serialization**).
- **UI:** `HUDController*.cs` (built in code, uGUI legacy Text, Rajdhani font): suspicion, objective (rich text), body/slots, prompts,
  hints incl. [F], toasts, subtitles, world markers, waypoints + compass (`.Mission.cs`), pause (+graphics preset, journal), end screen.
  `MainMenuController.cs` (Bootstrap scene), `SettingsOverlay.cs` (shared title/pause graphics, audio and sensitivity controls).
  `UIKit.EnsureEventSystem()` uses the Input System UI module; Escape closes a pause submenu before resuming gameplay.
  `LakesideEpilogue` + `LakesideSet` stage the seated girl at a sunset lake. The first explicit Peace/Dominion choice is immutable;
  held skip input must be released before choices activate. Waiting or skipping does not choose. The summary follows resolution.
- **World text:** `World/WorldTextFont` binds the dynamic font atlas to `Shaders/WorldText.shader` (depth tested, back-face culled,
  alpha + vertex colour). It refreshes after `Font.textureRebuilt`; runtime material clones leave UI font materials untouched.
- **FX:** `FxApi.cs` public API (Sfx/Fx), `FxBackend/SfxBackend/SfxSynth/FxAssets`, `AmoebaVisual` (+ `HostGlow`), shaders
  `Shaders/Amoeba.shader`, `AmoebaNucleus.shader`; materials generated by `Editor/FxSetup.cs`.
- **Editor (asmdef `Parasite.Editor`):** `CityBuilder.cs` + `World/*` (procedural city/lab, materials, props, vehicles, signs),
  `MissionPlacement.cs` (mission props, cameras, extra NPCs, door flag wiring — called at end of `CityBuilder.Gameplay()`),
  `ProjectSetup.cs`, `FxSetup.cs`, `PhotoTool.cs`, `BuildAutomation.cs`.

## 5. Fixed release scope and campaign

Version **0.2.0** contains the existing eight missions, opening backstory, lakeside choices and Ctrl+M presentation shortcut. The user froze scope:
finish validation, package Linux, push and stop. The user subsequently requested Ctrl+M for judges; that addition is implemented.
No additional missions, art systems or feature backlog is authorized for this delivery.

| Mission | Existing behavior |
|---|---|
| 1 — OUT OF THE DRAIN | Leave the alley and take the first host. |
| 2 — WORD ON THE STREET | Read the Plaza newsstand and overhear Dr. Petrov at the café. |
| 3 — CREDENTIALS | Guard badge/main gate, scientist plus café keycard/staff terminal, or maintenance plus power/service tunnel. |
| 4 — VALE'S RESEARCH | Copy both lab terminals; two separate staff hosts badge the office readers within 8 seconds. |
| 5 — CREATOR | Vale reveals the experiment; protected dialogue ends by restoring gameplay. |
| 6 — THE PART HE HID | Maintenance holds service-room coolant while a different scientist host copies the signed archive for 8 seconds. |
| 7 — BREAK THE LEASH | Guard holds lobby dispatch while a different maintenance host overloads the east-alley relay for 7 seconds; CCTV turns off permanently and exits open. |
| 8 — OUTSIDE THE EXPERIMENT | Any host broadcasts at the Plaza uplink for 10 seconds without pursuit, then a free amoeba escapes through the original drain. |

New-game entry alone requests the **80-second opening**. Space/Enter/Escape or gamepad South/East skips it after release;
checkpoint reloads and Restart do not replay it. At the end, the **sunset lake/seated girl** presents **Live quietly (Peace)** and
**Become the world (Dominion)**. No timer or skip input selects a future. Only the chosen resolution publishes the final summary.

During gameplay, either Ctrl key plus M invokes `MissionManager.TryPresentationEscape`: completes prerequisite flags,
releases the active host, stages the free amoeba at the original drain and starts the existing protected escape dialogue.
Opening/Vale dialogue, pause, in-transit possession and an already running/completed ending reject the shortcut.
It never sets `Escaped` or chooses Peace/Dominion. Skip the opening with Space first for a fast presentation.

Story presentation freezes simulation at `timeScale = 0`. Characters, suspicion, CCTV and capture timers reject non-playing story
states. Dialogue uses realtime waits and unscaled time; input resumes only after Vale's reveal or the opening cleanup.
The `Escaped` flag commits in `PublishFinalEnding`, after an explicit outcome. An unfinished finale can therefore resume after a reload.
Completed evidence/sabotage/broadcast flags restore world state; transient paired console operators must be rearmed.

## 6. Verification status (21 Nov 2026)

- **CPU source compile: PASS**, 66 runtime + 11 editor files.
- **Eight-mission gameplay: 13/13 scenarios PASS** in final Editor acceptance, all three routes and both futures.
- **Combined PlayMode:** initial 5/6; only failure was the 20× test clock overshooting its first shot. Slowed the test to 2×;
  affected opening rerun passed 2/2. Do not represent the initial combined XML as entirely passing.
- **Final opening/shortcut focus: 3/3 PASS**, both opening cases plus queued left/right Ctrl+M and M-alone isolation.
  All 11 opening captures regenerated; corrected interlock-release camera reviewed. No automatic or duplicate ending choice.
- **Settings/journal/lakeside UI: 3/3 PASS**, including immutable explicit choices, both resolutions and temporary-scene cleanup.
- **Generated city/photo audit:** 482,781 static triangles, 277,497 prop triangles, 1,391 merged meshes;
  PhotoTool including gameplay meshes: 490,641 triangles, zero missing scripts, 15 RTX renders.
- **Linux build source `3f71476`: PASS**, 425 MB, 6.56 seconds cached, zero errors/warnings (`Logs/presentation-linux.log`).
- **Real standalone campaign acceptance: 13/13 PASS** on the final executable (`Logs/presentation-standalone.txt`,
  `Logs/presentation-standalone-player.log`), including all three eight-mission routes and Peace/Dominion outcomes.
  The player completed the suite and shut down cleanly. The final run used player batch mode on the RTX.
- The earlier 22/22 EditMode rules/audio checks remain valid for unchanged rule/audio sources. Older five-mission benchmark
  measurements are historical; no fresh 0.2.0 performance benchmark is claimed.

`Docs/TEST_REPORT.md` owns final artifact-specific evidence. `Docs/COMPLETE_GAME_GUIDE.md` is the illustrated player, judges
and developer handbook. The release archive includes this guide and its eight screenshots alongside controls, testing notes
and licences. Delivery is complete after archive integrity/checksum and the final documentation push; no further features.

## 7. Known limitations

See `Docs/KNOWN_ISSUES.md` for the maintained artifact-specific list. Humans remain procedural rigs and trees use cards; simulated
acceptance is not an extended human playthrough or physical gamepad verification. Imported prop detail is selected during scene
generation rather than switching at runtime with camera distance. Earlier benchmarks include occasional frame-time hitches and a
timed-out return leg; they do not guarantee constant performance across every view or establish fresh 0.2.0 performance.
Linux x86-64 is the sole delivery target. Windows was cancelled by the user and is not a pending deliverable.

These are limitations of the fixed release, not directions to add features before delivery.

## 8. Gotchas learned

- Unity 6 batch needs the licence: if runs fail with licensing errors, open Unity Hub and sign in.
- One MonoBehaviour per file (else "missing script" in generated scenes).
- Story sequences freeze simulation with `timeScale = 0`; dialogue waits and skip timing must use realtime/unscaled time.
  Resume gameplay only after Vale's reveal; commit `Escaped` only when the chosen lakeside ending resolves. Acceptance must not speed
  story sequences by overriding `timeScale`, because that bypasses capture/AI safety.
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
