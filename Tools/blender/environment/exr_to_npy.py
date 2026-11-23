# Blender helper: convert every *.exr in a folder to float32 .npy (H x W x 4, top row first) for numpy/PIL post-processing.
# usage: blender -b --python exr_to_npy.py -- <dir>
import bpy, sys, os, glob
import numpy as np
d = sys.argv[sys.argv.index("--") + 1]
for f in sorted(glob.glob(os.path.join(d, "*.exr"))):
    img = bpy.data.images.load(f)
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    np.save(f[:-4] + ".npy", a.reshape(h, w, 4)[::-1].copy())
    bpy.data.images.remove(img)
    print("[npy]", f)
