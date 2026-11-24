"""Export the preserved live-caption timing for editorial/audio review, never into runtime Assets."""
import sys,json
sys.dont_write_bytecode=True
from pathlib import Path
from plan import CAPTIONS,DURATION
out=Path(__file__).resolve().parents[4]/'ArtSource/FullCinematics/Opening'
def stamp(sec):
 millis=round(sec*1000);hours,millis=divmod(millis,3600000);minutes,millis=divmod(millis,60000);seconds,millis=divmod(millis,1000)
 return f'{hours:02d}:{minutes:02d}:{seconds:02d},{millis:03d}'
blocks=[]
for i,(at,who,text) in enumerate(CAPTIONS):
 if not text:continue
 end=CAPTIONS[i+1][0] if i+1<len(CAPTIONS) else DURATION
 blocks.append(f'{len(blocks)+1}\n{stamp(at)} --> {stamp(end)}\n{who}\n{text}')
(out/'opening-captions-review.srt').write_text('\n\n'.join(blocks)+'\n')
print('Exported',len(blocks),'caption intervals with unchanged story cue times')
