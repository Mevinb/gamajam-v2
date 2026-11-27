# Portable Van headlamp refinement

`author_vehicles.py` is the exact proposed procedural generator (`802f0acd…`). It retains the previous generator's vehicle definitions and adds the six-mesh lamp hook. `refine_van_lamps.py` is the narrow repeatable route: it opens an explicitly provided complete curved-body baseline, revises only Van Body/Glass at three authored LODs, preserves the original twelve wheel buffers, and optionally exports only VehicleVan. It never calls the whole-kit generator main or regenerates textures.

The exact original inputs are preserved at `ArtSource/Vehicles/RefinementSources/Vehicles.before-headlamps.blend` (`9178639e…`) and `ArtSource/Vehicles/RefinementSources/author_vehicles.before-headlamps.py` (`0a834f2d…`). They are editable source dependencies, not hidden temporary inputs. The recipe accepts any explicit paths and has no dependency on this working stage or an absolute machine path.

The two Python sources live together in `Tools/blender/vehicles`. A reproduction command is:

```bash
blender --background --threads 1 --python-exit-code 1 --python Tools/blender/vehicles/refine_van_lamps.py -- --source ArtSource/Vehicles/RefinementSources/Vehicles.before-headlamps.blend --source-sha256 9178639e9518962b1dfe98df0625fecd6c21edab8c631cca3749577077de1f10 --generator-sha256 802f0acd946abf050fd1d77e173a7c138a42ee2a9f887becc80f6f1a839dff1a --output /path/to/fresh/output --export-van
```

Use the project CPU budget (20%, 768MiB, nice19, one thread) around this command. The source and output must be distinct, and output must not already exist. The source hash deliberately rejects reapplying the recipe to an already refined kit. Preserve the existing three PNGs unchanged alongside the result; no image bake is required. Do not run the generator's whole-kit main for this narrow integration.

The actual staged replay produced97 objects, the original79 non-Van objects, ten material roles, twelve unchanged wheels and50415/15816/3928 Van triangles. It saved a complete editable blend plus one Van FBX. Its exact FBX geometry, normals, indices, UVs, smoothing, material roles/properties and units match the accepted final candidate. FBX bytes differ because Blender regenerates object/document IDs, creation timestamp and input-file metadata. These differences are recorded separately and do not relax any geometry comparison. Fresh complete-source verification is a separate receipt; do not infer it from the export proof alone.

The accepted editable lamp-only source remains `d70368d0…`, with accepted FBX `eb3fd086…`. Those exact files are retained unchanged. Root reviewed the actual15 URP images and accepted visible orange amber at all forced LODs and the quarter view. This establishes static appearance in those views; driving, automatic LOD transitions, blinking and complete photorealism are outside that acceptance.
