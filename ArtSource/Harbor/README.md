# Harbor mechanics art and district

Nine original visual-only Blender assemblies support the Harbor chapter: valve pedestal and independently rotating handwheel, pressure-gauge stand and separate needle, quarantine lift leaf, roller conveyor, sealed cargo crate, relay treadpad and optical emitter. The kit has **8,328 distinct triangles and 244 closed manufactured solids**. Each FBX has one mesh/material, no imported physics or animation, and a metre-scale local origin.

The packed `Harbor.blend` retains the unchanged original `CityGatePilot_SharedPBR` material and all three original gate atlas images. The source images are byte-verified; no PBR maps are generated or duplicated into runtime Harbor assets. The owned runtime `HarborSharedPBR` material references the exact existing imported gate texture GUIDs. Runtime copies and indicator variants are owned by `HarborArt`; status changes use material property blocks rather than changing source materials.

## Geometry and pivots

`production-manifest.json` records each FBX's measured bounds and triangle count; `piece-map.json` records each manufactured solid and positive source signed volume. `export-verification.json` independently opens the packed source and imports all nine FBXs into empty Blender scenes. It verifies finite bounded data, padded atlas UVs, nondegenerate geometry/UV area, closed consistently outward components, source hashes and the four PNGs. Overlapping manufactured pieces are intentionally not represented as a boolean-unioned volume.

Both wheel and gauge needle are authored around local zero and rotate about Unity local Z. Runtime places the wheel at pedestal-local `(0,1.1,-.36)` and the needle at gauge-local `(0,0,-.18)`. The gauge station root uses `HarborLayout.GaugeLocal`. The gate leaf is centred at local zero and uses the exact layout moving-root centre/size; the world supplies its reviewed simple BoxCollider, carving obstacle and static-nav exclusion. Conveyor roller crowns are `.773m` above export origin; runtime places them at `-.223m` so crowns match the crate-bottom route height `.55m`. Crate roots are bottom-centred, size `1.2×1×1.4 m`; their bodies/colliders are created explicitly by the district, not imported.

`HarborDistrict` repeats the kit 34 times (including 15 conveyor sections), records instantiated model triangles through `HarborArt.RepeatedModelTriangles`, and reuses the existing detailed Riverside ferry as distant nonwalkable scenery. The exact repeated budget is in `district-contract.json`. The bounded yard has continuous footing at `.15 m`, visible matching perimeter solids, a full quarantine wall with the sole gated aperture, real cargo-grille bars with `.35 m` gaps, a nonwalkable cargo volume, public role hosts, two patrol routes and cover pockets. Scanner views pass through a real grille gap. Pads are visual children lowered to meet the same footing; the protected optical arms/lenses visibly mark the exact supplied sight points. The dedicated arrival-caption owner is created by the director integration and is not attached to the district root.

## Review images and limits

`Review/` contains four inspected 960×720 CPU views: kit overview, pressure works, conveyor/cargo and relay manufacturing. Each uses eight Cycles samples, two fixed CPU threads and CPU OpenImageDenoise. They are authoring evidence, not native gameplay captures.

The author and independent roundtrip verifier each exited 0. Runtime/Editor compilation is coordinated by the parent. Unity import, generated district physics/navigation/LOS, complete mission behavior, native rendered appearance/performance, shader/probe lifetime and player acceptance require their own receipts; these CPU artifacts do not establish those results. The existing user's immutable live game was not changed or interrupted by this authoring run.

The implementation owns only the new Harbor world/art scripts, scoped importer, `Resources/Harbor`, this source package and `Tools/blender/harbor`. Existing game integration and mission mechanics have separate owners. Existing scenes are never hand-edited.
