# Original Blender road vehicles

This visual kit replaces moving traffic, parked sedans/hatches/SUVs/vans/taxis and
the three existing road-closure box trucks. All models and surface maps are
original Blender manufacture, with no third-party asset dependencies.

The packed `Vehicles.blend` is the editable source. `Exports/` contains five
visual-only vehicle FBX files and a separate taxi roof sign. Vehicle files each
contain body, glazing and individual wheel meshes at three distance LODs.
`production-manifest.json` pins every export and map, bounds, wheel centres and
the actual triangle budgets. `cpu-verification.json` records independent FBX
round-trip checks, including ten rays through real glass apertures, 22 backed
weather-seal rays and explicit upward exterior/downward headliner normals. This is
source geometry evidence, not Unity appearance or performance acceptance.

The manufactured details include crowned hood/deck panels and closed 18 mm roof
shells with inner headliners; open wheel
arches; separate curved glass and structural window frames; seats, headrests,
dashboard, console and steering; recessed projector lamp assemblies; mirrors,
door seams/handles, registration lettering; rounded tyre sidewalls/tread,
separate alloy rings/spokes/fasteners and brake discs. The van has tapered cargo
sheet metal, pressed ribs, loading doors and hardware. The truck has a shaped
cab, edged cargo box, loading locks, six wheels and underrun protection.

Runtime surfaces share three 2048-pixel maps: sRGB detail albedo, linear tangent
normal, and linear metallic/occlusion/smoothness packing. Unity imports full
mips with streaming, BC7/BC5/BC7 compression and no CPU readability. Paint uses
URP Complex Lit clear coat; glass uses premultiplied transparent URP Lit with
back-face culling, no depth write and no shadow casting. Eight parked paint
assets are shared; moving cars tint only the paint submesh through property
blocks, without allocating per-car materials.

The visual prefabs contain no colliders or rigidbodies. The existing parked
footprints, road/truck colliders, deterministic parked choices and traffic
movement/braking/routing/audio remain authoritative. Each wheel's three meshes
are parented around a car-local X pivot at the original authored axle centre;
TrafficCar retains its existing spin calculation. The hatch visual is fitted
to the original moving-car envelope. No hardware ray tracing is claimed.

Author/recheck with the installed Blender, CPU and two threads:

```bash
nice -n 15 /home/mevlec/Data/Blender/current/blender --background --threads 2 --python-exit-code 1 --python Tools/blender/vehicles/author_vehicles.py -- --output ArtSource/Vehicles
nice -n 15 /home/mevlec/Data/Blender/current/blender --background --threads 2 --python-exit-code 1 --python Tools/blender/vehicles/verify_vehicles.py -- --source ArtSource/Vehicles --out ArtSource/Vehicles/cpu-verification.json
```

After source review, root runs the serialized Unity import before city generation:

```bash
Tools/unity.sh vehicle-art -executeMethod Parasite.EditorTools.VehicleArtImporter.PrepareBatch -quit
Tools/unity.sh vehicle-data -executeMethod Parasite.EditorTools.VehicleArtVerification.VerifyBatch -quit
```

The second command records an actual Unity import-data receipt under `Logs/`.
Then regenerate the scenes through CityBuilder, run the existing
`Parasite.Tests.TrafficSafetyTests`, inspect parked and moving vehicles
in the declared renderer, and build/measure the final player. Those acceptance
steps are separate from authoring and the CPU checks.

`Tools/blender/vehicles/review_vehicle.py` makes a small CPU Cycles preview with
explicit Nishita daylight and AgX. It writes only the specified output image
and does not modify the packed source. Native appearance and FPS still need
verification in the game atmosphere; the source render alone does not prove
photorealism.
