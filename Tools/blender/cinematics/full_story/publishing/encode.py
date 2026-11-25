#!/usr/bin/env python3
"""Encode exactly one completed real film on two CPU threads after all of its input evidence passes."""
import argparse, datetime, subprocess, tempfile, shutil
from common import *
from validate_frames import load_contract,validate_film
from movies import validate_movie

def commands(folder,name,count,work,output,bitrate):
    base=['ffmpeg','-hide_banner','-loglevel','warning','-nostdin','-y','-filter_threads','2','-threads','2','-framerate','24','-start_number','1','-i',str(folder/'frame-%06d.png'),
        '-map','0:v:0','-vf','scale=in_range=full:out_range=tv:out_color_matrix=bt709','-frames:v',str(count),'-an','-c:v','libvpx','-b:v',str(bitrate),'-deadline','good','-threads','2','-pix_fmt','yuv420p','-g','48',
        '-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-color_range','tv','-passlogfile',str(work/name)]
    return [base+['-cpu-used','4','-pass','1','-f','null',os.devnull],base+['-cpu-used','2','-pass','2',str(output)]]

def encode(production,contract_path,name,out,bitrate=8_000_000):
    production=below(production,PROJECT/'Logs/full-cinematic-renders');out=below(out,PROJECT/'Logs')
    require(not out.is_relative_to(production) and not production.is_relative_to(out),'Movie output must be separate from renderer inputs')
    require(1_000_000<=bitrate<=8_500_000,'Choose an explicit1–8.5Mbps bitrate; default8Mbps preserves95MB headroom for80s')
    contract=load_contract(contract_path);verify_sources(contract['plan'],contract['sources']['manifest_sha256'])
    folder=production/name
    with film_lock(folder):
        inputs=validate_film(production,contract_path,name)
        # Nothing above this line starts an encoder or creates a placeholder movie.
        out.mkdir(parents=True,exist_ok=True)
        destination=out/(name+'.webm');report=out/(name+'.encode.json')
        require(not destination.exists() and not report.exists(),'Refusing overwrite; use a new output directory or review existing encode')
        with tempfile.TemporaryDirectory(prefix='.'+name+'-encoding-',dir=out) as temporary:
            work=Path(temporary);candidate=work/(name+'.webm')
            recipe=commands(folder,name,FILMS[name],work,candidate,bitrate)
            for index,command in enumerate(recipe,1):
                with (out/(name+f'.pass{index}.log')).open('w') as log:
                    result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
                require(result.returncode==0,f'VP8 pass{index} failed; no movie published')
            movie=validate_movie(candidate,name)
            # A writer that bypassed the renderer lock still cannot silently alter a validated input set.
            after=validate_film(production,contract_path,name)
            require(after==inputs,'Input evidence changed during encoding')
            verify_sources(contract['plan'],contract['sources']['manifest_sha256'])
            os.replace(candidate,destination)
            payload={'schema':1,'kind':'completed-real-full-film-encode','status':'PASS','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'production_root':str(production),'contract_path':str(Path(contract_path).resolve()),'contract_sha256':sha(contract_path),'render_contract_sha256':render_digest(contract),'source_publication_sha256':contract['sources']['manifest_sha256'],'inputs':inputs,'movie':movie,
                'bitrate':bitrate,'commands':recipe,'ffmpeg':subprocess.check_output(['ffmpeg','-version'],text=True).splitlines()[0],
                'encoder_script_sha256':sha(__file__),'validator_script_sha256':sha(Path(__file__).with_name('movies.py'))}
            atomic_json(report,payload)
    print('ENCODE_PASS',name,movie['bytes'],'bytes',movie['sha256'])

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--production',required=True);p.add_argument('--contract',required=True);p.add_argument('--film',choices=list(FILMS),required=True);p.add_argument('--out',required=True);p.add_argument('--bitrate',type=int,default=8_000_000)
    a=p.parse_args();encode(a.production,a.contract,a.film,a.out,a.bitrate)
