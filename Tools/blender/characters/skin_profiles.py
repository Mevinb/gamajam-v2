"""Shared MakeHuman UV skin-surface sets, independent of skin colour and garment atlases."""

PROFILES = {
    "Adult": dict(age=0.53, pore_depth=0.000070, crease_depth=0.000025),
    "Mature": dict(age=0.68, pore_depth=0.000085, crease_depth=0.000075),
    "Elder": dict(age=0.84, pore_depth=0.000095, crease_depth=0.000145),
}


def profile_for(age):
    return "Adult" if age < 0.59 else ("Mature" if age < 0.77 else "Elder")


def textures_for(age):
    stem = "Skins/Surface/" + profile_for(age)
    return {"skinN": stem + "_N.png", "skinM": stem + "_M.png"}
