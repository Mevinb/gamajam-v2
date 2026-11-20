#!/usr/bin/env python3
"""Fetch Poly Haven (CC0) models: FBX 1k + the glTF texture set (diff/arm/nor_gl jpg, alpha/opacity png).
usage: Tools/fetch_polyhaven.py <asset_id> ...   -> Assets/Parasite/Art/City/Props/<asset_id>/"""
import json, os, sys, urllib.request
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Parasite", "Art", "City", "Props")
UA = {"User-Agent": "Mozilla/5.0 (Parasite asset fetch)"}
def get(url, timeout=300):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout).read()
for aid in sys.argv[1:]:
    dst = os.path.join(ROOT, aid); os.makedirs(dst, exist_ok=True)
    try:
        d = json.loads(get(f"https://api.polyhaven.com/files/{aid}", 60))
        fbx = d["fbx"]["1k"]["fbx"]
        files = {f"{aid}.fbx": fbx["url"]}
        for k, v in d["gltf"]["1k"]["gltf"]["include"].items():
            n = os.path.basename(k)
            if n.endswith((".jpg", ".png")) and not n.endswith(".bin"): files[n] = v["url"]
        # alpha/opacity maps are not always part of the glTF set
        for key in ("opacity", "Opacity", "alpha", "Alpha"):
            if key in d and "1k" in d[key]:
                for fmt, v in d[key]["1k"].items():
                    if fmt in ("png", "jpg"): files[os.path.basename(v["url"])] = v["url"]
        tot = 0
        for n, u in files.items():
            p = os.path.join(dst, n)
            if not os.path.exists(p):
                b = get(u); open(p, "wb").write(b); tot += len(b)
        print(aid, f"{tot/1e6:.1f} MB", " ".join(sorted(files)))
    except Exception as e:
        print(aid, "FAILED", e)
