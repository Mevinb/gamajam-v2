# Full cinematic motion library

Reusable offline animation for the real PARASITE actor rigs. This folder does not change any Unity/runtime assets.

- `UAL_story_source.blend`: compact editable UAL skeleton and all 34 final authored manifest actions, 8.5 MB. No mannequin mesh, external textures, or dependency on the 263 MiB development blend is needed for playback.
- `Actors_story_actions.blend`: actual DrPetrov and MaintEli skeletons with nine retargeted actions each, 4.4 MB. Actions are named `Actor__Clip`, with 30 fps keys and a repeated endpoint for loops. Skeleton only; append the original actor mesh separately if desired.
- `manifest.json`: existing clip timing, loop, speed, and source metadata copied from `Tools/blender/animation/manifest.json`.
- `verification.json`, `standalone_verification.json`: numeric verification and SHA-256 evidence.
- `DrPetrov_pose_verification.png`, `MaintEli_pose_verification.png` (also SVG): inspected actual target skeletons in front and side views, with common metre scales. These are CPU FK diagrams, not rendered mannequin substitutes.

## Calling from a Blender shot script

```python
import sys
sys.dont_write_bytecode = True
from pathlib import Path
from mathutils import Vector
project = Path('/home/mevlec/Data/Parasite')
sys.path.insert(0, str(project / 'Tools/blender/cinematics/full_story'))
from motion import MotionLibrary

motion = MotionLibrary(project)  # instantiate once; use for all actors and frames
fk = motion.apply_pose(actor_armature, 'Console_Loop', seconds, root_motion=False)
hand_world = actor_armature.matrix_world @ fk['RightHand'].translation
# Layer head/jaw/hand rotations here if needed, then motion.fk(actor_armature).
```

`available_clip_names` is a tuple of exact manifest clip names. `clip_info(name)` returns a metadata copy including exact action duration and 30 fps. `pose_for(armature, name, seconds, root_motion=False)` returns `{bone_name: (Quaternion, Vector)}` without writing pose components. `apply_pose` writes these components and returns all target armature-space FK matrices. `fk(armature)` recomputes them from current components after additional caller layers. Neither function needs a depsgraph update or changes scene time.

The caller owns object translation, rotation, scale, parenting, and world travel. Animation remains in Blender armature space (Z up, -Y forward, +X actor left); the caller's Unity coordinate conversion `(x, -z, y)` stays outside this module.

## Retargeting and contact

The module samples source actions at their authored 30 fps, caches source FK, interpolates world-space quaternion deltas, applies those deltas to each target bone's actual rest orientation, and converts back through the target parent hierarchy. It retains target limb lengths and maps 53 bones, including 30 finger segments. Jaw and eyes retain their local rest transforms so the caller can layer performance keys.

Looped clips wrap at the exact action duration. Nonlooped clips clamp. `root_motion=False` removes the UAL `root` transform while retaining articulated pelvis motion, including sway, crouch, and collapse. `True` includes source root translation scaled by target leg length and root rotation. Existing Walk/Run are in-place: world locomotion is supplied by the shot.

The faithful source death pose penetrates a floor at actor origin on the actual meshes. At final Collapse/Unconscious pose, raise DrPetrov's object by **0.10533 m** or MaintEli's by **0.07706 m** for minimum mesh Z = 0. Blend this small placement lift during the fall as appropriate for the shot. These values are measured from CPU-deformed LOD0 geometry; the library deliberately leaves object placement with the caller. Complete sample mesh bounds are recorded in `verification.json`.

## Rebuild and verify

From the project root:

```bash
~/Data/Blender/current/blender -b --factory-startup --python Tools/blender/cinematics/full_story/motion/build_library.py
python3 Tools/blender/cinematics/full_story/motion/draw_poses.py
~/Data/Blender/current/blender -b --factory-startup --python Tools/blender/cinematics/full_story/motion/verify_standalone.py
```

The rebuild reads the complete authored `/home/mevlec/Data/parasite-work/animation/dev.blend`; the older `build.blend` contains only 14 early clips. Runtime module initialization defaults to the compact source in this folder; an explicit second source-path argument may override it. Rebuilds only write this folder and `Tools/blender/cinematics/full_story/motion/`. No GPU render, Unity process, runtime export, or commit is part of these commands.

Verified with Blender 4.5.16 LTS. All 34 clips were sampled on DrPetrov, DrVale, MaintEli, and LakeGirl in a fresh process. All 18 baked actions were replayed independently. Maximum FK matrix-element discrepancy was 0.00000215 against Blender pose matrices; representative complete-clip maximum limb-length discrepancy was 0.000000267 m. Actual LOD0 deformations were checked for finite vertices. Standing head heights exceed 1.4 m; collapse head heights are under 0.19 m. CPU sampling of 4,248 actor frames took 5.45 seconds on this machine (excluding key insertion/depsgraph evaluation).

## Provenance

Source motion/skeleton: Quaternius Universal Animation Library 1 + 2 [Standard], CC0 1.0. Walk derives from CMU Graphics Lab Motion Capture Database subject 07 trial 12, under its free-use terms (not for resale as mocap data). The data used in this project was obtained from mocap.cs.cmu.edu, created with funding from NSF EIA-0196217. Profession idles, possessed overlays, captured, console, panic, alert, point, gestures, and breathing were authored by the existing project scripts under the project licence.

The exact inherited attribution and links remain in `Assets/Parasite/Art/Characters/Animations/CREDITS.md` and per-clip source fields in `manifest.json`. The reusable retarget module, compact extraction, actor bakes, and verification diagrams are original project tooling/output. Actor rigs are the existing MakeHuman/MPFB sources in `parasite-work/characters/out/`; their existing project provenance is unchanged.

## Optional jaw amplitude from actual opening speech

`motion.speech.SpeechPerformance` adds a pure standard-library lookup for the eight actor lines in `full_story/plan.py` CAPTIONS. Its 25 KB cache is `opening_speech_envelopes.json`. The module reads no audio, starts no process, and imports no Blender API while sampling.

```python
from motion.speech import SpeechPerformance
speech = SpeechPerformance(project)
# who/text are the exact CAPTIONS speaker and line; time is seconds after its cue.
aperture = speech.amplitude(who, text, time_since_cue)  # always 0..1
actual_duration = speech.duration(who, text)
source_and_timing = speech.info(who, text)
```

Caption speaker strings and actor aliases `DrPetrov`, `DrVale`, `MaintEli`, and `MaintenanceWorker` are accepted. Unknown lines, negative time, nonfinite time, and time at or beyond actual clip duration return zero. The caller selects the active caption, stops/replaces it according to the film/audio cue policy, and applies `maximum_jaw_angle * aperture` around the actual jaw hinge. `speech.cues` provides the eight cue times, exact speakers/lines, durations, and keys. Use the measured duration rather than the earlier generic text-length speech estimate: Vale's shipped line is 2.43 seconds long.

The cache derives from the shipped `Assets/Parasite/Resources/Audio/Voice/vo_*.ogg` files. Every key matches the exact `VoiceOver.KeyFor` FNV-1a64 lookup and `Assets/Parasite/Audio/voice_manifest.json` entry. Each original file's hash, profile, voice ID, catalogue source, sample count, ffprobe stream data, and measured duration are recorded. Source `.ogg` files, catalogue, runtime code, and common/opening shot scripts were not modified by this task.

| Cue | Actor line | Actual decoded / ffprobe duration |
|---|---|---|
| 6.0 s | Four taps. Then we begin. | 1.60 s |
| 16.0 s | You remembered. | 1.05 s |
| 19.5 s | It anticipated a stimulus. | 2.43 s |
| 22.0 s | It knew I was here. | 1.34 s |
| 34.8 s | That wasn't me. | 0.94 s |
| 37.4 s | End contact. | 1.23 s |
| 44.0 s | Seven. Stay with the rhythm. | 1.60 s |
| 53.0 s | Who released the interlock? | 1.44 s |

Extraction decodes mono float PCM at 24 kHz on CPU, calculates a centred 25 ms RMS energy window, gates low-level noise, normalizes against the line's 95th percentile, and smooths at 100 Hz with 15 ms attack / 55 ms release. A 35 ms terminal closure keeps the jaw closed at the media endpoint. These are source-derived jaw energy envelopes; they contain no phonemes, visemes, lip shapes, or word-alignment claims. Narrator clips are excluded.

Rebuild with `python3 Tools/blender/cinematics/full_story/motion/build_speech.py`; draw the optional inspection plot with `python3 Tools/blender/cinematics/full_story/motion/draw_speech.py`. Both write only the allowed motion source folders. Evidence is in `speech_verification.json` and `speech_build.log`; the inspected plot is `opening_speech_envelopes.png`. All eight decoded sample-count durations exactly equal ffprobe durations and catalogue durations. All samples are finite, lookup output stays in 0..1, actor aliases resolve correctly, and output is zero outside the actual media duration. A standard-Python 15,360-call lookup check took 0.08755 seconds; no Blender, Unity, GPU, or rendering process was used for speech work.
