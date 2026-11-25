"""Actual stream validation reused from the staged runtime publication gate; no synthetic media."""
import subprocess
from fractions import Fraction
from common import *

def validate_movie(path,name):
    path=Path(path);require(name in FILMS and path.is_file() and not path.is_symlink(),'Missing/nonregular real movie: '+str(path))
    size=path.stat().st_size;require(0<size<MAX_BYTES,'Movie must be strictly below95000000bytes: '+name)
    command=['ffprobe','-v','error','-threads','2','-count_frames','-show_streams','-show_format','-of','json',str(path)]
    result=subprocess.run(command,capture_output=True,text=True)
    require(result.returncode==0 and not result.stderr.strip(),'ffprobe failed: '+result.stderr)
    data=json.loads(result.stdout);streams=data.get('streams',[])
    require(len(streams)==1 and streams[0].get('codec_type')=='video','Only one silent video stream is allowed; never discard embedded voices')
    stream=streams[0]
    require(stream.get('codec_name')=='vp8' and stream.get('pix_fmt')=='yuv420p','Output must be VP8/yuv420p')
    require('webm' in data.get('format',{}).get('format_name',''),'Output must be WebM')
    require(stream.get('width')==1920 and stream.get('height')==1080 and Fraction(stream['avg_frame_rate'])==24,'Output must be1920x1080 at24fps')
    require(int(stream.get('nb_read_frames',0))==FILMS[name],'Encoded stream has missing/extra frames')
    duration=float(stream.get('duration',data['format']['duration']))
    require(abs(duration-FILMS[name]/24)<=1/24+.005,'Encoded stream has wrong duration')
    decoded=subprocess.run(['ffmpeg','-v','error','-threads','2','-i',str(path),'-map','0:v:0','-f','null','-'],capture_output=True,text=True)
    require(decoded.returncode==0 and not decoded.stderr.strip(),'Full-stream ffmpeg decode failed: '+decoded.stderr)
    return {'name':name,'resource':'Cinematics/FullStory/'+name,'filename':name+'.webm','sha256':sha(path),'bytes':size,'width':1920,'height':1080,'fps':24,'frames':FILMS[name],'seconds':duration,'audio_tracks':0,'codec':'vp8','full_stream_decode':'PASS'}
