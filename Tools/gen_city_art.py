#!/usr/bin/env python3
"""
PARASITE city art generator (run with a Python that has numpy + Pillow, e.g. ~/.local/bin/python3.12):
    python3.12 Tools/gen_city_art.py [facades|skyline|ground|signs|foliage|decals|shop|all]

Inputs: raw CC0 downloads (ambientCG via Tools/fetch_acg.sh, Poly Haven via Tools/fetch_polyhaven.py) in RAW
(default ~/Data/tmp_dl/raw, override with PARASITE_RAW). Outputs: Unity-ready textures in Assets/Parasite/Art/City/*.
All outputs are derived from CC0 sources (see Docs/THIRD_PARTY_ASSETS.md) or are generated here.

Texture conventions (consumed by Assets/Parasite/Editor/World/CityAssets.cs):
  *_albedo.jpg (sRGB), *_normal.jpg (OpenGL), *_mask.png (R metallic, G occlusion, B 0, A smoothness),
  *_height.png (parallax, grey), *_emission.jpg (sRGB, night lighting; material intensity <= 1.5).
"""
import json, math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.abspath(os.path.join(HERE, ".."))
RAW = os.environ.get("PARASITE_RAW", os.path.expanduser("~/Data/tmp_dl/raw"))
ACG = os.path.join(RAW, "acg")
OUT = os.path.join(PROJ, "Assets", "Parasite", "Art", "City")
FONT_DIR = os.path.join(PROJ, "Assets", "Parasite", "Resources", "UI")


def load(path, mode="RGB", size=None):
    im = Image.open(path).convert(mode)
    if size and im.size != (size, size): im = im.resize((size, size), Image.LANCZOS)
    return np.asarray(im, np.float32) / 255.0


def save(arr, path, q=90):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    a = np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8)
    im = Image.fromarray(a)
    if path.endswith(".jpg"): im.save(path, quality=q, optimize=True)
    else: im.save(path, optimize=True)
    print("  wrote", os.path.relpath(path, PROJ), im.size, f"{os.path.getsize(path)/1e6:.1f} MB")


def acg(id_, kind):
    for ext in (".jpg", ".png"):
        p = os.path.join(ACG, id_, f"{id_}_{kind}{ext}")
        if os.path.exists(p): return p
    return None


def mask_from(rough, metal=None, ao=None, smooth_scale=1.0, smooth_bias=0.0):
    h, w = rough.shape[:2]
    m = np.zeros((h, w, 4), np.float32)
    m[..., 0] = 0 if metal is None else metal
    m[..., 1] = 1 if ao is None else ao
    m[..., 3] = np.clip((1.0 - rough) * smooth_scale + smooth_bias, 0, 1)
    return m


def blur(a, r):
    """Approximate gaussian (3 box passes) with wrap-around, so tiling textures stay seamless."""
    a = np.asarray(a, np.float32)
    k = max(1, int(round(r)))
    for axis in (0, 1):
        for _ in range(3):
            acc = np.zeros_like(a)
            for o in range(-k, k + 1): acc += np.roll(a, o, axis)
            a = acc / (2 * k + 1)
    return a


def value_noise(size, cells, seed, octaves=4):
    rng = np.random.default_rng(seed)
    out = np.zeros((size, size), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        n = cells * (2 ** o)
        g = rng.random((n, n)).astype(np.float32)
        im = Image.fromarray(g, "F").resize((size, size), Image.BICUBIC)
        # make it tile: wrap by blending with rolled copy
        a = np.asarray(im, np.float32)
        out += a * amp; tot += amp; amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


# ============================================================================================ facades
# ambientCG Facade018A/019A/020A share one grid: 6 x 6 window cells per 2048 px tile, window centres on k/6.
FACADE_BASES = {"Res": "Facade018A", "Office": "Facade019A", "Loft": "Facade020A"}
# style name, base, wall paint (None = original), paint strength, emission seed, lit fraction
FACADE_STYLES = [
    ("BrickRed", "Res", None, 0.0, 11, 0.34),
    ("BrickCream", "Res", (0.80, 0.71, 0.56), 0.85, 12, 0.30),
    ("BrickWhite", "Res", (0.80, 0.80, 0.77), 0.85, 13, 0.36),
    ("BrickOchre", "Res", (0.72, 0.52, 0.30), 0.75, 14, 0.28),
    ("StoneGrey", "Office", None, 0.0, 15, 0.40),
    ("StoneSand", "Office", (0.78, 0.69, 0.55), 0.6, 16, 0.33),
    ("LoftBrown", "Loft", None, 0.0, 17, 0.35),
    ("LoftCharcoal", "Loft", (0.30, 0.30, 0.31), 0.7, 18, 0.42),
]
WARM = [(1.0, 0.70, 0.42), (1.0, 0.62, 0.34), (1.0, 0.80, 0.58), (0.98, 0.86, 0.70), (1.0, 0.56, 0.28)]
COOL = [(0.62, 0.76, 1.0), (0.75, 0.85, 1.0)]


def facades():
    print("facades")
    S = 2048
    leak = load(acg("Leaking012A", "Color"), "L", S) * load(acg("Leaking017B", "Color"), "L", S)
    for key, id_ in FACADE_BASES.items():
        d = os.path.join(OUT, "Facades")
        rough = load(acg(id_, "Roughness"), "L", S)
        metal = load(acg(id_, "Metalness"), "L", S)
        ao = load(acg(id_, "AmbientOcclusion"), "L", S)
        glass = (rough < 0.12) & (metal > 0.5)
        frame = (metal > 0.5) & ~glass
        # base maps shared by every style of this base
        nor = Image.open(acg(id_, "NormalGL")).convert("RGB")
        os.makedirs(d, exist_ok=True)
        nor.save(os.path.join(d, f"{key}_normal.jpg"), quality=88, optimize=True)
        print("  wrote", f"Facades/{key}_normal.jpg")
        met = np.where(glass, 0.9, np.where(frame, 0.35, 0.0)).astype(np.float32)
        sm = np.where(glass, 0.93, np.clip((1 - rough) * 0.75, 0, 0.6)).astype(np.float32)
        m = np.zeros((S, S, 4), np.float32); m[..., 0] = met; m[..., 1] = 0.35 + 0.65 * ao; m[..., 3] = sm
        save(m[::2, ::2], os.path.join(d, f"{key}_mask.png"))
        h = load(acg(id_, "Displacement"), "L", 1024)
        save(np.stack([h] * 3, -1), os.path.join(d, f"{key}_height.png"))

        col = load(acg(id_, "Color"), "RGB", S)
        wall = (metal < 0.5)
        lum = col @ np.array([0.299, 0.587, 0.114], np.float32)
        for name, base, paint, k, seed, lit in FACADE_STYLES:
            if base != key: continue
            a = col.copy()
            if paint is not None:
                wl = lum[wall].mean()
                rel = np.clip(lum / wl, 0, 2.2)[..., None]
                painted = np.array(paint, np.float32) * (0.55 + 0.45 * rel)
                a = np.where(wall[..., None], a * (1 - k) + painted * k, a)
            # weathering: leak streaks + large scale dirt on the wall only
            dirt = value_noise(S, 3, seed, 4)
            wmul = (0.75 + 0.25 * leak) * (0.86 + 0.14 * dirt)
            a = np.where(wall[..., None], a * wmul[..., None], a)
            # glass: darken the pale "curtain" panes a little so unlit windows never read as white at night
            g3 = glass[..., None]
            a = np.where(g3, a * 0.55 + np.array([0.02, 0.025, 0.035], np.float32), a)
            save(a, os.path.join(d, f"{name}_albedo.jpg"), 88)
            save(window_emission(glass, col, seed, lit, 6, 6, S)[::2, ::2], os.path.join(d, f"{name}_emission.jpg"), 90)


def window_emission(glass, col, seed, lit, nx, ny, S):
    rng = random.Random(seed)
    e = np.zeros((S, S, 3), np.float32)
    cw, ch = S / nx, S / ny
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx = np.floor(xx / cw + 0.5).astype(int) % nx  # window centres on multiples of the cell size
    cy = np.floor(yy / ch + 0.5).astype(int) % ny
    fx = (xx / cw + 0.5) % 1.0  # 0..1 inside the cell (window centred at 0.5)
    fy = (yy / ch + 0.5) % 1.0
    for j in range(ny):
        for i in range(nx):
            if rng.random() > lit: continue
            c = np.array(rng.choice(COOL) if rng.random() < 0.12 else rng.choice(WARM), np.float32)
            b = rng.uniform(0.35, 0.95)
            sel = glass & (cx == i) & (cy == j)
            # ceiling-lit room: brighter near the top, curtain on one side sometimes
            grad = 0.65 + 0.35 * (1 - fy[sel])
            if rng.random() < 0.4:
                side = fx[sel] < 0.5 if rng.random() < 0.5 else fx[sel] >= 0.5
                grad = np.where(side, grad * 0.35, grad)
            e[sel] = c * (b * grad)[:, None]
    return blur(e, 1.2)


# ============================================================================================ skyline
SKY = [  # id, out name, has emission map
    ("Facade012", "TowerDense"), ("Facade015", "TowerGrid"), ("Facade016", "TowerBrown"),
    ("Facade017", "TowerPiers"), ("Facade009", "TowerGlassNight"), ("Facade006", "TowerGlass"),
]


def skyline():
    print("skyline")
    d = os.path.join(OUT, "Skyline")
    for id_, name in SKY:
        col = load(acg(id_, "Color"), "RGB", 1024)
        rough = load(acg(id_, "Roughness"), "L", 1024)
        metal = load(acg(id_, "Metalness"), "L", 1024) if acg(id_, "Metalness") else None
        em = acg(id_, "Emission")
        if em: e = load(em, "RGB", 1024)
        else:  # derive: bright window pixels in the night photo
            l = col.max(2)
            e = np.where((l > 0.55)[..., None], col, 0) * 0.8
        if name == "TowerGlass":  # daytime glass: add a sparse warm lit-office pattern for the evening
            gl = rough < 0.2
            e = window_emission(gl, col, 77, 0.22, 16, 16, 1024) * 0.8
            col = col * 0.7
        save(col, os.path.join(d, f"{name}_albedo.jpg"), 88)
        save(e, os.path.join(d, f"{name}_emission.jpg"), 88)
        Image.open(acg(id_, "NormalGL")).convert("RGB").resize((1024, 1024)).save(os.path.join(d, f"{name}_normal.jpg"), quality=85)
        m = mask_from(rough, None if metal is None else metal * 0.6, None, 0.9)
        save(m, os.path.join(d, f"{name}_mask.png"))


# ============================================================================================ ground
def ground():
    print("ground")
    d = os.path.join(OUT, "Ground")
    for id_, name, size, sm, wet in [("Asphalt026C", "Road", 2048, 0.7, True), ("PavingStones070", "Cobble", 2048, 0.8, False),
                                     ("Gravel040", "Gravel", 1024, 0.5, False)]:
        col = load(acg(id_, "Color"), "RGB", size)
        rough = load(acg(id_, "Roughness"), "L", size)
        ao = load(acg(id_, "AmbientOcclusion"), "L", size) if acg(id_, "AmbientOcclusion") else None
        m = mask_from(rough, None, ao, sm)
        if wet:  # damp evening asphalt: patchy extra smoothness, darker where wet
            n = value_noise(size, 4, 5, 4)
            w = np.clip((n - 0.55) * 4, 0, 1)
            m[..., 3] = np.clip(m[..., 3] + w * 0.45, 0, 0.92)
            col = col * (1 - 0.3 * w[..., None])
        save(col, os.path.join(d, f"{name}_albedo.jpg"), 88)
        Image.open(acg(id_, "NormalGL")).convert("RGB").resize((size, size)).save(os.path.join(d, f"{name}_normal.jpg"), quality=86)
        save(m if size <= 1024 else m[::2, ::2], os.path.join(d, f"{name}_mask.png"))


# ============================================================================================ signs
SIGNS = [
    # text, style, bg, fg
    ("24/7 MART", "box", (0.08, 0.45, 0.22), (1, 1, 1)), ("KITO RAMEN", "channel", (0.08, 0.05, 0.05), (1, 0.35, 0.25)),
    ("LUCKY LAUNDRY", "box", (0.12, 0.3, 0.62), (1, 1, 1)), ("PAWN & GOLD", "channel", (0.06, 0.05, 0.04), (1.0, 0.78, 0.3)),
    ("HALCYON BOOKS", "serif", (0.1, 0.22, 0.16), (0.95, 0.9, 0.75)), ("NOVA PHARMACY", "box", (0.95, 0.96, 0.95), (0.05, 0.5, 0.3)),
    ("METRO DINER", "neon", (0.05, 0.04, 0.06), (1.0, 0.3, 0.4)), ("HOTEL ARDEN", "serif", (0.1, 0.06, 0.12), (1.0, 0.85, 0.6)),
    ("CAFE LUMEN", "serif", (0.12, 0.08, 0.05), (1.0, 0.9, 0.7)), ("FLORA", "neon", (0.05, 0.06, 0.05), (1.0, 0.45, 0.8)),
    ("PIXEL ARCADE", "neon", (0.04, 0.03, 0.08), (0.3, 0.9, 1.0)), ("QUICK PRINT", "box", (0.95, 0.75, 0.1), (0.08, 0.08, 0.1)),
    ("OSAKA SUSHI", "box", (0.75, 0.1, 0.1), (1, 1, 1)), ("CITY BANK", "channel", (0.06, 0.08, 0.14), (0.75, 0.9, 1.0)),
    ("MIDNIGHT BAR", "neon", (0.04, 0.03, 0.06), (0.75, 0.4, 1.0)), ("NIGHT OWL DELI", "box", (0.1, 0.12, 0.14), (1.0, 0.82, 0.35)),
    ("BARBER", "serif", (0.9, 0.9, 0.88), (0.5, 0.08, 0.08)), ("TACO LOCO", "box", (0.95, 0.45, 0.1), (1, 1, 1)),
    ("PHONE FIX", "box", (0.1, 0.35, 0.75), (1, 1, 1)), ("BAKERY", "serif", (0.35, 0.2, 0.1), (1.0, 0.9, 0.7)),
    ("NOODLE BAR", "neon", (0.05, 0.04, 0.04), (1.0, 0.75, 0.3)), ("LIQUOR", "channel", (0.07, 0.05, 0.05), (1.0, 0.3, 0.25)),
    ("TATTOO", "neon", (0.03, 0.03, 0.03), (0.4, 1.0, 0.6)), ("DRY CLEAN", "box", (0.9, 0.92, 0.95), (0.1, 0.25, 0.6)),
    ("VALE BIOTECH", "channel", (0.04, 0.06, 0.06), (0.45, 1.0, 0.62)), ("MAIN GATE — SECURITY", "box", (0.08, 0.09, 0.1), (1, 1, 1)),
    ("STAFF ENTRANCE", "box", (0.08, 0.09, 0.1), (1, 1, 1)), ("SERVICE TUNNEL", "box", (0.85, 0.62, 0.1), (0.05, 0.05, 0.05)),
    ("ROAD CLOSED", "box", (0.85, 0.1, 0.08), (1, 1, 1)), ("BUS 42", "box", (0.1, 0.3, 0.6), (1, 1, 1)),
    ("NEWS", "box", (0.85, 0.1, 0.08), (1, 1, 1)), ("STAFF PARKING", "box", (0.1, 0.3, 0.6), (1, 1, 1)),
    ("VALE AVENUE", "street", (0.05, 0.3, 0.15), (1, 1, 1)), ("MARKET ST", "street", (0.05, 0.3, 0.15), (1, 1, 1)),
    ("CAMPUS RD", "street", (0.05, 0.3, 0.15), (1, 1, 1)), ("VALE BIOTECH  ^", "street", (0.05, 0.3, 0.15), (1, 1, 1)),
    ("VALE BIOTECH  ^  NORTH", "street", (0.05, 0.3, 0.15), (1, 1, 1)), ("DR. E. VALE", "channel", (0.04, 0.06, 0.06), (0.45, 1.0, 0.62)),
    ("OPEN", "neon", (0.02, 0.02, 0.02), (1.0, 0.2, 0.25)), ("HOTEL", "neon", (0.02, 0.02, 0.02), (1.0, 0.6, 0.3)),
]
SIGN_W, SIGN_H, SIGN_COLS = 1024, 128, 2


def font(kind, size):
    f = {"serif": "/usr/share/fonts/truetype/noto/NotoSerifDisplay-Bold.ttf",
         "neon": "/usr/share/fonts/truetype/noto/NotoSans-BoldItalic.ttf"}.get(kind, os.path.join(FONT_DIR, "Rajdhani-Bold.ttf"))
    return ImageFont.truetype(f, size)


def signs():
    print("signs")
    rows = math.ceil(len(SIGNS) / SIGN_COLS)
    H = 1 << math.ceil(math.log2(rows * SIGN_H))
    alb = Image.new("RGB", (SIGN_W * SIGN_COLS, H), (20, 20, 22))
    emi = Image.new("RGB", (SIGN_W * SIGN_COLS, H), (0, 0, 0))
    table = []
    for idx, (text, style, bg, fg) in enumerate(SIGNS):
        cx, cy = (idx % SIGN_COLS) * SIGN_W, (idx // SIGN_COLS) * SIGN_H
        A = Image.new("RGB", (SIGN_W, SIGN_H)); E = Image.new("RGB", (SIGN_W, SIGN_H))
        da, de = ImageDraw.Draw(A), ImageDraw.Draw(E)
        bgc = tuple(int(c * 255) for c in bg); fgc = tuple(int(c * 255) for c in fg)
        size = 96 if style != "street" else 84
        f = font(style, size)
        while f.getbbox(text)[2] - f.getbbox(text)[0] > SIGN_W - 90 and size > 30:
            size -= 4; f = font(style, size)
        bb = f.getbbox(text); tw, th = bb[2] - bb[0], bb[3] - bb[1]
        tx, ty = (SIGN_W - tw) // 2 - bb[0], (SIGN_H - th) // 2 - bb[1]
        da.rectangle([0, 0, SIGN_W, SIGN_H], fill=bgc)
        if style == "box":  # back-lit lightbox: whole face glows, text darker or lighter
            da.rectangle([6, 6, SIGN_W - 7, SIGN_H - 7], outline=tuple(int(c * 200) for c in bg), width=4)
            da.text((tx, ty), text, font=f, fill=fgc)
            de.rectangle([10, 10, SIGN_W - 11, SIGN_H - 11], fill=tuple(int(c * 150) for c in bg))
            de.text((tx, ty), text, font=f, fill=tuple(int(c * 255 * (0.9 if sum(fg) > sum(bg) else 0.15)) for c in fg))
        elif style in ("channel", "serif"):  # illuminated letters on a dark/coloured fascia board
            da.text((tx, ty), text, font=f, fill=fgc)
            de.text((tx, ty), text, font=f, fill=tuple(int(c * 255 * 0.95) for c in fg))
        elif style == "neon":  # glowing tubes: blurred halo + bright core
            glow = Image.new("RGB", (SIGN_W, SIGN_H)); dg = ImageDraw.Draw(glow)
            dg.text((tx, ty), text, font=f, fill=fgc)
            glow = glow.filter(ImageFilter.GaussianBlur(7))
            A.paste(Image.blend(A, glow, 0.6))
            da = ImageDraw.Draw(A)
            da.text((tx, ty), text, font=f, fill=tuple(min(255, int(c * 255 * 0.6 + 100)) for c in fg))
            E.paste(glow)
            de = ImageDraw.Draw(E)
            de.text((tx, ty), text, font=f, fill=tuple(min(255, int(c * 255 + 60)) for c in fg))
        else:  # street name plate (reflective, not lit)
            da.rounded_rectangle([3, 3, SIGN_W - 4, SIGN_H - 4], 14, fill=bgc, outline=(235, 235, 235), width=6)
            da.text((tx, ty), text, font=f, fill=fgc)
        alb.paste(A, (cx, cy)); emi.paste(E, (cx, cy))
        table.append((text, cx / alb.width, 1 - (cy + SIGN_H) / H, SIGN_W / alb.width, SIGN_H / H, style, min(1.0, (tw + 70) / SIGN_W)))
    d = os.path.join(OUT, "Signs"); os.makedirs(d, exist_ok=True)
    alb.save(os.path.join(d, "signs_albedo.png"), optimize=True)
    emi.save(os.path.join(d, "signs_emission.png"), optimize=True)
    print("  wrote Signs/signs_albedo.png", alb.size)
    # C# table (UV rects, Unity v-up)
    cs = ["// <auto-generated> by Tools/gen_city_art.py signs — do not edit by hand.", "using UnityEngine;", "",
          "namespace Parasite.EditorTools", "{", "    internal static class SignAtlas", "    {",
          "        public const float Aspect = %.1ff;" % (SIGN_W / SIGN_H),
          "        /// <summary>uv = atlas cell (Unity v-up); fill = fraction of the cell width covered by the text (+ margin).</summary>",
          "        public static readonly (string text, Rect uv, string style, float fill)[] Entries =", "        {"]
    for t, x, y, w, h, s, fl in table:
        cs.append('            ("%s", new Rect(%.6ff, %.6ff, %.6ff, %.6ff), "%s", %.3ff),' % (t.replace('"', '\\"'), x, y, w, h, s, fl))
    cs += ["        };", "    }", "}", ""]
    open(os.path.join(PROJ, "Assets", "Parasite", "Editor", "World", "SignAtlas.g.cs"), "w", encoding="utf-8").write("\n".join(cs))
    print("  wrote Editor/World/SignAtlas.g.cs")


# ============================================================================================ foliage
def foliage():
    """Leaf-cluster cards (1024², RGBA alpha-cut) composed from the ambientCG LeafSet atlases, + grass tuft card."""
    print("foliage")
    d = os.path.join(OUT, "Foliage"); os.makedirs(d, exist_ok=True)
    for id_, name, grid, tint, seed in [("LeafSet024", "LeavesGreen", 3, (0.85, 0.95, 0.8), 3), ("LeafSet030", "LeavesAutumn", 4, (1.0, 0.9, 0.8), 4)]:
        col = Image.open(acg(id_, "Color")).convert("RGB"); op = Image.open(acg(id_, "Opacity")).convert("L")
        nor = Image.open(acg(id_, "NormalGL")).convert("RGB")
        W = col.width; cw = W // grid
        leaves = []
        for j in range(grid):
            for i in range(grid):
                box = (i * cw, j * cw, (i + 1) * cw, (j + 1) * cw)
                leaves.append((col.crop(box), op.crop(box), nor.crop(box)))
        S = 1024
        A = Image.new("RGBA", (S, S), (0, 0, 0, 0)); N = Image.new("RGB", (S, S), (128, 128, 255))
        rng = random.Random(seed)
        # twigs
        dt = ImageDraw.Draw(A)
        for _ in range(14):
            a = rng.uniform(0, math.tau); r = rng.uniform(220, 440)
            dt.line([(S / 2, S / 2), (S / 2 + math.cos(a) * r, S / 2 + math.sin(a) * r)], fill=(60, 45, 30, 255), width=rng.randint(3, 6))
        for n in range(1100):
            r = math.sqrt(rng.random()) * 420; a = rng.uniform(0, math.tau)
            if rng.random() < 0.35 and r > 150: continue  # ragged outline with gaps
            x, y = S / 2 + math.cos(a) * r, S / 2 + math.sin(a) * r * 0.95
            c, o, nn = rng.choice(leaves)
            sz = int(rng.uniform(0.055, 0.085) * S * (1.0 if grid == 3 else 1.25))
            ang = rng.uniform(0, 360)
            shade = rng.uniform(0.6, 1.08) * (0.75 + 0.25 * (1 - r / 400))
            c2 = Image.eval(c.resize((sz, sz)), lambda v: v).point(lambda v: min(255, int(v * shade)))
            c2 = Image.merge("RGB", [ch.point(lambda v, t=t: min(255, int(v * t))) for ch, t in zip(c2.split(), tint)])
            o2 = o.resize((sz, sz)).point(lambda v: 255 if v > 120 else 0)
            c2 = c2.rotate(ang, Image.BILINEAR, expand=True); o2 = o2.rotate(ang, Image.NEAREST, expand=True)
            n2 = nn.resize((sz, sz)).rotate(ang, Image.BILINEAR, expand=True)
            p = (int(x - c2.width / 2), int(y - c2.height / 2))
            A.paste(Image.merge("RGBA", (*c2.split(), o2)), p, o2)
            N.paste(n2, p, o2)
        # dilate colour into transparent texels (avoids dark fringes in mips)
        rgb = np.asarray(A.convert("RGB"), np.float32) / 255; al = np.asarray(A.split()[3], np.float32) / 255
        acc = rgb * al[..., None]; w = al.copy()
        for rad in (2, 6, 16):
            acc2 = blur(acc, rad); w2 = blur(w, rad)
            fill = acc2 / np.maximum(w2, 1e-4)[..., None]
            rgb = np.where(al[..., None] > 0.5, rgb, np.where(w2[..., None] > 1e-3, fill, rgb))
        out = np.concatenate([rgb, al[..., None]], -1)
        save(out, os.path.join(d, f"{name}_albedo.png"))
        N.save(os.path.join(d, f"{name}_normal.jpg"), quality=85)
    # grass tuft card from Foliage001 (blades)
    col = Image.open(acg("Foliage001", "Color")).convert("RGB"); op = Image.open(acg("Foliage001", "Opacity")).convert("L")
    rgba = Image.merge("RGBA", (*col.split(), op)).resize((512, 512))
    rgba.save(os.path.join(d, "Grass_albedo.png"), optimize=True)
    print("  wrote Foliage/Grass_albedo.png")


# ============================================================================================ decals
def decals():
    print("decals")
    d = os.path.join(OUT, "Decals"); os.makedirs(d, exist_ok=True)
    def rgba(id_, size, name, alpha_scale=1.0):
        col = load(acg(id_, "Color"), "RGB", size); op = load(acg(id_, "Opacity"), "L", size) * alpha_scale
        save(np.concatenate([col, op[..., None]], -1), os.path.join(d, f"{name}_albedo.png"))
        if acg(id_, "NormalGL"): Image.open(acg(id_, "NormalGL")).convert("RGB").resize((size, size)).save(os.path.join(d, f"{name}_normal.jpg"), quality=85)
    rgba("GraffitiSet001", 2048, "Graffiti", 0.92)
    rgba("AsphaltDamageSet001", 1024, "AsphaltDamage", 0.85)
    rgba("ManholeCover011", 1024, "Manhole")
    # grime: dark dirt gradient strip for building bases / wet stains (tileable in u)
    S = 512
    n = value_noise(S, 6, 9, 5)
    v = np.linspace(1, 0, S, dtype=np.float32)[:, None]  # top -> bottom
    a = np.clip((v ** 2.2) * (0.55 + 0.6 * n) - 0.05, 0, 0.85)
    c = np.stack([0.09 + 0.05 * n, 0.08 + 0.04 * n, 0.07 + 0.03 * n], -1)
    save(np.concatenate([c, a[..., None]], -1), os.path.join(d, "Grime_albedo.png"))
    # wet patch / puddle: soft blob alpha, used with high smoothness
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) / S - 0.5
    r = np.sqrt(xx ** 2 + yy ** 2)
    n2 = value_noise(S, 5, 21, 4)
    a = np.clip((0.42 - r + (n2 - 0.5) * 0.22) * 9, 0, 1)
    c = np.stack([np.full_like(a, 0.03), np.full_like(a, 0.033), np.full_like(a, 0.04)], -1)
    save(np.concatenate([c, a[..., None] * 0.9], -1), os.path.join(d, "Puddle_albedo.png"))


# ============================================================================================ shop interior
def shop():
    """Back-wall texture seen through shop windows: lit shelves with goods (procedural), 2048x512 = 4 interiors."""
    print("shop")
    d = os.path.join(OUT, "Shop"); os.makedirs(d, exist_ok=True)
    W, H = 2048, 512
    A = Image.new("RGB", (W, H)); E = Image.new("RGB", (W, H))
    da, de = ImageDraw.Draw(A), ImageDraw.Draw(E)
    rng = random.Random(42)
    walls = [(0.85, 0.82, 0.75), (0.75, 0.8, 0.82), (0.55, 0.42, 0.32), (0.9, 0.88, 0.85)]
    for s in range(4):
        x0 = s * 512
        wc = walls[s]
        for y in range(H):  # wall with ceiling light falloff
            k = 0.55 + 0.45 * (1 - y / H) ** 1.5
            da.line([(x0, y), (x0 + 511, y)], fill=tuple(int(c * 255 * k) for c in wc))
            de.line([(x0, y), (x0 + 511, y)], fill=tuple(int(c * 255 * k * 0.55) for c in (1.0, 0.85, 0.65)))
        # ceiling light strip
        da.rectangle([x0, 0, x0 + 511, 26], fill=(250, 245, 230)); de.rectangle([x0, 0, x0 + 511, 26], fill=(255, 240, 210))
        if s == 2:  # cafe/bar: counter + bottles
            da.rectangle([x0, 330, x0 + 511, 511], fill=(70, 45, 30)); de.rectangle([x0, 330, x0 + 511, 511], fill=(40, 25, 15))
            for sh in (120, 200):
                da.rectangle([x0 + 20, sh, x0 + 490, sh + 8], fill=(90, 70, 50))
                for bx in range(x0 + 30, x0 + 480, 18):
                    hcol = (rng.randint(40, 200), rng.randint(40, 160), rng.randint(20, 120))
                    hh = rng.randint(30, 60)
                    da.rectangle([bx, sh - hh, bx + 10, sh], fill=hcol); de.rectangle([bx, sh - hh, bx + 10, sh], fill=tuple(c // 3 for c in hcol))
            continue
        for sh in range(90, 470, 95):  # shelves with goods
            da.rectangle([x0 + 10, sh, x0 + 501, sh + 10], fill=(200, 200, 205))
            bx = x0 + 14
            while bx < x0 + 495:
                w = rng.randint(14, 42); h = rng.randint(30, 80)
                c = (rng.randint(30, 240), rng.randint(30, 240), rng.randint(30, 240))
                da.rectangle([bx, sh - h, bx + w - 3, sh], fill=c)
                de.rectangle([bx, sh - h, bx + w - 3, sh], fill=tuple(int(v * 0.45) for v in c))
                bx += w
    A = A.filter(ImageFilter.GaussianBlur(2.2)); E = E.filter(ImageFilter.GaussianBlur(4))
    # muted, slightly warm: goods behind glass should read as "lit shop", not as a colour chart
    from PIL import ImageEnhance
    A = ImageEnhance.Color(A).enhance(0.45); E = ImageEnhance.Color(E).enhance(0.35)
    A = ImageEnhance.Brightness(A).enhance(0.8)
    A.save(os.path.join(d, "Interior_albedo.jpg"), quality=88); E.save(os.path.join(d, "Interior_emission.jpg"), quality=88)
    print("  wrote Shop/Interior_*.jpg")


# ============================================================================================ props
LAMP_GLASS = {"street_lamp_01", "street_lamp_02", "security_light"}  # opacity = lamp glass -> baked warm glow, opaque


def props():
    """Poly Haven models: copy FBX + diffuse/normal, convert each glTF ARM map (AO, rough, metal) to a URP mask.
    Opacity maps: lamp glass becomes an emission map (opaque glowing panes); other cut-outs go into the diffuse alpha."""
    import shutil
    print("props")
    src = os.path.join(RAW, "Props"); dst = os.path.join(OUT, "Props")
    for aid in sorted(os.listdir(src)):
        sd = os.path.join(src, aid)
        if not os.path.isdir(sd): continue
        dd = os.path.join(dst, aid); os.makedirs(dd, exist_ok=True)
        files = sorted(f for f in os.listdir(sd) if not f.endswith(".meta"))
        for f in files:
            p = os.path.join(sd, f)
            if f.endswith("_arm_1k.jpg"):
                arm = load(p, "RGB", 512)
                m = np.zeros((512, 512, 4), np.float32)
                m[..., 0] = arm[..., 2]; m[..., 1] = arm[..., 0]; m[..., 3] = 1 - arm[..., 1]
                save(m, os.path.join(dd, f.replace("_arm_1k.jpg", "_mask.png")))
            elif f.endswith("_diff_1k.jpg"):
                pre = f[:-len("_diff_1k.jpg")]
                op = next((os.path.join(sd, pre + s_) for s_ in ("_opacity_1k.png", "_alpha_1k.png") if os.path.exists(os.path.join(sd, pre + s_))), None)
                if op is None and aid in ("shrub_04", "plastic_crate_02"):
                    op = next((os.path.join(sd, g) for g in files if g.endswith(("_alpha_1k.png", "_opacity_1k.png"))), None)
                if op and aid in LAMP_GLASS:
                    col = load(p, "RGB"); o = load(op, "L", col.shape[0])
                    glow = np.clip((1 - o) * 1.4, 0, 1)[..., None] * np.array([1.0, 0.78, 0.5], np.float32)
                    save(glow, os.path.join(dd, pre + "_emissive_1k.jpg"))
                    shutil.copyfile(p, os.path.join(dd, f))
                elif op:
                    col = load(p, "RGB"); o = load(op, "L", col.shape[0])
                    save(np.concatenate([col, o[..., None]], -1), os.path.join(dd, pre + "_diff_1k.png"))
                    jp = os.path.join(dd, f)
                    if os.path.exists(jp): os.remove(jp)
                else:
                    shutil.copyfile(p, os.path.join(dd, f))
            elif f.endswith((".fbx", "_nor_gl_1k.jpg", "_emissive_1k.jpg")):
                shutil.copyfile(p, os.path.join(dd, f))
        for f in os.listdir(dd):  # never ship stray opacity maps (merged above)
            if f.endswith(("_opacity_1k.png", "_alpha_1k.png")): os.remove(os.path.join(dd, f))


if __name__ == "__main__":
    what = sys.argv[1:] or ["all"]
    steps = {"facades": facades, "skyline": skyline, "ground": ground, "signs": signs, "foliage": foliage, "decals": decals, "shop": shop, "props": props}
    for k, f in steps.items():
        if "all" in what or k in what: f()
