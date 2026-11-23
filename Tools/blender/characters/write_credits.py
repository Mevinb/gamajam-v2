"""Write CREDITS.md for the generated characters from the per-character metadata + asset_licences.json."""
import json, os, sys, glob

here = os.path.dirname(os.path.abspath(__file__))
work, models = sys.argv[1], sys.argv[2]
lic = json.load(open(os.path.join(here, "asset_licences.json")))
KIND = {"skins": ("skins", "skin"), "eyebrows": ("eyebrows",), "eyelashes": ("eyelashes",), "hair": ("hair", "clothes"),
        "clothes": ("clothes",)}
used = {}
for f in sorted(glob.glob(os.path.join(models, "*.json"))):
    m = json.load(open(f))
    for a in m.get("assets", []):
        key = None
        for t in KIND.get(a["kind"], (a["kind"],)):
            if t + "/" + a["name"] in lic:
                key = t + "/" + a["name"]
                break
        info = lic.get(key, {"author": "makehuman_system", "license": "CC0", "pack": "makehuman_system_assets"})
        used.setdefault((a["kind"], a["name"]), {"info": info, "chars": set()})["chars"].add(m["id"])
print("# Character art credits\n")
print("Generated with MPFB 2.0.18 (MakeHuman Plugin for Blender, GPL-3.0, Joel Palmius / MakeHuman Community,")
print("https://extensions.blender.org/add-ons/mpfb/). The generated humans (base mesh, targets, rig weights) are CC0.")
print("Assets from the MakeHuman Community asset packs (https://static.makehumancommunity.org/assets/assetpacks/):\n")
print("| Asset | Kind | Author | Licence | Pack | Used by |")
print("|---|---|---|---|---|---|")
for (kind, name), u in sorted(used.items()):
    i = u["info"]
    print(f"| {name} | {kind} | {i['author']} | {i['license']} | {i['pack']} | {', '.join(sorted(u['chars']))} |")
print("\nFabric detail textures for procedural garments: Poly Haven (CC0), Assets/Parasite/Resources/Textures/Clothes.")
print("CC-BY assets require attribution: keep this file with the game's credits.")
