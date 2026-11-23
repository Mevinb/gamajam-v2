# Humanoid animation credits

All clips in this folder are rebuilt on the Quaternius UAL skeleton by `Tools/blender/animation/build_animations.sh`
(Blender 4.5, headless). Sources:

| Source | Author | URL | Licence | Used for |
|---|---|---|---|---|
| Universal Animation Library [Standard] | Quaternius (with Gonzalo Furnier) | https://quaternius.itch.io/universal-animation-library | CC0 1.0 | Idle_Neutral, Run, Jump_Takeoff, Fall_Loop, Land, Collapse, Sit_Enter/Loop/Exit, Talk_Loop; skeleton, base poses and preview mannequin (Preview/UAL_Mannequin.fbx) |
| Universal Animation Library 2 [Standard] | Quaternius | https://quaternius.itch.io/universal-animation-library-2 | CC0 1.0 | Idle_FoldArms, GetUp (LayToIdle); female preview mannequin (Preview/UAL_Mannequin_F.fbx) |
| CMU Graphics Lab Motion Capture Database, subject 07 trial 12 ("brisk walk") | Carnegie Mellon University | http://mocap.cs.cmu.edu/ | free to use, including commercial products (not to be resold as mocap data) | Walk (retargeted, looped, in place, foot-locked) and Walk_Possessed |

"The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from
NSF EIA-0196217."

All other clips (profession idles, possessed overlays, captured, console, panic, alert, point, gestures, breathing loops)
are authored procedurally by the scripts in `Tools/blender/animation/` (project-original, same licence as the project).
