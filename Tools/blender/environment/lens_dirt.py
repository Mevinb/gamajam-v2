#!/usr/bin/env python3.12
"""Procedural lens-dirt texture for URP Bloom (soft smudges + faint bokeh rings, mostly black). Deterministic.
usage: lens_dirt.py <out png>"""
import sys
import numpy as np
from PIL import Image, ImageFilter
W, H = 1024, 576
rng = np.random.default_rng(7)
img = np.zeros((H, W), np.float32)
yy, xx = np.mgrid[0:H, 0:W]
for _ in range(70):                                   # bokeh discs / rings
    cx, cy, r = rng.uniform(0, W), rng.uniform(0, H), rng.uniform(6, 34)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    disc = np.clip(1 - d / r, 0, 1) ** 0.35 * rng.uniform(0.08, 0.28)
    ring = np.exp(-((d - r * 0.92) / (r * 0.08)) ** 2) * rng.uniform(0.05, 0.2)
    img = np.maximum(img, disc + ring)
smudge = rng.normal(0, 1, (H // 16, W // 16)).astype(np.float32)
sm = Image.fromarray(((smudge - smudge.min()) / (smudge.max() - smudge.min()) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
sm = np.asarray(sm.filter(ImageFilter.GaussianBlur(18)), np.float32) / 255.0
img += np.clip(sm - 0.55, 0, 1) * 0.5
edge = np.clip((np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) - 0.35) * 1.2, 0, 1)
img *= 0.35 + 0.65 * edge                             # cleaner centre, dirtier corners
img = np.clip(img, 0, 1)
rgb = np.stack([img * 1.0, img * 0.97, img * 0.92], -1)
Image.fromarray((rgb * 255).astype(np.uint8), "RGB").save(sys.argv[1], optimize=True)
print("lens dirt", sys.argv[1])
