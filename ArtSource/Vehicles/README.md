# Original Blender road vehicles

This original Blender kit supplies moving traffic, parked cars/vans/taxis and road-closure trucks. Five vehicle FBXs contain body, glazing and individual wheels at three LODs; a sixth FBX contains the taxi roof sign. Existing gameplay colliders, wheel centres, traffic movement/braking/routing and audio remain authoritative.

Van revision 4 replaces the slab-like primary form with curved cargo sides/tumblehome, a rolled 18 mm roof/headliner, a short cowl, bowed glazing, backed aperture returns and a furnished cab. Closed shoulder backings extend 12 mm inwardX and 4 mm up while preserving every exterior shoulder point. The old disconnected sheets faced inward after normal recalculation and produced a daylight slit under Unity backface culling; the repair closes the geometry with Paint exterior/caps and Interior liner. Existing culling and material roles remain intact.

The actual cleaned Van triangle counts are **49,783 / 14,660 / 3,702**, within the unchanged 50,968 / 16,166 / 4,006 ceilings. All three CPU proofs preserve raw non-shoulder/Glass geometry exactly. Six post-decimation/cleanup shoulder components pass closed/oriented topology, positive volume and 162 outside-Paint→Interior ray pairs, including near-end samples. The unchanged roof/aperture checks also pass, and all 12 wheel vertex multisets match the retained export exactly.

`Vehicles.blend` is the complete editable source: 97 objects, including 18 revised Van objects and 79 preserved non-Van objects; 10 original material datablocks; and the original single packed `VehicleDetail_N` image. All three A/N/M PNGs remain external under `Textures/`. The five other exports, three external PNG byte streams and all 36 existing vehicle metas are unchanged. Independent fresh-process reopen proves the revised Van geometry, UVs, indices, normals, tangents, polygon material indices and transforms match the accepted standalone source. The original non-Van object/mesh/collection/material fingerprint is preserved.

| Current file | SHA256 |
|---|---|
| `Tools/blender/vehicles/author_vehicles.py` | `0a834f2d3dde21646276d6fa5eb30fe5a8613dfb1a2b4ae35f931c1e0cccb71c` |
| `ArtSource/Vehicles/Exports/VehicleVan.fbx` | `e9a35874249ce13ee27b6f0859eecdb8c07b94e15b5f736d8be02854864b5e27` |
| `ArtSource/Vehicles/Vehicles.blend` | `9178639e9518962b1dfe98df0625fecd6c21edab8c631cca3749577077de1f10` |

The runtime FBX has the same bytes as the source export. Its original model GUID `0774415ab918974a3a9a5133d4eb827f` and prefab GUID `d42680d6403b128cb9f525fd578790bb` are retained. `production-manifest.json` pins exports/maps, LOD counts, bounds and wheel centres. Initial full-kit CPU evidence remains in `cpu-verification.json`; current Van evidence is linked in [the revision report](../../Docs/verification/van-refinement-20261128.json).

Unity uses three 2048 px maps with full streamed mips, BC7/BC5/BC7 compression and no CPU readability. Paint uses URP Complex Lit clear coat; thin glass uses premultiplied transparent URP Lit, backface culling, no depth write and no shadow pass. The surface-detail packing supplies local AO; per-material metallic/smoothness values remain authoritative. Parked cars share eight authored paint assets; moving cars tint the paint slot with property blocks. Visual prefabs contain no colliders or rigidbodies, and wheel pivots retain the authored car-local X axes.

Actual RTX 4050/Vulkan static review exits 0 with 18 images, 18 independently settled arms and no captured warnings/errors. All six candidate views—LOD 0 front/rear/roof/side and forced LOD 1/2 front—are accepted for the Van primary form. The narrow canonical `PrepareVanBatch` import also exits 0 against the 82-entry vehicle inventory. CityBuilder regeneration and post-import review also exit 0. The current-source capture contains 39 images with no captured warnings or errors. All 18 prefab parts and both generated Van instances (36 scene parts) use the reviewed compact material arrays and retain their named mesh subasset IDs. Semantic comparison preserves all colliders, gameplay markers and non-Van renderers; visual LOD reference centres shift down 0.86625 mm within the original physical envelope.

Earlier native visual fixtures v1/v2 remain failed evidence: a compact-material expectation error, then inactive-group ForceLOD/restoration errors. The inward shoulder defect was separately visible in retained images. Rejected complete-source merge v9 also remains: clearing/rebuilding material slots reset polygon material indices, caught on fresh reopen. Merge v10 assigns slots in place and passes independent fresh reopen. These failures are retained and identified in the revision report.

For a Van-only replacement, review source/manifest first, then use the narrow importer and regenerate scenes through CityBuilder:

```bash
Tools/compile_check.sh all
Tools/unity.sh vehicle-van -executeMethod Parasite.EditorTools.VehicleArtImporter.PrepareVanBatch -quit
Tools/unity.sh vehicle-city -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch
```

Regeneration is required even when mesh GUID/local file IDs remain stable: the generated City serializes material arrays, and the revised Body submesh order differs. Full-kit authoring with `Tools/blender/vehicles/author_vehicles.py` should write a fresh reviewed Logs directory before promotion. Preserve the five unchanged exports during a Van-only revision.

This acceptance covers static imported geometry/materials/pivots and forced-LOD appearance. Driving, automatic LOD transitions and performance are unexercised. Uniform paint and simple lamps remain visible limitations; full photorealism is not claimed.
