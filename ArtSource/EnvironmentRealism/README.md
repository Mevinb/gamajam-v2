# Environment realism kit

This is a bounded visual upgrade for the generated city: actual Blender geometry adds dressed window jambs, projecting sills and drip edges, rebated frames, jointed stone entrances, metal door hardware, folded condenser cases with radiator louvres/fan guards/fasteners, individually bevelled curb stones, cast storm drains, fractured stones and folded grass blades. The existing photographed facades, interior-mapped glazing, scanned trees and HDRI remain the large-scale source assets.

`EnvironmentRealism.blend` is the editable, packed source. `production-manifest.json` records the seven exported meshes, metre bounds, source pieces, texture channel meanings, byte sizes and SHA256 digests. Runtime exports live only at `Assets/Parasite/Art/EnvironmentRealism`; the packed source deliberately contains the three maps so it opens without external dependencies. No additional FBX copies are kept under ArtSource.

Author with Blender 4.5 LTS, at low priority and two CPU threads:

```bash
nice -n 15 ~/Data/Blender/current/blender -b -t 2 --python Tools/blender/environment_realism/author_environment.py -- "$PWD"
nice -n 15 ~/Data/Blender/current/blender -b -t 2 --python Tools/blender/environment_realism/verify_environment.py -- "$PWD"
```

The source contains four procedural material masters, baked in CPU Cycles into one shared 2K atlas: limestone, powder-coated steel, oxidised cast iron and foliage. Albedo is unlit sRGB. The normal map is linear tangent-space OpenGL +Y. The linear packed mask stores metallic in R, neutral 0.98 occlusion in G, unused B and smoothness in A. The material does not paint in illumination or claim baked geometry AO. Standalone import uses BC7 albedo/mask and BC5 normals with full mip chains, approximately 16 MiB of compressed texture payload before residency/allocation overhead. This is a calculated payload, not measured VRAM.

The seven meshes total 4,888 source triangles before placement. Their local axes are X right, Y up and -Z outward in Unity. Blender source coordinates are X right, Z up and +Y outward; FBX uses explicit -Z forward/Y up conversion. The first three visible upper storeys receive window reveals. Their open aperture matches the photographed glass envelope at 0.62 bay/floor size; the original glazing/emission remains visible through the open centre. Window placement consumes no RNG. Lower entrances retain their old steps and lamps. Condensers reuse existing rooftop positions, dimensions and random selection. Cast drains retain the prior 0.9 by 0.45 metre plan. Curbs retain prior strip centres/height and use 3 mm joints. Grass and pebbles stay within existing tree pits.

`EnvironmentRealismAssets` merges these meshes into the existing WorldKit static material/cell batches. It creates no colliders, nav modifiers, lights, gameplay objects or runtime scripts. All existing building/ground/tree collision calls and random-number calls remain intact. No generated scene is hand-edited. Existing material texture scans and metre UVs remain bound to the ground; `Parasite/RealisticSurface` adds only restrained world-space albedo and smoothness variation. It uses the standard URP Lit constant-buffer layout and stock shadow/depth passes.

The CPU verifier audits real FBX round-trip axes/bounds, triangle counts, UV range, one material slot and packed texture hashes. A passing source audit does not establish imported Unity scale, shader compilation, scene appearance, physics/NavMesh equality, native FPS or GPU memory. Root integration must compile, regenerate with CityBuilder, inspect matched street/roof/ground views, compare collider/navigation contracts and build/test the target Linux player. No broad island/river landscape or final-cinematic realism acceptance is implied by this city kit.

All new kit geometry, shaders and procedural surface sources are original PARASITE work; no new third-party downloads are used.
