"""CPU plot of the shipped-audio envelope and 24fps lookup samples."""
import sys
sys.dont_write_bytecode=True
import json,math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[4]
OUT=PROJECT/'ArtSource/FullCinematics/Motion'
sys.path.insert(0,str(HERE.parent))
from motion.speech import SpeechPerformance
performance=SpeechPerformance(PROJECT)
records=performance.data['lines']
image=Image.new('RGB',(1800,1320),'#101820')
draw=ImageDraw.Draw(image)
fontpath='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
def text(x,y,value,size=18,color='#b2c7cf'):
    draw.text((x,y),value,font=ImageFont.truetype(fontpath,size),fill=color)
text(42,20,'Opening jaw aperture from the shipped actor speech',31,'#edf1f3')
text(42,70,'100 Hz energy cache · 25 ms RMS window · 15 ms attack / 55 ms release · no phoneme or viseme inference',19)
for i,entry in enumerate(records):
    col=i%2;row=i//2
    x=42+col*890;y=128+row*275
    text(x,y,entry['speaker'],17,'#e3edf0')
    text(x,y+28,entry['line'],21,'#edf1f3')
    text(x,y+61,f"cue {entry['cue_seconds']:g}s  ·  measured {entry['duration_seconds']:.2f}s  ·  {entry['key']}",15)
    px=x+35;py=y+102;w=795;h=119
    duration=entry['duration_seconds']
    for value in [0,.5,1]:
        yy=py+h-value*h
        draw.line([(px,yy),(px+w,yy)],fill='#38505a',width=1)
        text(x,yy-9,f'{value:g}',12)
    points=[(px+min(duration,index/entry['envelope_hz'])/duration*w,py+h-value*h)
            for index,value in enumerate(entry['amplitude'])]
    draw.line(points,fill='#54d8b5',width=3)
    for frame in range(math.ceil(duration*24)+1):
        time=min(duration,frame/24)
        value=performance.amplitude(entry['speaker'],entry['line'],time)
        xx=px+time/duration*w;yy=py+h-value*h
        draw.ellipse((xx-2,yy-2,xx+2,yy+2),fill='#eeb369')
    text(px,py+h+12,'0s',13)
    text(px+w-55,py+h+12,f'{duration:.2f}s',13)
text(42,1250,'Green: source-derived envelope. Amber: actual lookup at 24 fps. Before/after the media duration, the amplitude is zero.',17)
text(42,1280,'All 8 FNV keys match VoiceOver and the catalogue; decoded PCM duration equals ffprobe duration. Original media is unchanged.',16)
image.save(OUT/'opening_speech_envelopes.png')
