#!/usr/bin/env python3.12
"""Find the individual leaves in ambientCG LeafSet opacity atlases (connected components) -> JSON of pixel rects.
usage: leaf_rects.py <src dir> <out json>"""
import json, sys
import numpy as np
from PIL import Image
from scipy import ndimage

SETS = ["LeafSet024", "LeafSet014", "LeafSet007", "LeafSet027"]
src, out = sys.argv[1], sys.argv[2]
res = {}
for s in SETS:
    a = np.array(Image.open(f"{src}/{s}_Opacity.png").convert("L")) > 128
    lab, n = ndimage.label(a)
    sizes = ndimage.sum(a, lab, range(1, n + 1))
    rects = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        if sizes[i] < 2000: continue
        y0, y1, x0, x1 = sl[0].start, sl[0].stop, sl[1].start, sl[1].stop
        rects.append([int(x0), int(y0), int(x1), int(y1)])   # image pixels, y down
    res[s] = {"size": [int(a.shape[1]), int(a.shape[0])], "rects": rects}
json.dump(res, open(out, "w"), indent=1)
print({k: len(v["rects"]) for k, v in res.items()})
