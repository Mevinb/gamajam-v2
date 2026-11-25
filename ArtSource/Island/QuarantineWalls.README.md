# Original island quarantine wall kit — review staging

No live Assets/source import/publication, Unity run, GPU render, Cycles/OptiX work, install or network access was performed. Blender4.5.16 CPU mesh/atlas authoring took2.37s with at most2threads. Full-story packed films/driver/policy remain unchanged; the separate frozen-input evidence verifies their hashes.

The target defect is visible in `Logs/ui/island-arrival-radio.png` and `island-southern-sign-approach.png`: large unsegmented concrete blocks dominate the entrance. Original primitive physics remains the authority. The new opaque precast skin adds panel joints, toe/coping, chamfered casting edges, tie recess normals, form marks and four restrained dirt/wetness variants.

## Files

- `ArtSource/Island/QuarantineWalls.blend`: original editable source, two final meshes and a non-exported wire collection showing the seven unchanged boxes. Three2K A/N/M maps are packed with portable relative paths; no cameras or lights.
- `Assets/Parasite/Resources/Island/IslandQuarantineWalls.fbx`: two merged opaque meshes,11,804triangles, one shared `IslandQuarantineConcrete` material. No colliders/bones/animation.
- `Assets/Parasite/Resources/Island/Textures/IslandQuarantine_{A,N,M}.png`: shared2048atlas with four padded variants; Aopaque/sRGB, Nlinear tangent normals, Mlinear metallicRGB0/smoothnessAlpha.
- `Tools/blender/island/build_quarantine_walls.py` and `verify_quarantine_walls.py`: reproducible author/export and CPU FBX round-trip verification, staging-only.
- `integration.patch`: three small source overlays. New-model visuals load before disabling only the seven original direct wall MeshRenderers; every original box expression/transform/collider remains. New material-prefix mapping and new-texture-prefix-only2048import limit leave all old island maps512.

## Evidence

- `geometry-uv-bounds.json`: seven exact before/after bounds, maximum12mm shallow recess,5,082opaque six-sided ray checks,507clear opening rays, nondegenerate UVs and material/triangle counts.
- `export-verification.json`: actual Blender FBX round-trip under the project's observed Unity axis mapping. All106m bounds match, negativeX openings are clear and mirroredpositiveX counterparts solid, both meshes preserve11,804triangles/UVs/outward winding/material slot;1,050opaque coverage rays pass.
- `packed-source.json`: three embedded portable2048images, no camera/light/render state.
- `runtime-integration.json`: all22original Box expressions are byte-identical; PhysicsColliders NavMesh collection remains unchanged. No actual Unity capsule/nav/LOS pass is claimed.
- `geometry-before-after.svg`: CPU vector contract diagram only, not a visual result.
- `frozen-film-inputs-after.json`: unchanged full-story blends/driver plus policy equal to its approved publication hash.

The original7active wall renderers/84triangles become2active wall renderers/11,804triangles. One added shared material, no new lights or shader passes. The expected compressed mipmapped atlas footprint is16MiB (64MiB if uncompressed); actual Unity format/performance is pending. The packed blend is14.3MB; the complete staged source/export package is below75MB. The diagram/data prove geometry, not improved in-engine appearance.

## Reproduce after authorization

From project root with cached Blender, no rendering:

```bash
CUDA_VISIBLE_DEVICES='' OPENBLAS_NUM_THREADS=2 OMP_NUM_THREADS=2 MKL_NUM_THREADS=2 ~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 --python Tools/blender/island/build_quarantine_walls.py -- --project "$PWD" --stage "$PWD/Logs/island-wall-rebuild"
CUDA_VISIBLE_DEVICES='' OPENBLAS_NUM_THREADS=2 OMP_NUM_THREADS=2 MKL_NUM_THREADS=2 ~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 --python Tools/blender/island/verify_quarantine_walls.py -- "$PWD" "$PWD/Logs/island-wall-rebuild"
```

Root reviews/imports/publishes atomically only after tests stop. Then actual same-camera arrival/sign/corridor images, M18routes/camera/LOS and relevant139-source compile checks must pass. Compare GPU memory/draw submissions and a fixed1080pHigh/Ultra island view before claiming a performance result. The historical city/riverside benchmarks are headroom evidence, not an island benchmark.


## Integrated Unity verification

The wall kit is now imported in the working tree. The importer explicitly sets Texture2D shape: the first automatic imports
were Cubemaps and failed surface loading. The retained import report passes140actual checks, including all three2048BC7 maps,
all18old maps512, two renderers/one material/11804triangles/no colliders. All four M18 route/outcome combinations and the full
camera check pass with this kit. Two visual cases pass the original physical/raster aperture thresholds after Atmosphere is
isolated only for structural raster checks and its exact prior state restored. Gameplay atmosphere/physics are unchanged.

The optional HDR staged art-QA passes, with linear FP16 throughout rendering/readback and one sRGB conversion. `WallReview`
contains unedited same-camera before/after PNGs; actor renderers and UI are hidden for these environment comparisons, while
owned lights/normal fog remain. These are staged art-QA images, not a native gameplay/UI record. Exact output/report hashes and
scope live in `quarantine-wall-runtime-verification.json`.

The first actual1920x1080/Vulkan island baseline completes20/20waypoints: High25.5FPS,Ultra26.7FPS. This is below the desired
playability target and is being profiled; no performance improvement or new measured result is implied by the mesh budget.
