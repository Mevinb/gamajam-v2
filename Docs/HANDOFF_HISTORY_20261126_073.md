# Historical checkpoint before the0.7.3 delivery record

Superseded by Docs/HANDOFF.md. Process/live/pending statements describe their original observation, not current state.

## Current checkpoint — 26 November 2026

**LATEST USER PLAYTEST: 0.7.2-preview is open.** [Build/launch proof](verification/0.7.2-preview-build-and-launch-20261126.json).
WORK candidate `Builds/Candidates/full-story-20261126T073327996101Z-8c413e3e`; source commit `1cf8bfb`, snapshot `8a26a001…`.
Runtime `998430159a0a7899d39d8e75272a89b2d35c35cb0bff301265a5a6320d8c8aac`.
Build succeeded1135MB, zero errors/warnings, source and all older artifacts unchanged. Native basic01–05 passes5/5/native0/29.296s.
Desktop PID **289391**, parent289390, client `0x100000a`, RTX4050/Vulkan title menu v0.7.2 visually inspected.
Independent user service `parasite-playtest-072-8c413e3e.service`; WORK session
`Logs/user-playtest-full-story-20261126T073327996101Z-8c413e3e-desktop/session.json`.
The first tool-owned launcher was interrupted143; its native final code was unavailable. Its observation is retained separately.
**Preserve the current user session: no close/restart/input injection or competing GPU jobs. Recheck live state before work.**
The detailed coach is now built into this player. [Integration proof](verification/northline-coach-integration-preview-20261126.json):
expanded Editor9/11; both chapter/binding tests and seven material tests pass. Two strict tests retain the known Editor ring-buffer warning;
physical/glass assertions completed and six review images captured. Preview graphics/performance acceptance remains ongoing.
The broader upgrade goal remains active; main runtime remains separate at0.6.1. Older pending0.7.2 statements below are superseded.


The full upgrade/continued-expansion goal remains active. The earlier 0.7 playtest ended normally; the user's current request is a fresh build and launch of the latest work. A new 0.7.2 preview is being prepared, with no new build or launch result claimed yet.

- **USER 0.7 PLAYTEST ENDED:** PID155953/start3334957 is gone. Its recorded session exited normally0 after21711.645s at
  **2026-11-26T06:57:00.686080Z**; the agent did not close, restart or inject input into it. Candidate WORK
  `Builds/Candidates/full-story-20261126T005055601457Z-875bb782` remains retained, runtime408112a0899965a49a95c656e34b8161a942135ce8455793efafed49b8040ca5.
  Session WORK `Logs/user-playtest-full-story-20261126T005055601457Z-875bb782/session.json` is terminal. No current GPU reservation derives from that ended session.
- **PRIVATE0.7.1 BUILD TERMINAL/PASS:** root9396/native0, old owned Editor194358 gone;1119MB/121.229s pipeline/0errors/0warnings.
  [Build proof](verification/0.7.1-candidate-20261126.json). Candidate WORK `Builds/Candidates/full-story-20261126T014554516966Z-b287ee31`;
  runtime **66e4289a8c4ecaeb379334ba360f57512251c11275fcefa7aa34c1ecb2cd7b8f**, BUILD_INFO **bc9045f5574b8d43b600e20b1217a26f9d8142f851272ae2d19df14e3df02ff0**.
  Source before/after **de2acc082e446d2ef9420eea7912cdc35431439a2b6db3a762133a2ad4f6533f**, HEAD **70259deafd711f841c07225949b55733eaf2101a**,
  built from the then-clean pushed WORK branch `codex/northline-runtime`; all older BASE artifacts/source/070candidate unchanged. The later supported-renderer96 invocation below uses this exact private071binary.
  State WORK `Logs/full-story-candidate/build-runs/20261126T014554516966Z-b287ee31/runner-state.json`.
- **071 NATIVE95 TERMINAL/PASS:** [Continuous Dominion26 proof](verification/0.7.1-native-continuous-dominion26-20261126.json), root54898,
  native0/supervisor0,634.37s, exactPASS95/SUMMARY1/1, Null, all source/HEAD/071/070artifact integrity checks pass, no cancellation.
  WORK `Logs/native-071-focused/95-20261126a/execution.json`; old supervisor195558/player195561 are gone. Existing within-chapter
  role/position/Calm/timeScale/simulated-input fixtures remain explicit; four earned chapter boundaries reach the actualM26coach/body endpoint and summary.
  This closes that named071continuous-Dominion check, not continuousPeace94, default27, rendered appearance or human acceptance. Its source freeze ended with the run.
- **071 native normal river branches pass:** [17Peace and18Dominion](verification/0.7.1-native-riverside-green-20261126.json), two separate invocations,
  roots91824/20467, native0/super0,28.234/28.188s, exactselectedPASS/SUMMARY1/1 each,allsource/071artifact/070user integrity pass,no cancellation.
  Those existing chapter fixtures are explicit; these river passes do not independently prove continuous26/default27/rendered/human acceptance.
- **071 NATIVE96 SUPPORTED-RENDERER PASS; ORIGINAL NULL RED RETAINED:** [RTX4050/Vulkan proof](verification/0.7.1-body-free-native-vulkan-green-20261126.json),
  root53840, native0/supervisor0,12.059s, exactPASS96/SUMMARY1/1, zero HDR and ring-buffer warnings, strict unexpected-log guard clean,
  all immutable candidate and separately reviewed current-development source checks pass. The player remains the old071/de2accsource; later source is not claimed in that binary.
  [Original Null red](verification/0.7.1-body-free-native-red-20261126.json) remains root53749/native1/supervisor1/10.101s: functional calls finished before
  the strict guard rejected `Warning: HDR Render Texture not supported, disabling HDR on reflection probe.`. No warning suppression, probe/HDR quality cut or test weakening was applied.
  [Null warning characterization](verification/null-hdr-warning-characterization-20261126.json) is diagnostic scope. The earlier Editor Vulkan ring-warning gate remains distinct and open.
- **0.7 native branches now pass independently:** [92Peace/recovery](verification/0.7.0-native-northline-peace-20261126.json),root22591/native0/super0,193.311s;
  [93Dominion](verification/0.7.0-native-northline-dominion-20261126.json),root66651/native0/super0,106.747s. ExactonePASS/SUMMARY1/1/Null,
  all source/HEAD/full-candidate/user checks pass, no cancellation. They prepareM23/initialroles; they do not prove earned94/95, default27 or graphics/human acceptance.
  Existing [0.7 build/API](verification/0.7.0-candidate-20261126.json), [basic5/5](verification/0.7.0-native-basic-20261126.json),
  [visible launch](verification/0.7.0-user-playtest-20261126.json) remain tied to the unchanged0.7 binary/source95d9/commit6e12405.
- **New actual ferry defect fixed in source:** a destroyed inactive blob remainedIsFree; the shared ferry guard could publishM17 with no physical second body.
  Root77623 actualred1/2/native2 records deadIsFree/allFree/ferryAllowedTrue, StateEnded/DepartedTrue/MissionIndex17.
  Source `IsFree` now requires liveBlob, preserving hidden/disabled identities; sharedAllFree rejects the missing body without pruning/spawning/autoReset.
  The pause menu now has separate **Restart checkpoint** and **Restart game** actions; seven rows fit its panel. The recovery test invokes the actual checkpoint button,
  reloads the real scene/new registry, and uses fresh normalE to earn the ferry. Player-facing denial points to that action.
  [Exact regression history](verification/body-free-state-regression-20261126.json) preserves own-log/input-driver fixture failures and final focused2/2/native0.
  Both normalriverbranches and earlier lifetime controls also pass in named runs. No fresh single6/6 claim is made for the mixed earlier batch.
  [Sharedhelper/selection17/17](verification/body-free-shared-editor-green-20261126.json),root30574/native0,8.311s,
  exercises actual menu recovery plus exact96/default27 registry. Actual compile176runtime/33Editor/176LinuxMono passes.
  Commit674b8e6 contains fix/UI/sharedacceptance/exactopt-in96; the retained0.7binary does not contain it. Native96 now passes on supported RTX/Vulkan, with its separate original Null strict-log failure retained above.
- **DETAILED COACH INTEGRATED IN WORK SOURCE, NOT YET A NEW PLAYER:** The original authored package in commit70259de has a newer uncommitted
  revision in WORK `ArtSource/NorthlineCoach`, manifest0de23f46b09641b5cfe94ff2fdb52bdaf9adb9f544e3359784b1ab51e915c5e1.
  The11144triangle coach/15972triangle kit retains198closed components,10real glass apertures, original bounds/pivot and ten byte-identical other models.
  Runtime integration now includes two owned URP materials, three dedicated2048 A/N/M textures and **32 precise seat-part colliders** for eight assemblies.
  Seat pans top at1.25m; broad eight-box staging was replaced so the legitimate0.55m hop remains possible. New colliders have explicit zero centres/unit sizes.
  WORK `Logs/coach-import32-20261126a.json` records **67247checks/no failures**, actual imported structure/data only. Material tests pass7/7;
  `Logs/coach-fitting32-20261126c.xml` passes1/1 in14.4128031s with physical blocking, hop/landing, aisle traversal and original boarding/control checks.
  Actual compile176runtime/33Editor/176LinuxMono passes. An expanded RTX/Vulkan smoke run is root-owned/in progress; no result is claimed here.
  Binding, rendered appearance, chapter/lifecycle and performance checks retain their named scope. Neither retained0.7 nor private0.7.1 includes this new coach.
- **Temporary061 extraction retired after full review:** [retirement proof](verification/061-extraction-retirement-20261126.json),root87248/native0.
  Exact193files/17dirs matched retained fee38 archive/CRC/modes and originalmanifest;0unique/userfiles/links/specials/xattrs. Fresh content/identity/reference checks,
  recorded same4opaque systemd/PAM/SSH identities and otherUID visibility limits. Only generated package directory removed; **1173475328B actually reclaimed**.
  Archive/candidate/reports/native4/5 history/profiles/parentownership token remain. Reconstruction command/evidence BASE `Logs/extraction-retirement-061-stage`.
  Historical extraction is now reconstructible evidence, not a runnable present directory. Current0.7 source/build/session unaffected. Recheck space before writes.
- Main runtime source remains0.6.1/DC5; WORK feature/bug/source-art commits are pushed, code integration to main awaits further native/graphics acceptance.
  Preserve unrelated untracked `Docs/GRAPHICS_UPGRADE_PLAN.md` unstaged. Keep all retained BASE0.5.0/0.5.1/0.6.0/0.6.1builds/releases, the ended0.7candidate and private0.7.1candidate,
  failed cold-build evidence and2760completed RTX/OptiX film frames. No rerender needed. No0.7/0.7.1portable archive exists yet.

Next: finish the current coach smoke and fresh0.7.2build/visible launch requested by the user. EarnedPeace94 remains unrun; full/default/rendered/media/settings/performance gates remain.
Preserve original pinned071run receipts and their original pending-pins refusal. Never transfer0.7source95d9 or0.7.1/de2accresults to the new coach source/player.
The earlier0.7GPUreservation ended with that session; preserve any newly launched user playtest once it exists.
Linux/URP plus offline RTX Blender films and improved real-time shaders is the chosen direction; gameplay hardware RT is not implemented.
Disk campaign saving, physical input/listening, final realism quality and measured performance remain separate work. Historical checkpoint labels are in HANDOFF_HISTORY_20261126.md.
