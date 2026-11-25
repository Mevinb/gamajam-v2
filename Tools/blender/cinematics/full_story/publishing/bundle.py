#!/usr/bin/env python3
"""Create/recheck a lake-four or final-five evidence bundle. Does not copy anything into Assets."""
import argparse, datetime
from common import *
from validate_frames import load_contract,validate_film
from movies import validate_movie

def names_for(group):
    require(group in ['lake','full'],'Unknown publication group')
    return LAKE if group=='lake' else tuple(FILMS)

def script_hashes():
    return {p.name:sha(p) for p in sorted(Path(__file__).parent.glob('*.py'))}

def movie_gate(directory,production,contract_path,name):
    record=read_json(directory/(name+'.encode.json'))
    require(record.get('schema')==1 and record.get('kind')=='completed-real-full-film-encode' and record.get('status')=='PASS','No completed encode report: '+name)
    current_contract=load_contract(contract_path)
    require(record.get('render_contract_sha256')==render_digest(current_contract) and Path(record.get('production_root','')).resolve()==production,'Encode belongs to different rendered content/inputs')
    with film_lock(production/name): inputs=validate_film(production,contract_path,name)
    require(record['inputs']==inputs,'Renderer input hashes changed since encoding: '+name)
    movie=validate_movie(directory/(name+'.webm'),name)
    require(record['movie']==movie,'Encoded movie bytes/metadata changed: '+name)
    return {'movie':movie,'inputs':inputs,'encode_report_sha256':sha(directory/(name+'.encode.json'))}

def finalize(directory,production,contract_path,group):
    directory=below(directory,PROJECT/'Logs');production=below(production,PROJECT/'Logs/full-cinematic-renders')
    contract=load_contract(contract_path);verify_sources(contract['plan'],contract['sources']['manifest_sha256'])
    names=names_for(group);movies={name:movie_gate(directory,production,contract_path,name) for name in names}
    result={'schema':1,'kind':'reviewable-full-film-publication-bundle','group':group,'status':'PASS','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'production_root':str(production),'contract_path':str(Path(contract_path).resolve()),'contract_sha256':sha(contract_path),'movies':movies,'scripts':script_hashes(),
        'has_all_five_validated_encodes':group=='full','scope':'Exact source/frame/quality evidence and actual silent VP8 stream checks. Visual continuity/audio synchronization and actual Unity/UI acceptance remain separate root gates.'}
    path=directory/('bundle-'+group+'.json');atomic_json(path,result);print('BUNDLE_PASS',group,path,'sha256='+sha(path));return path

def recheck(path):
    path=below(path,PROJECT/'Logs');data=read_json(path)
    require(data.get('schema')==1 and data.get('kind')=='reviewable-full-film-publication-bundle' and data.get('status')=='PASS','Invalid reviewable bundle')
    names=names_for(data['group']);require(set(data.get('movies',{}))==set(names),'Publication bundle is not its complete required group')
    require(data.get('scripts')==script_hashes(),'Publication scripts changed after bundle review')
    contract_path=Path(data['contract_path']);require(sha(contract_path)==data['contract_sha256'],'Frozen contract changed')
    contract=load_contract(contract_path);verify_sources(contract['plan'],contract['sources']['manifest_sha256'])
    production=below(data['production_root'],PROJECT/'Logs/full-cinematic-renders')
    for name in names:
        require(movie_gate(path.parent,production,contract_path,name)==data['movies'][name],'Bundle evidence changed: '+name)
    return data

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--directory',required=True);p.add_argument('--production',required=True);p.add_argument('--contract',required=True);p.add_argument('--group',choices=['lake','full'],required=True)
    a=p.parse_args();finalize(a.directory,a.production,a.contract,a.group)
