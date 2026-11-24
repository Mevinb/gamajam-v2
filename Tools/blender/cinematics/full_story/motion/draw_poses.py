"""Plot actual actor skeleton FK in two orthographic views, without GPU/rendering."""
import sys
sys.dont_write_bytecode=True
import json,html
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[4]
OUT=PROJECT/'ArtSource/FullCinematics/Motion'
data=json.loads((OUT/'pose_diagrams.json').read_text())
clips=['Idle_Neutral','Point_Loop','Console_Loop','Collapse']
body=['Hips','Spine','Chest','UpperChest','Neck','Head']
for side in ['Left','Right']:
    body += [side+n for n in ['Shoulder','UpperArm','LowerArm','Hand','UpperLeg','LowerLeg','Foot','Toes']]
fontpath='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
for actor in ['DrPetrov','MaintEli']:
    image=Image.new('RGB',(1800,1080),'#101820')
    draw=ImageDraw.Draw(image)
    svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="1080" viewBox="0 0 1800 1080">','<rect width="1800" height="1080" fill="#101820"/>']
    def line(points,color,width=1):
        draw.line(points,fill=color,width=width)
        svg.append('<polyline points="'+' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round"/>')
    def circle(x,y,color,r=3):
        draw.ellipse((x-r,y-r,x+r,y+r),fill=color)
        svg.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="{r}" fill="{color}"/>')
    def text(x,y,value,size=16,color='#a5b8c2'):
        font=ImageFont.truetype(fontpath,size)
        draw.text((x,y),value,font=font,fill=color)
        svg.append(f'<text x="{x}" y="{y+size}" font-family="DejaVu Sans,sans-serif" font-size="{size}" fill="{color}">{html.escape(value)}</text>')
    text(50,24,actor+' — actual character rig / authored motion retarget',30,'#edf1f3')
    text(50,69,'53 mapped body and finger bones • all panels share metre scale • cyan = left / orange = right',17)
    for row,view in enumerate(['FRONT','SIDE']):
        for col,clip in enumerate(clips):
            item=next(v for v in data if v['actor']==actor and v['clip']==clip)
            x=50+col*438;y=134+row*438
            text(x,y,clip.replace('_',' '),20,'#edf1f3')
            text(x,y+29,f"{view}  •  t = {item['seconds']:.2f} s",14)
            px=x+18;py=y+66
            scale=139.0
            coord=lambda p:(px+((p[0] if view=='FRONT' else -p[1])+1.5)*scale,py+(2-p[2])*scale)
            for height in [0,.5,1,1.5,2]:
                yy=py+(2-height)*scale
                line([(px,yy),(px+2.8*scale,yy)],'#344650' if height else '#718b98',1 if height else 2)
                text(px,yy-19,f'{height:g}m',11)
            for n in body:
                b=item['bones'][n]
                color='#49bfe2' if n.startswith('Left') else '#f1a366' if n.startswith('Right') else '#dce5e9'
                h=coord(b['head']);t=coord(b['tail'])
                line([h,t],color,5 if n!='Head' else 10)
                circle(*h,color,r=3)
                parent=b['parent']
                if parent and parent in body:
                    ph=coord(item['bones'][parent]['tail'])
                    line([h,ph],color,1)
            text(x+18,y+388,'actor x (m)' if view=='FRONT' else 'actor forward (m)',13)
    text(50,1036,'CPU FK of imported DrPetrov / MaintEli bones. Geometry was separately deformed and checked for finite vertices.',16)
    image.save(OUT/(actor+'_pose_verification.png'))
    svg.append('</svg>')
    (OUT/(actor+'_pose_verification.svg')).write_text('\n'.join(svg)+'\n')
report=json.loads((OUT/'verification.json').read_text())
report['pose_diagrams']=[actor+'_pose_verification.png' for actor in ['DrPetrov','MaintEli']]
(OUT/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
