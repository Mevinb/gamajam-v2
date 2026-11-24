"""Assemble unchanged CPU renders with labels; reject stale source evidence."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT/'ArtSource/FullCinematics/Lake'
audit = json.loads((OUT/'audit.json').read_text())
manifest = json.loads((OUT/'manifest.json').read_text())
font_path = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
title = ImageFont.truetype(font_path,21)
label = ImageFont.truetype(font_path,17)
board = Image.new('RGB',(1920,1360),(17,22,23))
d = ImageDraw.Draw(board)
summaries = {
    'arrival':'ARRIVAL · 15 seconds · neutral attention',
    'hold':'CHOICE HOLD · 6 seconds · no automatic outcome',
    'peace':'PEACE · 7 seconds · explicit Peace choice only',
    'dominion':'DOMINION · 7 seconds · explicit Dominion choice only',
}
checks = {}
for row,film in enumerate(['arrival','hold','peace','dominion']):
    ev = json.loads((OUT/'previews'/(film+'_evidence.json')).read_text())
    assert ev['source_sha256']==audit[film]['sha256'], (film,'stale source preview')
    assert ev['device']=='CPU' and ev['threads']==2 and ev['resolution']==[480,270]
    assert [v['frame'] for v in ev['frames']]==manifest['films'][film]['preview_frames']
    y = row*340
    d.text((13,y+8),summaries[film],font=title,fill=(225,224,206))
    for col,item in enumerate(ev['frames']):
        path = OUT/'previews'/item['png']
        assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256'],path
        im = Image.open(path).convert('RGB')
        assert im.size==(480,270)
        board.paste(im,(col*480,y+39))
        d.text((col*480+12,y+314),'frame %d · Cycles CPU / 8 samples'%item['frame'],font=label,fill=(171,181,177))
    checks[film] = {'source_sha256':ev['source_sha256'],'verified_cpu_frames':len(ev['frames']),
                    'cpu_preview_seconds':round(sum(v['seconds'] for v in ev['frames']),3)}
board.save(OUT/'Lake_Storyboard_CPU.png')
(OUT/'preview_verification.json').write_text(json.dumps(checks,indent=2)+'\n')
print('PASS: final source hashes and 13 CPU frame hashes; storyboard saved')
