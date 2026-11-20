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

Generated in-project (no third-party source): `Assets/Parasite/Art/City/Generated/skyline_*.png` (procedural window grid).

## Audio

Kenney clips (CC0 1.0) were extracted from the two packs below and renamed into `Assets/Parasite/Resources/Audio/`.
All other sounds and the city ambience are synthesized at runtime by `SfxSynth.cs` (original, no third-party source).

| Asset | Author(s) | Source URL | License | Local path | Used for |
|---|---|---|---|---|---|
| Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 | Resources/Audio/ui_click, denied, terminal_beep, alert_curious, switch .ogg | UI clicks, access denied, terminal, alerts, amoeba switch |
| Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 | Resources/Audio/footstep_*, land_*, possess_hit_*, capture_clank_*, door_clunk, contained_zap .ogg | Footsteps, landing, possession impact, capture clank, doors, containment |

Licence text: `Assets/Parasite/Audio/KENNEY_LICENSE.txt`.

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
