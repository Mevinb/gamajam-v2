#!/usr/bin/env python3.12
"""Assemble rendered leaf-cluster cells (2x2) into card atlases: <Name>_albedo.png (sRGB RGBA, colour dilated into
transparent texels so mips don't darken the edges) and <Name>_normal.png (tangent-space, OpenGL Y+).
Cell i occupies UV u in [(i%2)/2, (i%2)/2+.5], v in [(i//2)/2, (i//2)/2+.5] (v up).
usage: post_atlas.py <clusters dir> <out dir>"""
import sys, os, glob
import numpy as np
from PIL import Image
from scipy import ndimage

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)

def srgb(x):
    x = np.clip(x, 0, 1)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)

def dilate(rgb, mask):
    # nearest opaque texel colour for every transparent texel
    idx = ndimage.distance_transform_edt(~mask, return_distances=False, return_indices=True)
    return rgb[idx[0], idx[1]]

names = sorted({os.path.basename(f).split("_")[0] for f in glob.glob(os.path.join(src, "*_albedo.npy"))})
for n in names:
    cells = []
    for i in range(4):
        a = np.load(os.path.join(src, f"{n}_{i}_albedo.npy")); nm = np.load(os.path.join(src, f"{n}_{i}_normal.npy"))
        alpha = a[..., 3]
        mask = alpha > 0.5
        col = srgb(a[..., :3] / np.maximum(alpha[..., None], 1e-4))       # un-premultiply film alpha
        col = dilate(col, mask)
        nrm = nm[..., :3] / np.maximum(nm[..., 3:4], 1e-4)
        nrm = dilate(nrm, mask)
        # soft alpha: keep the antialiased coverage (importer preserves coverage across mips)
        cells.append((col, nrm, alpha))
    h, w = cells[0][2].shape
    A = np.zeros((h * 2, w * 2, 4), np.float32); N = np.zeros((h * 2, w * 2, 3), np.float32)
    for i, (col, nrm, alpha) in enumerate(cells):
        cx, cy = i % 2, i // 2                     # cy counts from the bottom (v up)
        y0 = (1 - cy) * h; x0 = cx * w
        A[y0:y0 + h, x0:x0 + w, :3] = col; A[y0:y0 + h, x0:x0 + w, 3] = alpha
        N[y0:y0 + h, x0:x0 + w] = nrm
    Image.fromarray((np.clip(A, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, f"Leaves{n}_albedo.png"), optimize=True)
    Image.fromarray((np.clip(N, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, f"Leaves{n}_normal.png"), optimize=True)
    print("atlas", n, A.shape)
