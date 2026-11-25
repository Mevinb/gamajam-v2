# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the old eight-mission scope freeze and local-only capture session.
The full requested upgrade is **in progress**; see `Docs/UPGRADE_TRACKER.md`. Never present historical tests as verification of a new build.

## User playtest build retained

The user requested the current work be built and launched. `0.5.0-preview` was launched on native `DISPLAY=:0`
(PID 2531989 at launch; check the actual process). It is the live M18/full-lake working tree, not the retained 0.4.2 archive.
The build succeeded at 1,007 MB with zero errors/warnings; `Logs/user-playtest/build-info.json` and `source-snapshot.json`
pin this uncommitted preview. Player log: `Logs/user-playtest/player.log`. Full lake movies and the island entry mission
are included; M19/M20 remain in ignored staging. Do not overwrite Builds/Linux while this player is running.
The preview has since exited normally with code 0 (recorded in `Logs/user-playtest/session.json`).
After that normal exit and the reflection diagnostic, the frozen RTX opening render was resumed. Check its actual
process/checkpoint before GPU work; request a safe STOP and wait for terminal completion first.

## Current source beyond the preview

The live source now contains M18–M20 plus the four full lake films and eight island voice clips. Runtime/editor/Linux
API compilation passes 141/23/141 sources with the wall verification helpers. The retained preview binary still contains M18 only; do not relabel it.

- All 14 `IslandRescueTests` now have actual passing execution across focused runs: full Peace (`island-rescue-peace-fixed.xml`),
  full Dominion (`island-rescue-dominion-fixed.xml`), three berth/inset cases (`island-boarding-clearance.xml`), six recovery cases
  from `island-recovery-reflection-null-fixed.xml`, and the three corrected cover prerequisites (`island-first-cover-retry.xml`).
  Full branch supervisors exit 0 with unchanged source. Morgan parks away from the gangway and must reach a .35m berth sphere;
  physical deck, all-free, awake-passenger, pursuit, transit and checkpoint gates remain. The failed cover fixtures had stopped
  the leader while Riley was still 3.636m from cover; real through-leading reaches 1.431m and commits the unchanged runtime rule.
- `reflection-owner-camera-diagnostic.xml`: camera passes 17,347 post-Cinemachine samples with zero failed samples, including
  entry, pitch/yaw, hop, tide wash, target changes, exit and owner destruction. Free bodies use a centred rig; human shoulder
  framing and canonical markers remain. Collision shortening is immediate. This isolated camera fixture stuns unrelated
  guards; actual entry/stealth route tests remain separate. Earlier normal-approach clipping and framing failures are retained.
- `river-reflection-publication-final.xml`: 6 GPU passes / 1 Null-only skip, actual RTX 4050/OpenGL Core. High/Ultra enable
  native probes only on real graphics devices; Low/Medium use verified sky fallback. Null policy controls pass separately.
  The native capture probe destroyed its attached RT on retirement (proved by before/after gate logs); publication now copies
  the completed image into an independent Cubemap. Six origin signatures, compressed BC6H sampling, epoch cancellation,
  repeated retirement, no recapture High-to-Ultra, cleanup and fresh unmarked water all pass. Built-player/Vulkan remains pending.
- Eight offline CPU voice clips pass technical/key/level gates. Seven focused Editor cases pass actual source tails across all eight arrival/refuge/roster/departure clips, finite clearing,
  release-aware skip and disabled/missing fallback. Arrival uses a monotonic cue origin. Subjective listening and rebuilt-player
  checks remain pending.
- The Blender wall kit (two meshes, one material, 11,804 triangles, shared 2K PBR atlas) is now applied. Initial automatic
  texture import produced Cubemaps and blocked World initialization; the importer now explicitly requires Texture2D.
  `island-wall-import-verification.json` passes 140 actual Unity checks: three 2048 BC7 maps (~16 MiB), all 18 old maps 512,
  exact model counts/bounds and no colliders. Actual GPU wall appearance/aperture/route/performance checks remain pending.

The retained preview's strict movie checks decode all seven expected items (two old clips, four full lake films and the
neutral wrap) on RTX/Vulkan and OpenGL, but both processes fail normal shutdown and hit their watchdogs. These are **not**
end-to-end passes. Parent-GDB snapshots and exact matching Unity symbols locate a native `VideoMediaMemoryOutput` lock
inside video-class engine cleanup; the lock owner/cause is unproven. Evidence lives under `Logs/standalone-0.5.0-preview*`.
A two-file optional lifecycle trace / final-seek A/B patch is now applied from `Logs/cinematic-native-lifetime`; no new
player build or runtime repair is claimed. Preserve the old forced-seek stress path until the native issue is understood.

The frozen RTX production has resumed after the successful GPU window (old stop was frame 485). Current supervisor PID
3394810 / Blender PID 3395037, session 31015; check actual processes/checkpoints. Stop safely before another GPU job.
A reversible diagnostic build runner is being prepared under `Logs/cinematic-native-lifetime` to preserve the original preview.
User playtest metadata remains under `Logs/user-playtest/`. The older native exits remain unproven; do not label them repaired.

## Active cinematic production — 25 November 2026

The verified Linux milestone is 0.4.2, built from `8fd5ece` (DLL SHA-256
`8cf43b0f6e174369bf561a85ecd113815347f30deb16fc9f1fe181397477a0c1`). GPU presentation/audio, built-player
rendered-water and both river branches pass. The isolated extracted copy passes media/input checks; the archive's full
file set, hashes and local guide links are verified. `TEST_REPORT.md` records exact scope and retained older evidence.

A resumable RTX production is retained under `Logs/full-cinematic-renders/full-production-20261125T003711Z`: all four lake clips,
then the 80-second opening, 2,760 frames total at 1080p24 / 48 samples / GPU OIDN. The approved packed blends, render driver
and policy must stay unchanged. Check actual processes/checkpoints rather than assuming the job has finished.

Before another Unity graphics/player/benchmark job, request a safe stop by creating that production directory's `STOP` file,
then wait for the active frame and supervisor to finish. Headless logic work and small CPU-only tasks can continue. Remove
`STOP` and rerun `python3 Logs/full-cinematic-renders/production_plan.py --go --out <same-production-directory>` to resume;
provenance and every recorded frame hash are checked. A 4 GiB free-space guard also stops between complete frames.
Do not restart from scratch after an observation timeout. Completed real sequences must pass encoding/decoder gates before
publication. Full-story runtime support and the river backdrop cut are committed. Current evidence includes 105 EditMode
cases, both headless river flows, 15 passing presentation/audio cases from the combined GPU run, its corrected water case,
and the extra disabled-VO destruction regression. The strict built-player water test passes on Vulkan. Complete full-film
decode of the four real lake films now passes in Unity, including neutral looping and explicit branch gating; the opening
remains unfinished. The four-film bundle was atomically published to live Resources after full frame/codec/hash gates.
The island entry package is applied but uncommitted: 118 rules pass, all four route/outcome combinations pass headless and
GPU traversal, and tide/arrival-cue checks pass. Its camera had a transient viewport failure; a focused damping fix is prepared
for GPU rerun. Its visual fixture passes after synchronizing physics before snapshot. Reflection-probe readiness remains
under diagnosis. M19/M20 are separate staged followup and still require integration/real motion and recovery tests.
The old opaque skyline ground issue is corrected in 0.4.2; source meshes/colliders/navigation are preserved.

## Environment and commands

- Project: `~/Data/Parasite`; remote `https://github.com/Mevinb/gamajam-v2`, branch `main`.
- Unity 6000.3.29f1: `~/Data/Unity/Editor/6000.3.29f1/Editor/Unity`; URP 17.3, Cinemachine 3, Input System.
- Blender: `~/Data/Blender/current/blender` (not on PATH). Existing pipelines live in `Tools/blender/`.
- Linux x86-64 remains the target. The user selected RTX-rendered Blender cinematics plus improved real-time shaders.
- GPU: RTX 4050 Laptop GPU, 6 GB; RAM 16 GB. Check `df -h .` before large imports/renders. There were 26 GB free on 25 Nov.
- Launch the built Linux player using `./play.sh`; it selects NVIDIA offload and prefers Vulkan on NVIDIA systems.
  Use `PARASITE_GRAPHICS_API=glcore ./play.sh` for OpenGL; explicit Unity API flags take priority.

```bash
Tools/compile_check.sh all
Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml
grep -E '\[AutoTest\] (PASS|FAIL)' Logs/playmode.log
Tools/unity.sh photos -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux
./play.sh -autotest -autotestOut /tmp/parasite-acceptance.txt
./play.sh -benchmark -benchmarkPreset High -benchmarkDistrict city -autotestOut /tmp/parasite-benchmark.txt
./play.sh -benchmark -benchmarkPreset Ultra -benchmarkDistrict riverside -autotestOut /tmp/parasite-riverside-benchmark.txt
Tools/package_build.sh linux
```

`Tools/unity.sh <log-name> <args>` serializes Unity with flock and writes `Logs/<log-name>.log`. Never open the editor GUI or
start another editor process on this project during a batch job. Poll the returned process/session; an old lock file is not
proof a job is alive. Coordinate Blender GPU renders with Unity graphics jobs, especially benchmarks on this 6 GB card.
Explicit `DISPLAY`/`XAUTHORITY`, including an Xvfb display, are preserved by `Tools/display.sh`.

`Tools/compile_check.sh all` now includes a Linux-Mono player API check, using only references/defines from the last valid
Bee player cache and enumerating current source anew. `runtime`, `editor` and `player` modes are explicit; unknown modes fail.
A successful Linux build seeds this cache. Unity-version, package lock or assembly-definition changes require a fresh build;
the player check fails clearly rather than silently using Editor assemblies. It does not replace shader/build/player tests.

After edits to editor builders, `MissionLayout`, or `CityLayout`: **compile → regenerate scenes → PlayMode → Linux rebuild**.
The generated Bootstrap and City scenes must never be hand-edited. Test source changes with the relevant focused suite,
then run campaign regressions when shared behavior or mission progression changes. Package only after the build and its
verification report agree. `Logs/`, `Library/`, and `Builds/` are ignored; they must not be committed.

## Current game architecture

All runtime source is under `Assets/Parasite/Scripts`, assembly `Parasite.Runtime`; editor tooling is in `Assets/Parasite/Editor`.
Use one MonoBehaviour per file, with the file matching the class name. Unity scene serialization depends on this.

- **Boot:** City scene's `World/CityDirector` creates GameManager, SuspicionSystem, MissionManager, PossessionManager,
  AbilitySystem, bakes the collider NavMesh under `World`, spawns `NpcSpawn` markers through `HostFactory`, then creates camera,
  traffic, HUD, ambience and weather. `GameManager.Awake` builds the riverside annex before the bake when the later chapter is active.
- **State:** `Core/GameManager` owns Playing/Paused/Ending/Ended/Resetting. Static `RunState` survives checkpoint scene reloads,
  including mission flags, index, statistics and immutable lakeside outcome; New Game clears it. There is no disk campaign-save claim.
- **Input/possession:** `GameInput` defines Input System actions and acceptance-only simulated controls. `PossessionManager`
  supports two amoebas, aimed E possession, Q release, V split and Tab switching. `PlayerCamera` uses Cinemachine third-person follow.
- **Humans:** `HostCharacter` switches between NavMeshAgent and CharacterController; do not enable both CC and HitBox.
  `HumanoidRig` loads Blender/MakeHuman skinned variants and humanoid animation. `HostBrain` and `GuardAI` handle routines/pursuit.
- **Stealth:** `SuspicionSystem` tracks witnessed violations, cameras report directly, hunters track burned bodies, and guards capture hosts.
- **Missions:** `MissionManager.BuildCampaign` supplies 14 initial missions. Flags drive progression/objectives/waypoints; completed
  evidence and sabotage flags restore world state. `MissionProp`, `CampaignOperation`, `CooperativeConsole`, `DualAuthReader`,
  `AccessDoor`, and `StaffTerminal` implement role and prerequisite gates. A cooperative console remains held by that same host;
  its paired operation needs a different possessed host, and cancellation does not preserve transient work.
- **Later chapter:** `AfterlakeCampaign` appends three missions only after the original summary's deliberate Continue action.
  `AfterlakeDistrict` builds a bounded riverside annex and NPC markers before NavMesh baking. Peace/Dominion changes the story,
  records objective, and radio role choices. Completion requires the free amoeba at the ferry. The first lakeside outcome persists.
- **Presentation:** Deliberate New Game requests the 80-second `OpeningCinematic`; reload/restart/direct City loads do not replay it.
  `LakesideEpilogue` requires explicit Peace/Dominion before publishing the original summary. Waiting or skipping never chooses.
  Both use unscaled time while live gameplay is frozen. Blender set assets and PBR surfaces are loaded through `CinematicArt`.
- **UI:** code-built uGUI in `HUDController` partial files; `MainMenuController`, `SettingsOverlay` and `UIKit` share input/navigation.
  Escape closes a pause submenu before resuming. Journal entries are built from the current campaign rather than hard-coded counts.
- **Graphics:** `GraphicsPreset` applies saved Low/Medium/High/Ultra settings to pipeline and newly created cameras. GroundWater's
  reflections are screen-space depth-buffer ray marches; they are not hardware ray tracing. `LampLights` budgets local lights.
  Custom atmosphere/foliage/facade/amoeba shaders and packed PBR textures provide the real-time look.
- **Audio/FX:** `AudioPreferences`, voice/narration/ambience/music systems; `FxApi`, `SfxBackend`, `AmoebaVisual`, `HostGlow`;
  weather and slime trail. Temporary cinematic gain must not overwrite saved preferences.
- **Builders:** `CityBuilder` + `Editor/World/*`, `MissionPlacement`, `ProjectSetup`, `FxSetup`; `PhotoTool` captures environments,
  while PlayMode UI capture renders actual overlay canvases through the camera. `BuildAutomation` creates the Linux player.

## Campaign

| Mission | Core objective |
|---|---|
| 1 OUT OF THE DRAIN | Leave the alley and borrow the first host. |
| 2 WORD ON THE STREET | Read the plaza newsstand and eavesdrop at the café. |
| 3 THE TAIL | Follow Petrov without crowding or losing her, then recover her dead drop. |
| 4 CREDENTIALS | Guard/main gate, scientist/keycard/staff terminal, or maintenance/power/tunnel. |
| 5 VALE'S RESEARCH | Copy two terminals and use two separate hosts at the office readers. |
| 6 CREATOR | Confront Vale; protected dialogue returns control afterward. |
| 7 HUNTER PROTOCOL | Evade bio-scans, bypass the arch as free amoeba, trip the duty-officer breaker. |
| 8 THE PART HE HID | Maintenance holds coolant while another scientist copies the signed archive. |
| 9 BREAK THE LEASH | Guard holds dispatch while another maintenance host overloads the relay. |
| 10 THE SHADOW UPLINK | Broadcast preliminary evidence without pursuit. |
| 11 THE BLACK SITE ARCHIVE | Trigger a diversion, then download the black-site archive as a scientist. |
| 12 CONTAINMENT PROTOCOL EPSILON | Overload backup power and override the forecourt security grid. |
| 13 BLACKOUT | Cut three transformers in 35 seconds using three distinct human palm prints. |
| 14 THE FINAL CONVERGENCE | Broadcast the unredacted archive and extract as free amoeba through the original drain. |
| Lake | Watch the arrival, explicitly choose Peace or Dominion, then reach the original summary. |
| 15 AFTER THE STILL WATER | Answer Petrov's distress signal and isolate a poisoned floodgate. |
| 16 BORROWED HEARTS / A WILLING NETWORK | Analyse runoff, coordinate guard/scientist hosts at the shelter records. |
| 17 THE OPEN CHANNEL / THE FIRST MANY | Hold mast power with maintenance, transmit with another host, leave by ferry as free amoeba. |

The presentation shortcut remains Ctrl+M (either Ctrl, either press order) through initial title/opening/pause/dialogue/gameplay.
It does not choose a lakeside outcome. It must not restart the old finale during the riverside chapter. Existing queued-input
checks do not establish a native physical keyboard or physical gamepad pass. Do not guess a replacement shortcut.

## Blender/art pipeline

- `Tools/blender/characters/`: MakeHuman/MPFB models, authored garments and textures; installed under `Art/Characters`.
- `Tools/blender/animation/`: retargeted/authored clips and previews.
- `Tools/blender/environment/`: tree meshes, bark and leaf atlases, HDR environment assets.
- `Tools/blender/cinematics/build_sets.sh`: deterministic original laboratory, conduit, bench and shoreline sets.
  Editable sources: `ArtSource/Cinematics`; runtime exports/textures: `Resources/Cinematics`; importer: `CinematicArtImporter`.
- `Tools/blender/cinematics/build_films.sh`: two RTX/OptiX establishing inserts, three seconds each; packed film scenes and
  derived device/codec/SHA evidence in `ArtSource/Cinematics`. Verify built-player decode with `-cinematicTestOut <absolute-path>`.
- `Tools/blender/riverside/build_props.sh`: eight original ferry/clinic/radio/floodgate/bank assemblies; editable source and
  CPU export verification in `ArtSource/Riverside`, game assets in `Resources/Riverside`.
- Asset generation must finish before a frozen Unity test run. Publish verified FBX/PNG/video files atomically from staging.
  Blender-to-Unity export needs the verified handedness conversion; size-only bounds checks miss front/back reflection.
- Consult `Docs/THIRD_PARTY_ASSETS.md` for external licences; keep source provenance alongside new assets.

## Verification and remaining work

`Docs/TEST_REPORT.md` owns artifact-specific evidence. `Docs/KNOWN_ISSUES.md` lists limits. `Docs/UPGRADE_TRACKER.md` retains the
full active request and the evidence needed for completion. An implementation or an old passing XML is not proof the new
feature works in the final player. Report unverified player video decode, physical controls, long playthroughs and performance honestly.

## Gotchas

- Unity 6 licensing must be active. Use Unity Hub sign-in only if logs show a licensing problem.
- Story freezes simulation at timeScale=0; use realtime waits/unscaled camera/audio/skip clocks, never speed acceptance by unfreezing AI.
- Commit the original Escaped flag only after an explicit resolved lake outcome. Keep continuation separate from replaying that choice.
- `Physics.IgnoreCollision` resets when colliders disable. Preserve HostCharacter's CC/HitBox switching.
- Unity object fake-null makes `?.`/`??` unsafe for destroyed objects; prefer `if (obj)` checks.
- Disabling a runtime-mutated quality feature must restore its lower-quality setting as well as enabling its higher-quality setting.
- The city atmosphere's height fog must not be applied to isolated cinematic stages far below the city.
- Keep ContributeGI off generated geometry unless deliberately introducing baked GI. Do not skip environment/reflection generation.
- World text needs `WorldTextFont` and the depth-tested shader, including atlas-rebuild refresh; do not mutate the shared UI font material.
- Use deterministic `ShopSeed`/local RNG for generated decoration; preserve unrelated shared RNG draw counts.
- Tests have no focused GameView: route queued keyboard events through DynamicUpdate/IgnoreFocus and restore settings after tests.
- Editor batch ScreenCapture/WaitForEndOfFrame is unreliable for overlay UI. Use the existing URP SingleCameraRequest capture helper.
- Cold shader builds may take many minutes. Poll the live process; an observation timeout is not a failed job.
- `pkill -f` can kill its own shell. Identify the exact PID and only terminate the intended task.
- Git identity is Mevinb only. Commit/push working features often, without Devin or generated-by trailers.
