# PARASITE expansion and realism upgrade

The current request reopens feature development and authorizes frequent commits and pushes to `Mevinb/gamajam-v2`.
Older handoff statements freezing the eight-mission release, or prohibiting pushes for its capture session, are historical.
The full upgrade remains in progress; a passing compile or a small acceptance suite does not establish that every bug is fixed.

## Requested outcome and evidence

| Requirement | Evidence needed before calling it complete |
|---|---|
| Fix game bugs | Reproduce reported defects, fix them, exercise affected behavior and run campaign regressions on the rebuilt player. Keep remaining known defects visible. |
| More realistic graphics, textures, models and shaders | Integrated assets, fresh gameplay/cinematic renders reviewed at their actual resolution, and a fresh RTX 4050 benchmark at declared settings. |
| Blender-authored redesign | Reproducible Blender scripts, editable `.blend` source, exported assets actually used in the game, documented ownership/licences. |
| Redesign opening and ending | Fresh renders of every opening shot and both lakeside resolutions; verify skip, explicit choice, simulation freeze and restoration. |
| Continue beyond the lake | Reach the existing lake by playing the campaign, choose either future, enter the new chapter, complete its objectives and verify checkpoint recovery for both branches. |
| Keep expanding missions and improve play | Playable missions with distinct mechanics, clear objectives, fair access to needed hosts, and acceptance covering success, cancellation and recovery. |
| Use the RTX 4050 and add ray tracing | Confirm the rendering GPU and supported API. Document measured performance and distinguish hardware ray tracing, offline path tracing and screen-space reflections. |
| Multiple agents | Parallel campaign, Blender cinematic and graphics implementation, followed by integration review. |
| Push important working steps | Feature commits authored as Mevinb, verified push to `origin/main`, no generated build/cache/log directories committed. |

## Rendering direction

The current delivery platform is Linux x86-64, Unity 6000.3.29f1, URP 17.3.0, NVIDIA RTX 4050 Laptop GPU (6 GB).
URP's screen-space reflection shader traces the camera depth buffer. It does not establish RT-core or DXR use.
Unity's built-in HDRP hardware ray tracing requires a different pipeline and a supported platform/API; do not advertise the
Linux URP player as hardware ray traced. The user explicitly selected **retain Linux and add RTX-rendered Blender cinematics**,
alongside improved real-time shaders. Do not add a Windows/DX12 target for this upgrade.

Primary references: [Unity render-pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison),
[HDRP 17.3 ray-tracing requirements](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition@17.3/manual/Ray-Tracing-Getting-Started.html).

## Integration work in progress (25 November 2026)

- Preserve the existing 14 missions and all three infiltration routes.
- Add a branch-sensitive riverside chapter after an explicit lake choice and a deliberate Continue action.
- Author detailed containment and shoreline sets in Blender and integrate their exports into both cinematics.
- Correct reflection hit detection, material/lighting shader variants and preset behavior; regenerate the scene assets.
- Prevent traffic from overshooting pedestrian clearance or intersection stop lines during frame delays.
- Replace contradictory current-state documentation with artifact-specific build and test evidence once verification finishes.

`TEST_REPORT.md` records completed verification. `KNOWN_ISSUES.md` records remaining limits. `HANDOFF.md` describes the
architecture and supported commands. Build output and captures remain ignored in `Builds/` and `Logs/`.
