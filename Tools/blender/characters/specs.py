"""
PARASITE character roster. Pure data (no bpy) so both Blender and plain Python can read it.

age: MPFB macro (0.1875 = 11 y, 0.5 = 25 y, 1.0 = 90 y; linear in between).  height/weight/muscle: 0..1 macros.
clothes: list of {asset, tint?, split?, color?, z?, decimate?}
  tint  -> whole item uses tint group "A"/"B"/"C" (normalised grey in the atlas, coloured by the shader)
  split -> {"upper": g, "lower": g}: connected parts above / below the waist get different groups
  color -> sRGB default colour of the tint group (else the original average colour is kept)
palettes: tint group -> runtime palette name (HumanoidRig re-tints generic NPCs from these)
garments: procedural extras: coat, vest, belt, badge, lanyard, hardhat
names: display names (HostCharacter) that map onto this exact variant
"""


def age(years):
    if years <= 25:
        return 0.1875 + (years - 11) / 14.0 * 0.3125
    return 0.5 + (years - 25) / 65.0 * 0.5


BLACK_HAIR = (0.045, 0.038, 0.034)
DARK_BROWN = (0.12, 0.075, 0.05)
BROWN = (0.26, 0.16, 0.09)
AUBURN = (0.30, 0.13, 0.07)
DIRTY_BLONDE = (0.55, 0.42, 0.25)
BLONDE = (0.78, 0.64, 0.42)
GREY = (0.52, 0.50, 0.49)
SILVER = (0.80, 0.80, 0.81)

NAVY = (0.10, 0.13, 0.24)
UNIFORM_PANTS = (0.07, 0.08, 0.11)

CHARACTERS = [
    # ------------------------------------------------------------------ civilians
    dict(id="CivMaya", profession="Civilian", names=["Maya Chen"], gender=0.0, age=age(26), height=0.42, weight=0.42,
         muscle=0.45, proportions=0.65, race=dict(asian=0.92, caucasian=0.08), skin="young_asian_female",
         eyes="brown", brows="eyebrow005", lashes="eyelashes02", hair="toigo_inverted_bob",
         hair_color=BLACK_HAIR,
         clothes=[dict(asset="female_casualsuit01", split={"upper": "A", "lower": "B"}, color_A=(0.78, 0.30, 0.28)),
                  dict(asset="shoes06")],
         palettes={"A": "top", "B": "denim"}),
    dict(id="CivOmar", profession="Civilian", names=["Omar Haddad"], gender=1.0, age=age(46), height=0.52, weight=0.6,
         muscle=0.5, race=dict(caucasian=0.62, asian=0.18, african=0.2), skin="middleage_caucasian_male",
         skin_tint=(0.92, 0.84, 0.77), eyes="brown", brows="eyebrow002", lashes="eyelashes01",
         hair="short04", hair_color=DARK_BROWN,
         clothes=[dict(asset="elvs_male_shirt_untucked_bd1", tint="A", color=(0.62, 0.70, 0.80), z=55),
                  dict(asset="elvs_male_trouser", tint="B", color=(0.28, 0.24, 0.20), z=47),
                  dict(asset="mindfront_shoes_oxford_male")],
         palettes={"A": "shirt", "B": "trousers"}),
    dict(id="CivLena", profession="Civilian", names=["Lena Fischer"], gender=0.0, age=age(31), height=0.55, weight=0.45,
         muscle=0.5, race=dict(caucasian=1.0), skin="young_caucasian_female2", eyes="bluegreen",
         brows="eyebrow008", lashes="eyelashes03", hair="ponytail01", hair_color=DIRTY_BLONDE,
         clothes=[dict(asset="elvs_hooded_sweat_jacket1", tint="A", color=(0.36, 0.48, 0.34), decimate=0.45),
                  dict(asset="punkduck_female_tight_jeans", tint="B"),
                  dict(asset="punkduck_comfortable_sneakers")],
         palettes={"A": "hoodie", "B": "denim"}),
    dict(id="CivTheo", profession="Civilian", names=["Theo Brooks"], gender=1.0, age=age(29), height=0.6, weight=0.5,
         muscle=0.62, race=dict(african=0.9, caucasian=0.1), skin="young_african_male", eyes="brown",
         brows="eyebrow001", lashes="eyelashes01", hair="short01", hair_color=BLACK_HAIR,
         clothes=[dict(asset="male_casualsuit05", split={"upper": "A", "lower": "B"}), dict(asset="shoes05")],
         palettes={"A": "jacket", "B": "denim"}),
    dict(id="CivLeo", profession="Civilian", names=["Leo Park"], gender=1.0, age=age(22), height=0.5, weight=0.42,
         muscle=0.5, race=dict(asian=0.95, caucasian=0.05), skin="young_asian_male", eyes="brown",
         brows="eyebrow003", lashes="eyelashes01", hair="cortu_short_messy_hair",
         hair_color=BLACK_HAIR,
         clothes=[dict(asset="namuhekam_male_polo_shirt", tint="A", color=(0.20, 0.35, 0.55)),
                  dict(asset="elvs_jeans_straight_leg", tint="B"), dict(asset="shoes06")],
         palettes={"A": "shirt", "B": "denim"}),
    dict(id="CivAmara", profession="Civilian", names=["Amara Diallo"], gender=0.0, age=age(28), height=0.58,
         weight=0.45, muscle=0.5, race=dict(african=1.0), skin="young_african_female", eyes="brown",
         brows="eyebrow006", lashes="eyelashes04", hair="braid01", hair_color=BLACK_HAIR,
         clothes=[dict(asset="female_elegantsuit01", split={"upper": "A", "lower": "B"}, color_A=(0.85, 0.70, 0.25)),
                  dict(asset="toigo_ballet_flats", decimate=0.4)],
         palettes={"A": "top", "B": "skirt"}),
    dict(id="CivWalter", profession="Civilian", names=[], gender=1.0, age=age(71), height=0.45, weight=0.55,
         muscle=0.4, race=dict(caucasian=1.0), skin="old_caucasian_male", eyes="lightblue",
         brows="eyebrow004", lashes="eyelashes01", hair="short02", hair_color=GREY,
         clothes=[dict(asset="mindfront_knitted_sweater_01", tint="A", color=(0.45, 0.20, 0.17)),
                  dict(asset="toigo_wool_pants", tint="B", color=(0.30, 0.28, 0.24)),
                  dict(asset="mindfront_shoes_monk_strap_male", decimate=0.5),
                  dict(asset="elvs_male_flat_cap1", tint="C", color=(0.30, 0.27, 0.22), z=100)],
         palettes={"A": "sweater", "B": "trousers", "C": "cap"}),
    dict(id="CivGrace", profession="Civilian", names=[], gender=0.0, age=age(52), height=0.5, weight=0.55,
         muscle=0.45, race=dict(caucasian=1.0), skin="middleage_caucasian_female", eyes="green",
         brows="eyebrow007", lashes="eyelashes03", hair="toigo_curled_under_bob",
         hair_color=AUBURN,
         clothes=[dict(asset="toigo_female_suit", tint="A", color=(0.22, 0.24, 0.32), decimate=0.3),
                  dict(asset="mindfront_shoes_oxford_female")],
         palettes={"A": "suit"}),
    dict(id="CivKenji", profession="Civilian", names=[], gender=1.0, age=age(45), height=0.48, weight=0.5,
         muscle=0.5, race=dict(asian=1.0), skin="middleage_asian_male", eyes="brown", brows="eyebrow002",
         lashes="eyelashes01", hair="short03", hair_color=BLACK_HAIR,
         clothes=[dict(asset="toigo_male_suit_3", decimate=0.55), dict(asset="mindfront_shoes_oxford_male")],
         palettes={}),
    dict(id="CivSofia", profession="Civilian", names=[], gender=0.0, age=age(24), height=0.48, weight=0.4,
         muscle=0.55, race=dict(caucasian=0.7, african=0.15, asian=0.15), skin="young_caucasian_female",
         skin_tint=(0.95, 0.87, 0.80), eyes="brownlight", brows="eyebrow009",
         lashes="eyelashes02", hair="long01", hair_color=DARK_BROWN,
         clothes=[dict(asset="female_sportsuit01", split={"upper": "A", "lower": "B"}),
                  dict(asset="punkduck_running_shoes_01")],
         palettes={"A": "top", "B": "trousers"}),

    # ------------------------------------------------------------------ security
    dict(id="GuardMarcus", profession="Guard", names=["Sgt. Marcus Reed"], gender=1.0, age=age(45), height=0.62,
         weight=0.62, muscle=0.68, race=dict(african=0.9, caucasian=0.1), skin="middleage_african_male",
         eyes="brown", brows="eyebrow001", lashes="eyelashes01", hair="short01",
         hair_color=BLACK_HAIR,
         clothes=[dict(asset="elvs_male_shirt_tie_tucked1", tint="A", color=NAVY, z=48),
                  dict(asset="mindfront_male_trousers_1", tint="B", color=UNIFORM_PANTS, z=47),
                  dict(asset="toigo_ankle_boots_male", z=60),
                  dict(asset="mindfront_patrol_cap", tint="C", color=(0.08, 0.10, 0.17), z=100)],
         garments=["belt", "badge"], palettes={"A": "uniform", "B": "uniform_pants", "C": "uniform"}),
    dict(id="GuardDana", profession="Guard", names=["Dana Kowalski"], gender=0.0, age=age(34), height=0.58,
         weight=0.48, muscle=0.6, race=dict(caucasian=1.0), skin="young_caucasian_female", eyes="blue",
         brows="eyebrow008", lashes="eyelashes02", hair="rehmanpolanski_hair_bun_brown",
         hair_color=BLONDE,
         clothes=[dict(asset="namuhekam_male_polo_shirt", tint="A", color=NAVY, z=48),
                  dict(asset="mindfront_female_trousers_1", tint="B", color=UNIFORM_PANTS, z=47),
                  dict(asset="toigo_ankle_boots_female", z=60)],
         garments=["belt", "badge"], palettes={"A": "uniform", "B": "uniform_pants"}),
    dict(id="GuardVictor", profession="Guard", names=["Victor Hale"], gender=1.0, age=age(58), height=0.55,
         weight=0.66, muscle=0.55, race=dict(caucasian=1.0), skin="middleage_caucasian_male", eyes="grey",
         brows="eyebrow004", lashes="eyelashes01", hair="short02", hair_color=(0.36, 0.33, 0.30),
         clothes=[dict(asset="elvs_male_shirt_tie_tucked1", tint="A", color=(0.16, 0.18, 0.22), z=48),
                  dict(asset="mindfront_male_trousers_1", tint="B", color=UNIFORM_PANTS, z=47),
                  dict(asset="toigo_ankle_boots_male", z=60),
                  dict(asset="mindfront_patrol_cap", tint="C", color=(0.08, 0.10, 0.17), z=100)],
         garments=["belt", "badge"], palettes={"A": "uniform", "B": "uniform_pants", "C": "uniform"}),
    dict(id="GuardPriya", profession="Guard", names=["Priya Nair"], gender=0.0, age=age(30), height=0.5,
         weight=0.45, muscle=0.58, race=dict(asian=0.6, caucasian=0.2, african=0.2), skin="cutoff3d_indian_female_skin",
         eyes="brown", brows="eyebrow006", lashes="eyelashes03", hair="ponytail01",
         hair_color=BLACK_HAIR,
         clothes=[dict(asset="toigo_basic_tucked_t-shirt", tint="A", color=NAVY, z=48),
                  dict(asset="mindfront_female_trousers_1", tint="B", color=UNIFORM_PANTS, z=47),
                  dict(asset="toigo_ankle_boots_female", z=60),
                  dict(asset="mindfront_patrol_cap", tint="C", color=(0.08, 0.10, 0.17), z=100)],
         garments=["belt", "badge"], palettes={"A": "uniform", "B": "uniform_pants", "C": "uniform"}),

    dict(id="GuardJonas", profession="Guard", names=[], gender=1.0, age=age(27), height=0.58, weight=0.5,
         muscle=0.62, race=dict(caucasian=0.75, african=0.1, asian=0.15), skin="young_caucasian_male",
         skin_tint=(0.95, 0.88, 0.80), eyes="brownlight", brows="eyebrow003", lashes="eyelashes01", hair="short03",
         hair_color=DARK_BROWN,
         clothes=[dict(asset="namuhekam_male_polo_shirt", tint="A", color=NAVY, z=48),
                  dict(asset="mindfront_male_trousers_1", tint="B", color=UNIFORM_PANTS, z=47),
                  dict(asset="toigo_ankle_boots_male", z=60),
                  dict(asset="mindfront_patrol_cap", tint="C", color=(0.08, 0.10, 0.17), z=100)],
         garments=["belt", "badge"], palettes={"A": "uniform", "B": "uniform_pants", "C": "uniform"}),

    # ------------------------------------------------------------------ scientists
    dict(id="SciIngrid", profession="Scientist", names=["Dr. Ingrid Sato"], gender=0.0, age=age(41), height=0.5,
         weight=0.45, muscle=0.45, race=dict(asian=0.85, caucasian=0.15), skin="middleage_asian_female", eyes="brown",
         brows="eyebrow005", lashes="eyelashes02", hair="toigo_blunt_bob", hair_color=BLACK_HAIR,
         glasses="spamrakuen_tbm_glasses_frames_01",
         clothes=[dict(asset="toigo_basic_tucked_t-shirt", tint="A", color=(0.55, 0.62, 0.70)),
                  dict(asset="toigo_wool_pants", tint="B", color=(0.18, 0.18, 0.20)),
                  dict(asset="mindfront_shoes_oxford_female")],
         garments=["coat", "lanyard"], palettes={"A": "shirt", "B": "trousers"}),
    dict(id="SciFelix", profession="Scientist", names=["Dr. Felix Moreau"], gender=1.0, age=age(38), height=0.56,
         weight=0.48, muscle=0.5, race=dict(caucasian=1.0), skin="middleage_caucasian_male", eyes="blue",
         brows="eyebrow003", lashes="eyelashes01", hair="cortu_short_messy_hair",
         hair_color=BROWN,
         clothes=[dict(asset="elvs_male_shirt_tie_tucked1", tint="A", color=(0.62, 0.72, 0.85)),
                  dict(asset="elvs_male_trouser", tint="B", color=(0.20, 0.21, 0.24)),
                  dict(asset="mindfront_shoes_oxford_male")],
         garments=["coat", "lanyard"], palettes={"A": "shirt", "B": "trousers"}),
    dict(id="SciSamuel", profession="Scientist", names=[], gender=1.0, age=age(52), height=0.55, weight=0.58,
         muscle=0.5, race=dict(african=1.0), skin="middleage_african_male", eyes="brown",
         brows="eyebrow002", lashes="eyelashes01", hair="short01", hair_color=(0.20, 0.19, 0.18),
         glasses="spamrakuen_tbm_glasses_frames_01",
         clothes=[dict(asset="male_casualsuit03", split={"upper": "A", "lower": "B"}, color_B=(0.22, 0.20, 0.18)),
                  dict(asset="shoes01")],
         garments=["coat", "lanyard"], palettes={"A": "shirt", "B": "trousers"}),

    # ------------------------------------------------------------------ maintenance
    dict(id="MaintGus", profession="Maintenance", names=["Gus Romero"], gender=1.0, age=age(55), height=0.5,
         weight=0.72, muscle=0.6, race=dict(caucasian=0.7, african=0.1, asian=0.2), skin="middleage_caucasian_male",
         skin_tint=(0.93, 0.85, 0.78), eyes="brown", brows="eyebrow004", lashes="eyelashes01",
         hair="short04", hair_color=(0.22, 0.20, 0.19), beard="wdg_scruffy_beard",
         clothes=[dict(asset="elvs_male_coveralls_1", tint="A", color=(0.16, 0.22, 0.36), decimate=0.35),
                  dict(asset="toigo_ankle_boots_male", z=60)],
         garments=["vest", "hardhat"], vest_color=(1.0, 0.45, 0.04), hat_color=(1.0, 0.78, 0.06),
         palettes={"A": "coverall"}),
    dict(id="MaintRita", profession="Maintenance", names=["Rita Okafor"], gender=0.0, age=age(40), height=0.55,
         weight=0.55, muscle=0.6, race=dict(african=1.0), skin="middleage_african_female", eyes="brown",
         brows="eyebrow006", lashes="eyelashes02", hair="rehmanpolanski_hair_bun_brown",
         hair_color=BLACK_HAIR,
         clothes=[dict(asset="male_worksuit01", split={"upper": "B", "lower": "A"}, color_A=(0.20, 0.30, 0.48)),
                  dict(asset="toigo_ankle_boots_female", z=60)],
         garments=["vest"], vest_color=(0.80, 1.0, 0.05), palettes={"A": "coverall", "B": "tshirt"}),
    dict(id="MaintNoor", profession="Maintenance", names=["Engineer Noor Patel"], gender=0.0, age=age(31),
         height=0.5, weight=0.45, muscle=0.55, race=dict(asian=0.6, caucasian=0.2, african=0.2),
         skin="cutoff3d_indian_female_enhanced", eyes="brown", brows="eyebrow005",
         lashes="eyelashes03", hair="braid01", hair_color=BLACK_HAIR,
         clothes=[dict(asset="elvs_male_coveralls_1", tint="A", color=(0.24, 0.26, 0.28), decimate=0.35),
                  dict(asset="toigo_ankle_boots_female", z=60)],
         garments=["vest", "hardhat"], vest_color=(1.0, 0.45, 0.04), hat_color=(0.95, 0.95, 0.93),
         palettes={"A": "coverall"}),

    dict(id="MaintEli", profession="Maintenance", names=[], gender=1.0, age=age(33), height=0.55, weight=0.55,
         muscle=0.6, race=dict(african=0.5, caucasian=0.5), skin="young_african_male", skin_tint=(1.0, 0.95, 0.9),
         eyes="brown", brows="eyebrow001", lashes="eyelashes01", hair="short01", hair_color=BLACK_HAIR,
         clothes=[dict(asset="male_worksuit01", split={"upper": "B", "lower": "A"}, color_A=(0.30, 0.32, 0.22)),
                  dict(asset="toigo_ankle_boots_male", z=60)],
         garments=["vest", "hardhat"], vest_color=(0.80, 1.0, 0.05), hat_color=(0.95, 0.95, 0.93),
         palettes={"A": "coverall", "B": "tshirt"}),

    # ------------------------------------------------------------------ unique story characters
    dict(id="DrVale", profession="Scientist", unique=True, names=["Dr. Vale"], gender=1.0, age=age(66), height=0.58,
         weight=0.5, muscle=0.42, race=dict(caucasian=1.0), skin="old_caucasian_male", eyes="grey",
         brows="eyebrow004", lashes="eyelashes01", hair="short02", hair_color=SILVER,
         glasses="frankyaye_glasses_library_male",
         clothes=[dict(asset="male_elegantsuit01", decimate=0.5), dict(asset="mindfront_shoes_oxford_male")],
         garments=["coat"], coat_vale=True, palettes={}),
    dict(id="DrPetrov", profession="Scientist", unique=True, names=["Dr. Hana Petrov"], gender=0.0, age=age(38),
         height=0.52, weight=0.45, muscle=0.5, race=dict(caucasian=1.0), skin="middleage_caucasian_female",
         eyes="green", brows="eyebrow007", lashes="eyelashes04",
         hair="rehmanpolanski_hair_bun_brown", hair_color=AUBURN,
         clothes=[dict(asset="punkduck_lace_up_blouse", tint="A", color=(0.86, 0.84, 0.80)),
                  dict(asset="mindfront_female_trousers_1", tint="B", color=(0.16, 0.16, 0.19), z=47),
                  dict(asset="mindfront_shoes_oxford_female")],
         garments=["coat", "lanyard"], palettes={"A": "shirt", "B": "trousers"}),
    dict(id="LakeGirl", profession="Civilian", unique=True, names=["Lakeside Girl"], gender=0.0, age=age(17),
         height=0.45, height_m=1.60, weight=0.4, muscle=0.45, proportions=0.6, race=dict(caucasian=0.9, asian=0.1),
         skin="young_caucasian_female", eyes="brown", brows="eyebrow009", lashes="eyelashes02",
         hair="long01", hair_color=(0.055, 0.04, 0.035),
         clothes=[dict(asset="toigo_fisherman_sweater", tint="A", color=(0.73, 0.65, 0.53), z=55),
                  dict(asset="punkduck_female_tight_jeans", tint="B", color=(0.16, 0.22, 0.29)),
                  dict(asset="toigo_ankle_boots_female", z=60)],
         palettes={"A": "sweater", "B": "denim"}),
]


def get(cid):
    for c in CHARACTERS:
        if c["id"] == cid:
            return c
    raise KeyError(cid)
