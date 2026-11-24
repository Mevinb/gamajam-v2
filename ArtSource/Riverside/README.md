# Riverside Blender source

Original PARASITE work-ferry, flood-control, radio and field-clinic art. No downloaded meshes or textures are used.

Run `Tools/blender/riverside/build_props.sh` to regenerate all eight runtime FBXs and the original 512px PBR textures. The script authors geometry and textures on the CPU and never invokes a renderer. It builds outside Assets in a temporary Logs staging directory, verifies the complete exports, then atomically replaces each finished FBX/PNG while retaining existing Unity metadata. It uses the existing Blender installation at `~/Data/Blender/current/blender`; set `BLENDER` to override.

`RiversideProps.blend` is the editable authoring lineup, grouped into named collections. Except the far-bank collection, models are spaced 10m apart for inspection in the source file. Each FBX is exported separately at its own metre-scale origin before the source lineup is arranged. Original texture images are packed into the blend. CinemaSteel and CinemaTimber reuse the existing original film-set albedos; runtime materials also bind those sets' normal and metallic/smoothness maps.

`asset_manifest.json` records exported local Unity bounds, merged mesh/material counts and triangle budgets. `export_verification.json` records independent CPU Blender FBX round-trip checks, including finite geometry/UV coordinates, identical material slots and triangles, fully triangulated geometry with no zero-area faces, and bounds matching within 0.002m. To rerun:

```bash
~/Data/Blender/current/blender -b -t 2 --python-exit-code 1 --python Tools/blender/riverside/verify_assets.py -- "$PWD"
```

The eight unique exports contain 84,710 triangles total. Runtime repeats the console and privacy-screen assets across the district. Models have no collision meshes. `AfterlakeProps` hides the earlier primitive renderers, preserves their collider objects and interaction points, and keeps the original animated terminal `Screen` renderers visible. The detailed foreground props cast and receive shadows. The distant river-bank mesh has no gameplay collision.

`RiversideArtImporter` preserves FBX metre scale and normals, imports normal textures as normals, keeps packed metallic/smoothness maps linear, and enables compressed mipmapped repeating textures. `RiversideResourceTests` checks the actual imported Resources assets, their scale, geometry budgets, material slots, absence of colliders, and all twelve PBR maps.

These are source/export checks. Final Unity material, lighting, navigation and performance evidence comes from the GPU captures and branch acceptance runs maintained by the root agent.

## Geometry cleanup after the first Unity import

The first GPU resource run found that Unity discarded 72 ferry triangles, 64 pump triangles and 24 industrial-console triangles. A CPU face audit traced these to bevel radii reaching half the thickness of thin glass, gauge-face and safety-surround panels, producing zero-area faces. Some converted font geometry also contained degenerate triangulation remnants. The source generator now limits a bevel to 40% of the smallest object dimension, freezes the triangulation before FBX export and removes zero-area faces. The independent FBX verifier rejects any remaining zero-area triangle. The cleanup uses the vector cross product rather than BMesh face area, which can report a tiny nonzero area for numerically collinear font vertices. Every exported triangle must retain more than 1e-9 square metres of area after an independent FBX import. The resource tests retain exact triangle-count checks against the cleaned exports rather than accepting missing geometry with a tolerance. Bounds, material slots, collider policy and interaction layout are unchanged.

## Unity axis boundary

An actual Unity material-bounds audit showed that this FBX path preserves X and Y but reflects the authored Z axis. Export-only temporary mesh copies now reflect Blender world Y and correct their winding, leaving the editable Blender source and runtime anchors unchanged. The CPU verifier uses the observed Unity coordinate mapping, and Unity tests check the intake lamp on +Z, cabin beacon on -Z, console-front labels, sample vials, dish X position and far-bank direction. Export meshes bake their object rotation, so the mast has a tighter 2.2 × 16.2411 × 1.51m renderer bound without moving any vertex in its authored world coordinates. Its prior bound was inflated by rotating a merged object's local bounding box.
