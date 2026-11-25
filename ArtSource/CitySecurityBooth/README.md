# City security booth — visual source package

This replaces the booth's visible shell beside the city gate. The editable packed Blender source and visual-only FBX use one joined `SecurityBoothShell` mesh, 6,012 triangles and the existing `CityGatePilot_SharedPBR` material. The original three 2K atlas images are reused byte for byte; no texture files or additional runtime texture allocations are required by this package.

The design includes bevelled precast face panels and compression joints; manufactured observation-window frames, gaskets, mullions, sills and screws; opaque dark reflective glazing; a rear louvered vent and small flush service plate; folded roof coping, drip edges, a rear gutter and a clipped downpipe. The service plate has no handle, hinges or human entrance affordance. Glazing is opaque, has zero transmission and emits no light. The original solid booth intent is retained.

## Placement and integration contract

Place the FBX root at Unity world **(4.25, 0, 55.1)** with identity rotation and scale. Local visual bounds are **(-1.55, 0, -1.8) to (1.55, 3.15, 1.8)** metres, within the existing roof envelope. The body reference is **(-1.25, 0, -1.5) to (1.25, 2.95, 1.5)**. Small manufactured surface details extend outside the body reference but remain within the roof envelope.

Keep the existing body/roof/window construction calls and all original collider, navigation and line-of-sight behavior. Replace only their visible renderers after the complete kit has passed import readiness. Keep the original Unity `SECURITY` text at **(4.25, 3.05, 53.27)**; the export contains no replacement text. Assign the already imported `CityGatePilot_SharedPBR` material to the FBX renderer. Do not create colliders, rigid bodies, animation, additional materials or texture imports from the export.

The FBX contains only the selected visual mesh. The packed `.blend` also contains wire body/roof reference meshes and a QA ground, cameras and lights in clearly named separate collections. Those editing/QA objects are not exported. The visual material is shared; the QA ground's separate material is used only in the `.blend` review setup.

`physical-contract.json` records exact measured bounds and window placements. `piece-map.json` lists every one of the 173 closed manufactured solids, including atlas tile, bounds, vertex count and outward signed volume. The joined mesh uses overlapping closed detail pieces, not a single boolean-unioned volume.

## Evidence and limits

`production-manifest.json` pins the author recipe, source gate blend, original atlas and generated files. `export-verification.json` is produced by the independent CPU verifier after opening the packed source and importing the FBX into an empty Blender scene. It checks source/image hashes, one exported material, identity root, finite in-range UVs, nonzero geometry/UV area, closed consistently outward solids, exact bounds, no animation/physics, and opaque rays through front/rear/sides. The two PNGs are CPU authoring views at 960×720, eight samples and two threads, with CPU OpenImageDenoise.

These are authoring checks. Actual Unity import, culling, material reassignment, generated-scene preservation and native gameplay appearance/performance require separate integration verification. No Unity scene, editor builder or runtime source was changed by this authoring package.

## Reproduction

See [the authoring tools](../../Tools/blender/city_security_booth/README.md). The author command is dry by default and refuses to overwrite an existing blend, FBX or production manifest. Use a fresh output directory when regenerating. The packed blend is directly editable without external downloads.
