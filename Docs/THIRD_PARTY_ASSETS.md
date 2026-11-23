# Third-party assets

Every external asset used by PARASITE, with author, source and licence. All entries are CC0 (public domain) unless
noted otherwise. Add new rows to the matching table.

## Textures & HDRI

All from [Poly Haven](https://polyhaven.com) (CC0 1.0). Downloaded through the Poly Haven API at 1K (asphalt albedo 2K):
albedo (`*_albedo.jpg`), OpenGL normal (`*_normal.jpg`), roughness (`*_rough.jpg`). `CityBuilder` derives
`*_mask.png` (metallic in R, smoothness = 1 − roughness in A) from the roughness maps.

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Asphalt 02 | Rob Tuytel | https://polyhaven.com/a/asphalt_02 | CC0 | Assets/Parasite/Art/City/asphalt/ | Roads, ground (`Asphalt.mat`) |
| Concrete Pavement | Charlotte Baglioni | https://polyhaven.com/a/concrete_pavement | CC0 | Assets/Parasite/Art/City/sidewalk/ | Sidewalks, plaza (`Sidewalk.mat`) |
| Brick Wall 02 | Dimitrios Savva | https://polyhaven.com/a/brick_wall_02 | CC0 | Assets/Parasite/Art/City/brick/ | Brick facades (`Brick.mat`, `BrickDark.mat`) |
| Plastered Wall 02 | Charlotte Baglioni | https://polyhaven.com/a/plastered_wall_02 | CC0 | Assets/Parasite/Art/City/plaster/ | Plaster facades, lab walls (`Plaster*.mat`, `LabWhite.mat`) |
| Rusty Metal 02 | Rob Tuytel | https://polyhaven.com/a/rusty_metal_02 | CC0 | Assets/Parasite/Art/City/metal/ | Worn metal (`RustyMetal.mat`) |
| Dark Wooden Planks | Amal Kumar | https://polyhaven.com/a/dark_wooden_planks | CC0 | Assets/Parasite/Art/City/wood/ | Benches, desks, shelves, hoardings (`Wood.mat`, `Hoarding.mat`) |
| Floor Tiles 08 | Rob Tuytel | https://polyhaven.com/a/floor_tiles_08 | CC0 | Assets/Parasite/Art/Lab/labfloor/ | Lab floor (`LabFloor.mat`) |
| Concrete Wall 008 | Dario Barresi, Charlotte Baglioni | https://polyhaven.com/a/concrete_wall_008 | CC0 | Assets/Parasite/Art/Lab/concrete/ | Fences, curbs, service tunnel (`Concrete.mat`, `Curb.mat`, `TunnelConcrete.mat`) |
| Metal Plate | Rob Tuytel | https://polyhaven.com/a/metal_plate | CC0 | Assets/Parasite/Art/Lab/labmetal/ | Lab steel, boilers, tanks (`SteelPlate.mat`) |
| Joburg Central Sunset (HDRI, 1K .hdr) | Greg Zaal, Dimitrios Savva | https://polyhaven.com/a/sunset_jhbcentral | CC0 | Assets/Parasite/Art/City/sky/sunset_jhbcentral_1k.hdr | Evening skybox + ambient/reflections (`SkyEvening.mat`) |

### City visual overhaul (Nov 2026) — ambientCG + Poly Haven

ambientCG (https://ambientcg.com, CC0 1.0) materials were fetched with `Tools/fetch_acg.sh` and processed by
`Tools/gen_city_art.py` (raw downloads are not kept in the repo; re-run the two scripts to regenerate). Processing:
facade wall recolouring + leak/dirt weathering, URP mask packing (R metallic, G AO, A smoothness), procedural lit-window
emission maps, leaf-cluster cards composed from leaf atlases, decal alpha packing.

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Facade 018 A (2K) | ambientCG | https://ambientcg.com/a/Facade018A | CC0 | Assets/Parasite/Art/City/Facades/Res_*, Brick{Red,Cream,White,Ochre}_* | Residential brick facades (`FacadeBrick*.mat`) |
| Facade 019 A (2K) | ambientCG | https://ambientcg.com/a/Facade019A | CC0 | Assets/Parasite/Art/City/Facades/Office_*, Stone{Grey,Sand}_* | Office/stone facades, tower podiums (`FacadeStone*.mat`) |
| Facade 020 A (2K) | ambientCG | https://ambientcg.com/a/Facade020A | CC0 | Assets/Parasite/Art/City/Facades/Loft_*, Loft{Brown,Charcoal}_* | Loft facades (`FacadeLoft*.mat`) |
| Facade 012, 015, 016, 017, 009, 006 (1K) | ambientCG | https://ambientcg.com/a/Facade012 (…/Facade015, 016, 017, 009, 006) | CC0 | Assets/Parasite/Art/City/Skyline/Tower* | Skyline towers (`Tower*.mat`) |
| Asphalt 026 C (2K) | ambientCG | https://ambientcg.com/a/Asphalt026C | CC0 | Assets/Parasite/Art/City/Ground/Road_* | Roads, with damp patches (`Asphalt.mat`) |
| Paving Stones 070 (2K) | ambientCG | https://ambientcg.com/a/PavingStones070 | CC0 | Assets/Parasite/Art/City/Ground/Cobble_* | City Plaza paving (`Cobble.mat`) |
| Gravel 040 (1K) | ambientCG | https://ambientcg.com/a/Gravel040 | CC0 | Assets/Parasite/Art/City/Ground/Gravel_* | Flat roofs (`Gravel.mat`) |
| Leaking 012 A, Leaking 017 B (2K) | ambientCG | https://ambientcg.com/a/Leaking012A , https://ambientcg.com/a/Leaking017B | CC0 | baked into Facades/*_albedo.jpg | Facade rain streaks |
| Graffiti Set 001 (2K) | ambientCG | https://ambientcg.com/a/GraffitiSet001 | CC0 | Assets/Parasite/Art/City/Decals/Graffiti_* | Wall / hoarding tags (`DecalGraffiti.mat`) |
| Asphalt Damage Set 001 (2K) | ambientCG | https://ambientcg.com/a/AsphaltDamageSet001 | CC0 | Assets/Parasite/Art/City/Decals/AsphaltDamage_* | Road patches (`DecalAsphaltDamage.mat`) |
| Manhole Cover 011 (1K) | ambientCG | https://ambientcg.com/a/ManholeCover011 | CC0 | Assets/Parasite/Art/City/Decals/Manhole_* | Manholes (`DecalManhole.mat`) |
| Leaf Set 024 (2K), Leaf Set 030 (1K) | ambientCG | https://ambientcg.com/a/LeafSet024 , https://ambientcg.com/a/LeafSet030 | CC0 | Assets/Parasite/Art/City/Foliage/Leaves{Green,Autumn}_* | Tree leaf-cluster cards |
| Foliage 001 (1K) | ambientCG | https://ambientcg.com/a/Foliage001 | CC0 | Assets/Parasite/Art/City/Foliage/Grass_albedo.png | Grass tuft cards |

Poly Haven models (https://polyhaven.com, CC0 1.0), FBX 1K + glTF texture set via `Tools/fetch_polyhaven.py`, copied
into `Assets/Parasite/Art/City/Props/<id>/` by `gen_city_art.py props` (ARM → URP mask; lamp-glass opacity → emission).
Unity generates Mesh LODs at import; `CityAssets.PropLod` picks the LOD that is merged into the static city batches.

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Street Lamp 01 | Josh Dean | https://polyhaven.com/a/street_lamp_01 | CC0 | Assets/Parasite/Art/City/Props/street_lamp_01/ | Street lamps |
| Street Lamp 02 | Josh Dean | https://polyhaven.com/a/street_lamp_02 | CC0 | Assets/Parasite/Art/City/Props/street_lamp_02/ | Wall lanterns at entrances |
| Fire Hydrant | Gonçalo Felício | https://polyhaven.com/a/fire_hydrant | CC0 | Assets/Parasite/Art/City/Props/fire_hydrant/ | Sidewalk hydrants |
| Metal Trash Can | GurJas Studios | https://polyhaven.com/a/metal_trash_can | CC0 | Assets/Parasite/Art/City/Props/metal_trash_can/ | Litter bins |
| Exterior Aircon Unit | Monsta3D | https://polyhaven.com/a/exterior_aircon_unit | CC0 | Assets/Parasite/Art/City/Props/exterior_aircon_unit/ | Wall AC units (alley) |
| Concrete Road Barrier 02 | Amal Kumar | https://polyhaven.com/a/concrete_road_barrier_02 | CC0 | Assets/Parasite/Art/City/Props/concrete_road_barrier_02/ | Road closures |
| Utility Box 01 / 02 | James Ray Cock | https://polyhaven.com/a/utility_box_01 , https://polyhaven.com/a/utility_box_02 | CC0 | Assets/Parasite/Art/City/Props/utility_box_0*/ | Street cabinets |
| Roller Shutter Door / Window 01 | MP | https://polyhaven.com/a/rollershutter_door , https://polyhaven.com/a/rollershutter_window_01 | CC0 | Assets/Parasite/Art/City/Props/rollershutter_*/ | Alley service doors |
| Security Camera 01, Security Light | Alexander Otterbeck, Yann Kervran; Maximilian Schuster | https://polyhaven.com/a/security_camera_01 , https://polyhaven.com/a/security_light | CC0 | Assets/Parasite/Art/City/Props/security_*/ | Alley / campus wall fixtures |
| Covered Car | MP | https://polyhaven.com/a/covered_car | CC0 | Assets/Parasite/Art/City/Props/covered_car/ | Parked covered car |
| Wooden Crate 01, Plastic Crate 02, Cardboard Box 01, Barrel 03, Old Tyre, Trashbag | James Ray Cock; Fabi_G; Rahul Chaudhary; Serhii Khromov; MP; Benny Weimer | https://polyhaven.com/a/wooden_crate_01 (…/plastic_crate_02, cardboard_box_01, barrel_03, old_tyre, trashbag) | CC0 | Assets/Parasite/Art/City/Props/<id>/ | Alley clutter |
| Planter Box 01, Shrub 04 | James Ray Cock; Rico Cilliers | https://polyhaven.com/a/planter_box_01 , https://polyhaven.com/a/shrub_04 | CC0 | Assets/Parasite/Art/City/Props/planter_box_01/, shrub_04/ | Planters, tree-pit shrubs |
| Industrial Wall Lamp | Kuutti Siitonen | https://polyhaven.com/a/industrial_wall_lamp | CC0 | Assets/Parasite/Art/City/Props/industrial_wall_lamp/ | Available wall-light model |
| Modular Fire Escape | Juniix | https://polyhaven.com/a/modular_fire_escape | CC0 | Assets/Parasite/Art/City/Props/modular_fire_escape/ | Available fire-escape kit (placed escapes are procedural) |
| Modular Metal Gutter | Maxim Domnin | https://polyhaven.com/a/modular_metal_gutter | CC0 | Assets/Parasite/Art/City/Props/modular_metal_gutter/ | Available gutter kit (placed pipes are procedural) |
| Modular Street Seating | Stuart Attenborrow | https://polyhaven.com/a/modular_street_seating | CC0 | Assets/Parasite/Art/City/Props/modular_street_seating/ | Available seating kit (placed benches are procedural) |
| Water Manhole Cover | Raunox | https://polyhaven.com/a/water_manhole_cover | CC0 | Assets/Parasite/Art/City/Props/water_manhole_cover/ | Available manhole model (road manholes use decals) |

Model author names above were checked against the Poly Haven `info/<asset_id>` API on 21 November 2026.
Repeated street fixtures merge selected generated mesh LODs (roughly 0.5–2K triangles per instance); each scene build
logs per-model instance/triangle totals and its complete geometry budget before environment baking.

Generated in-project by `Tools/gen_city_art.py` (no third-party source): `Signs/signs_*.png` (shop sign atlas, text set in
Rajdhani Bold (OFL, above) and Noto Sans/Serif Display (SIL OFL 1.1, system fonts, rendered into the texture only)),
`Shop/Interior_*.jpg` (shop interiors), `Decals/Grime_albedo.png`, `Decals/Puddle_albedo.png`. Procedural vehicles
(`Editor/World/Vehicles.cs`) and trees are built from code.

Generated in-project (no third-party source): `Assets/Parasite/Art/City/Generated/skyline_*.png` (procedural window grid).

## Audio

Kenney clips (CC0 1.0) were extracted from the two packs below and renamed into `Assets/Parasite/Resources/Audio/`.
All other sounds and the city ambience are synthesized at runtime by `SfxSynth.cs` (original, no third-party source).

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 | Resources/Audio/ui_click, denied, terminal_beep, alert_curious, switch .ogg | UI clicks, access denied, terminal, alerts, amoeba switch |
| Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 | Resources/Audio/footstep_*, land_*, possess_hit_*, capture_clank_*, door_clunk, contained_zap .ogg | Footsteps, landing, possession impact, capture clank, doors, containment |

Licence text: `Assets/Parasite/Audio/KENNEY_LICENSE.txt`.

## Character Models, Clothing & Animation

### Character Models & Clothing (MPFB / MakeHuman Community)
Character base models and clothing assets generated with MPFB 2.0.18 (MakeHuman Plugin for Blender, GPL-3.0) and CC0 / CC-BY MakeHuman Community asset packs (Elvaerwyn, Mindfront, MargaretToigo, punkduck, makehuman_system, etc.).
See full itemized attribution table in [Art/Characters/CREDITS.md](file:///home/mevlec/Data/Parasite/Assets/Parasite/Art/Characters/CREDITS.md).

### Humanoid Animations
- **Universal Animation Library [Standard] & UAL 2** — Quaternius (with Gonzalo Furnier), CC0 1.0. https://quaternius.itch.io/universal-animation-library
- **CMU Graphics Lab Motion Capture Database** (subject 07 trial 12 brisk walk) — Carnegie Mellon University, http://mocap.cs.cmu.edu/ (free for commercial and non-commercial products).
See full animation details in [Art/Characters/Animations/CREDITS.md](file:///home/mevlec/Data/Parasite/Assets/Parasite/Art/Characters/Animations/CREDITS.md).

### Foliage & Skybox Upgrade
| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Belfast Sunset (Pure Sky) 4k HDR | Greg Zaal, Dimitrios Savva, Jarod Guest | https://polyhaven.com/a/belfast_sunset_puresky | CC0 | Assets/Parasite/Art/City/sky/belfast_sunset_puresky_4k.hdr | Dusk skybox + ambient/reflection bake |
| Bark Platanus 1k | Dimitrios Savva | https://polyhaven.com/a/bark_platanus | CC0 | Art/City/Foliage/Trees/BarkPlane_* | Plane tree bark |
| Bark Brown 02 1k | Rob Tuytel | https://polyhaven.com/a/bark_brown_02 | CC0 | Art/City/Foliage/Trees/BarkLinden_* | Linden bark |
| Japanese Zelkova Bark 1k | Charlotte Baglioni | https://polyhaven.com/a/japanese_zelkova_bark | CC0 | Art/City/Foliage/Trees/BarkMaple_* | Maple bark |
| LeafSet 024 / 014 / 007 / 027 (2K) | ambientCG (Lennart Demes) | https://ambientcg.com/ | CC0 | Art/City/Foliage/Trees/TreeLeaves*_albedo/_normal.png | Leaf cluster card atlases |

## Fonts

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Rajdhani (Medium, SemiBold, Bold) | Indian Type Foundry | https://github.com/google/fonts/tree/main/ofl/rajdhani | SIL OFL 1.1 | Assets/Parasite/Resources/UI/Rajdhani-*.ttf (+ OFL-Rajdhani.txt) | All UI text |

## Unity packages (Unity Companion License / Unity terms, free)

Universal RP 17.3, Shader Graph 17.3, Cinemachine 3.1.7, Input System 1.20.1, AI Navigation 2.0.16, uGUI 2.0, Test Framework 1.6 — installed through the Unity Package Manager, not redistributed as raw packs.

## Not used (blocked)

- Unity Starter Assets Third Person controller, Demo City, Bubba/Scavenger (Asset Store) and Mixamo characters: require an
  Asset Store / Adobe login and manual "Add to My Assets", unavailable to the build agent. Replaced by original
  procedural code: character controller, humanoid rig + animation, and a scripted city.
