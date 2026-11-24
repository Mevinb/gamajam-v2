# PARASITE — After the Still Water

You are Specimen 7, an escaped amoeba that borrows human bodies to uncover Vale Biotech's experiments. Possess people,
use their credentials, split into two bodies, coordinate different professions and expose the black-site archive.

The initial **14-mission campaign** ends at a sunset lake with an explicit **Peace** or **Dominion** choice. From its summary,
choose **Continue · After the lake** to enter three more missions in a riverside district: stop poisoned runoff, coordinate
an evacuation, operate a guarded radio mast and escape by ferry. Your chosen future changes the later chapter.

The current **0.4.0 upgrade is being verified**. It includes Blender-authored cinematic environments, packed PBR textures,
revised shots, graphics fixes and the post-lake chapter. Follow [the upgrade tracker](Docs/UPGRADE_TRACKER.md) for the full
active scope and [the test report](Docs/TEST_REPORT.md) for completed artifact-specific checks.

## Play on Linux

```bash
./play.sh
```

The project launcher selects the NVIDIA GPU. The generated executable is `Builds/Linux/Parasite.x86_64`; builds and logs are
local artifacts, not stored in Git. Keep the executable, `UnityPlayer.so` and `Parasite_Data` together when sharing a build.

Choose **Begin Infiltration** to watch the opening. Press Space, Enter or Esc and release to skip. Reloading a checkpoint or
restarting does not replay it. The lakeside scene always requires a deliberate choice; skipping or waiting never chooses.
Graphics, mouse sensitivity and audio levels persist. Esc opens pause/settings and the mission journal.

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift / Space | Sprint / jump |
| E / hold E | Possess or interact / work at terminals |
| Q | Leave a host |
| V / Tab | Split / switch amoebas |
| F | Host profession ability |
| Esc / F1 / F3 | Pause / controls / FPS |
| Ctrl + M | Presentation shortcut to the original escape and explicit lake choice |

The shortcut works from the initial title, opening, pause, dialogue and gameplay; it preserves explicit choices and does not
replay the old finale in the later chapter. Automated queued-input coverage is separate from a physical keyboard/gamepad check.

Read [the developer handoff](Docs/HANDOFF.md), [game guide](Docs/COMPLETE_GAME_GUIDE.md),
[post-lake walkthrough](Docs/AFTERLAKE_GUIDE.md), and [known limits](Docs/KNOWN_ISSUES.md).

## Rendering and Blender

Linux remains the delivery target. The RTX 4050 renders the real-time URP game. Reflections use probes and screen-space
ray marching, alongside normal-mapped surfaces, ambient occlusion, shadows, wet ground, foliage and atmosphere.
The user selected **RTX-rendered Blender cinematic footage** for path tracing, while retaining Linux gameplay.
The Linux URP player does not use built-in hardware ray tracing.

Blender scripts are in `Tools/blender/`; editable film sets are in `ArtSource/Cinematics`, and Unity imports their FBX exports
and PBR textures from `Assets/Parasite/Resources/Cinematics`. External artwork and audio provenance is in
[third-party assets](Docs/THIRD_PARTY_ASSETS.md).

## Develop and verify

Use Unity **6000.3.29f1** and read the handoff first. Scenes are generated; change their builders, not their YAML.

```bash
Tools/compile_check.sh all
Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux
./play.sh -autotest -autotestOut /tmp/parasite-acceptance.txt
Tools/package_build.sh linux
```

The Unity wrapper serializes editor access and selects the RTX. Coordinate Blender GPU renders with graphics jobs.
Compile, regenerate, test and rebuild after builder changes. Package only after the report reflects the actual player.
