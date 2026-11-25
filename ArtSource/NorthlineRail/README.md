# Northline rail source art

This source package contains eleven original visual-only rail assemblies, a packed editable `NorthlineRail.blend`, and the manufacturing manifests. The geometry/export foundation is accepted for later integration. Unity import, in-game appearance, physics/navigation, performance and the broader realism upgrade remain unverified for this kit.

All eleven exported files independently round-trip through Blender: **7,336 triangles and 178 closed outward manufactured components**. Checks cover one mesh and shared material per export, exact vertex/triangle/component counts, finite coordinates and UVs, nondegenerate triangles and UV area, consistently paired edges, positive component volume, imported shading-normal direction and measured bounds. Maximum bounds error is `4.77e-7 m`. Overlapping manufactured parts are not claimed to form one boolean-unioned surface.

| Model | Triangles | Closed components |
| --- | ---: | ---: |
| RailTram | 1,724 | 37 |
| RailTransferCradle | 736 | 24 |
| RailControl | 464 | 12 |
| RailLowSignal | 236 | 5 |
| RailPointBlade | 264 | 6 |
| RailCrossingBarrier | 364 | 13 |
| RailServiceGate | 236 | 5 |
| RailPlatformGate | 280 | 6 |
| RailPortalGate | 368 | 8 |
| RailDepartureCoach | 2,508 | 49 |
| RailBoardingRamp | 156 | 13 |

The conventional coach is about `3.204 m` across Unity X and `14.019 m` along Z. Its deck top is `.75 m` above origin; visual bounds are `(-1.602,.15,-7.0095)` to `(1.602,3.29,7.0095)`. The ramp pivot is its low western foot: bounds `(0,.13,-1)` to `(3.35,.75,1)`. The separate bright review aligns its upper edge about `.048 m` short of the coach's west side. Other exact local bounds and pivots are in `production-manifest.json`; moving leaves are centred on their moving roots. These are visual contracts, not imported collision or gameplay behavior.

The export boundary matches the existing gate and booth: author Unity `(x,y,z)` as Blender `(x,-z,y)`, reflect only temporary export copies along Blender Y and reverse their winding, then use baked-space FBX with `-Z` forward/Y up. Independent imported world coordinates decode using `(x,z,y)`. Control, crossing barrier and portal asymmetry distinguish that mapping; several symmetric bounding boxes cannot distinguish a Z reflection, and the verification records that ambiguity. Actual Unity handedness, culling and material assignment still require scoped importer checks.

The single `CityGatePilot_SharedPBR` material and all three packed images are unchanged originals from `ArtSource/CityGatePilot`. There are no new atlas images or runtime texture copies. The original blend retains packed images, so the promoted source opens with embedded data. Its external image path strings retain the original author output's `Logs/rail-author-runs/20261125T211444784842Z-4421ba02/authored` base. If deliberately unpacking/reloading, rebase to `ArtSource/CityGatePilot/Textures/CityGatePilot_{A,N,M}.png` and verify the manifest hashes; do not assume the old relative paths resolve from this new directory.

`production-manifest.json`, `piece-map.json`, all eleven FBXs and the packed blend are byte-identical copies of the actual successful author run. `export-verification.json` is the unchanged successful independent review/round-trip receipt. Those manifests retain provenance-only `Review/` image references: the PNGs remain in ignored Logs, not this source package. `promotion-record.json` records each copy and the portable verifier adaptation. `package-index.json` pins the promoted source/tool files.

The original three author reviews were too dark/cropped for appearance acceptance and remain preserved. Four subsequent 1280×800 studio views use actual Cycles CPU, two fixed render threads, eight samples and CPU OpenImageDenoise. The full coach/ramp and whole-kit views have measured complete bounding-corner framing; the door/bogie view intentionally frames a detail region. Root viewed all four brighter views and accepted the geometry/export foundation. The review changes lights, camera and review-copy placement only; source geometry, material graph, packed maps and original files remain unchanged. These are CPU authoring views, not screenshots of the game.

Actual evidence locations:

- Author: `Logs/rail-author-runs/20261125T211444784842Z-4421ba02/author-receipt.json`.
- Independent checks and brighter images: `Logs/rail-review-runs/20261125T212030697054Z-75f6470a/review-receipt.json` and its `review/Review/` directory.
- Tracked scope: [authoring proof](../../Docs/verification/northline-blender-authoring-20261126.json).
- Tools and fresh-output reproduction: [Northline tools](../../Tools/blender/northline/README.md).
- Ownership and licensing provenance: [CREDITS.md](CREDITS.md).

No Assets, scene, importer, collider, runtime or package-settings files were promoted with this source art. The open user preview stayed unchanged and was never signaled.
