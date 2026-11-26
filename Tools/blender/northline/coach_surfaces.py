"""Original coach trim surfaces; stdlib only. No image generation or writes on import.

Eight 2048x256 trim bands, with 16px gutters, share three 2048-square maps.
Paint is dielectric until a deliberately bounded coating chip exposes metal.
Wear follows drainage, machining, wheel contact, floor tread and vinyl seams;
there is no whole-object random-noise overlay or baked lighting.
"""
import math
import struct
import zlib
from pathlib import Path

SIZE = 2048
BAND_HEIGHT = 256
GUTTER = 16
PIXELS_PER_METRE = 128
BANDS = ("blue_paint", "interior_enamel", "brushed_steel", "wheel_steel",
         "rubber", "nonslip_floor", "seat_vinyl", "safety_yellow")


def clamp(x, lo=0.0, hi=1.0):
    return max(lo, min(hi, x))


def pulse(x, centre, width):
    return clamp(1.0 - abs(x - centre) / width)


def surface(band, x, y):
    """Return sRGB colour, metallic, roughness, reference relief(m at 128px/m).

    x/y are local pixels in a padded trim band, not camera/screen coordinates.
    Features use repeatable analytical manufacturing marks, without random seeds.
    """
    x = max(GUTTER, min(SIZE-GUTTER-1, x)) - GUTTER
    y = max(GUTTER, min(BAND_HEIGHT-GUTTER-1, y)) - GUTTER
    if band in (0, 1, 7):
        colour = ((.125, .245, .295), (.69, .705, .66), (.82, .57, .12))[{0:0, 1:1, 7:2}[band]]
        # Water streaks have distinct source spacing and taper down the sheet.
        runoff = 0.0
        for source, width, length in ((83, 2.8, 168), (307, 4.1, 210), (617, 2.1, 143),
                                      (1061, 3.2, 195), (1439, 2.7, 172), (1847, 4.0, 205)):
            runoff += pulse(x, source + .8*math.sin(y*.035), width) * clamp((length-y)/100)
        # Sparse coating damage lives in a narrow lower drainage edge. The
        # remaining paint stays non-metallic, unlike the old all-metal slab.
        chipped = y < 3.0 and (math.sin(x*.119)+math.sin(x*.043)) > 1.25
        if chipped:
            return (.31, .32, .30), .94, .40, -.00012
        orange_peel = .5 + .5*math.sin(x*1.71)*math.sin(y*1.37)
        tint = 1 - .12*clamp(runoff)
        return tuple(c*tint for c in colour), 0, clamp(.40+.055*orange_peel+.08*runoff), .000009*orange_peel
    if band == 2:
        # Mill direction is along the 16m trim axis; narrow tool lines, not
        # isotropic speckle. A few longitudinal scoring marks are subtler.
        tool = .5+.5*math.sin(y*2.57 + .35*math.sin(x*.003))
        score = pulse(y, 74+.15*math.sin(x*.011), .7)
        return (.46+.025*tool, .47+.025*tool, .46+.025*tool), .97, .29+.08*tool+.05*score, .000006*tool-.000025*score
    if band == 3:
        # Wheel profile UVs put the actual running tread in y=60..150;
        # oxidized flange/inner web lies outside this physical contact band.
        contact = clamp(min((y-52)/12, (159-y)/12))
        lathe = .5+.5*math.sin(y*2.3)
        oxide = (1-contact)*(.5+.5*math.sin(x*.017)*math.sin(y*.031))
        colour = tuple((.28+.06*lathe)*contact + c*(1-contact) for c in (.205, .155, .105))
        return colour, .96-.46*oxide, .20*contact+.70*(1-contact)+.025*lathe, .000008*lathe
    if band == 4:
        rib = pulse((y % 18), 2, 1.5)
        return (.025, .029, .028), 0, .76+.10*rib, -.00012*rib
    if band == 5:
        # Raised alternating diamond/checker plate. This is surface relief;
        # silhouette/footing still comes from the unchanged actual deck.
        qx, qy = x % 44, y % 44
        diagonal = min(abs(qx-qy-9), abs(qx+qy-34))
        raised = pulse(diagonal, 0, 2.3) if 8<qx<36 and 8<qy<36 else 0
        worn = .08*raised
        return (.17+worn, .185+worn, .18+worn), .86, .64-.25*raised, .00065*raised
    if band == 6:
        # Rail vinyl upholstery: resolvable 12.5cm moulded panel seams and a
        # broad polished contact area. Sub-millimetre fabric threads cannot be
        # resolved by this atlas; do not fake them with Nyquist-sampled noise.
        seam = pulse(x % 16, 2, 1.2)
        contact = pulse(y, 40, 28)
        return (.075-.006*seam, .15-.01*seam, .17-.01*seam), 0, .76-.08*contact+.03*seam, -.00018*seam
    raise ValueError("Unknown coach surface band")


def pixel(band, x, y):
    colour, metallic, roughness, height = surface(band, x, y)
    du = (surface(band, x+1, y)[3]-surface(band, x-1, y)[3]) * PIXELS_PER_METRE*.5
    dv = (surface(band, x, y+1)[3]-surface(band, x, y-1)[3]) * PIXELS_PER_METRE*.5
    length = math.sqrt(du*du+dv*dv+1)
    byte = lambda q: max(0, min(255, round(q*255)))
    return (bytes((*map(byte, colour), 255)),
            bytes((byte((1-du/length)*.5), byte((1-dv/length)*.5), byte((1+1/length)*.5), 255)),
            bytes((byte(metallic), 255, 0, byte(1-roughness))))


def chunk(file, kind, data):
    file.write(struct.pack(">I", len(data))+kind+data+struct.pack(">I", zlib.crc32(kind+data) & 0xffffffff))


def write_atlas(directory):
    """Write A(sRGB), N(linear), M(linear R=metal/A=smooth) PNGs, no bpy/GPU.

    Called only by the explicit reviewed Blender --go authoring path. PNG row
    order is top-down; local band coordinates and UV coordinates are bottom-up.
    Normal Y therefore corresponds to increasing UV V, rather than PNG row.
    """
    directory = Path(directory)
    directory.mkdir()
    paths = [directory/("RailCoach_"+suffix+".png") for suffix in ("A", "N", "M")]
    files = [p.open("xb") for p in paths]
    streams = [zlib.compressobj(6) for _ in paths]
    try:
        for file in files:
            file.write(b"\x89PNG\r\n\x1a\n")
            chunk(file, b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))
        for image_y in range(SIZE-1, -1, -1):
            band, y = divmod(image_y, BAND_HEIGHT)
            rows = [bytearray(b"\x00") for _ in files]
            for x in range(SIZE):
                for row, value in zip(rows, pixel(band, x, y)):
                    row.extend(value)
            for file, stream, row in zip(files, streams, rows):
                data = stream.compress(row)
                if data:
                    chunk(file, b"IDAT", data)
        for file, stream in zip(files, streams):
            chunk(file, b"IDAT", stream.flush())
            chunk(file, b"IEND", b"")
    finally:
        for file in files:
            file.close()
    return paths
