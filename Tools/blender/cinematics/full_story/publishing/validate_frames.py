#!/usr/bin/env python3
"""Reject partial, preview, stale, corrupt or misbound renderer evidence. Never encodes a frame."""
import argparse
from common import *

def load_contract(path):
    contract=read_json(path)
    require(contract.get('schema')==1 and contract.get('kind')=='root-approved-full-film-evidence-contract','Unknown frozen contract')
    require(set(contract.get('expected_provenance',{}))==set(FILMS),'Frozen contract is missing films')
    require(contract.get('render_contract_sha256')==render_digest(contract),'Frozen render-content contract was edited')
    return contract

def validate_film(production,contract_path,name,verify_images=True):
    require(name in FILMS,'Unknown movie name')
    production=below(production,PROJECT/'Logs/full-cinematic-renders')
    contract=load_contract(contract_path);plan=contract['plan']
    require(normalize_plan(read_json(production/'production-plan.json'))==plan,'Production root belongs to a different frozen plan')
    summary=read_json(production/'production-summary.json')
    require(normalize_plan(summary['plan'])==plan,'Production supervisor plan differs')
    jobs=[row for row in summary.get('jobs',[]) if row.get('name')==name]
    require(len(jobs)==1 and jobs[0].get('exit_code')==0 and jobs[0].get('status')=='complete' and jobs[0].get('complete_frames')==FILMS[name],'Supervisor has not completed '+name)
    folder=production/name;require(folder.is_dir() and not folder.is_symlink(),'Invalid movie frame directory')
    count=FILMS[name];evidence_path=folder/f'render-evidence-000001-{count:06d}.json';evidence=read_json(evidence_path)
    require(evidence.get('schema')==2 and evidence.get('status')=='complete' and evidence.get('complete') is True and evidence.get('error') is None,'Renderer checkpoint is not complete: '+name)
    require(evidence.get('start')==1 and evidence.get('end')==count,'Wrong full-range checkpoint bounds')
    provenance=read_json(folder/'job-provenance.json');expected=contract['expected_provenance'][name]
    require(digest(provenance)==digest(expected) and digest(evidence.get('provenance',{}))==digest(expected),'Source/device/quality/color/camera provenance differs from frozen contract')
    require(evidence.get('provenance_sha256')==digest(expected),'Invalid provenance digest')
    source=next(row for row in plan['sources'] if row['name']==name)
    require(Path(evidence.get('source','')).resolve()==project_file(source['source']),'Frame evidence names a different packed source')
    records=evidence.get('frames',[])
    require([row.get('frame') for row in records]==list(range(1,count+1)),'Exact unique ordered frame set1..N is required')
    images={path.name for path in folder.glob('frame-*.png')}
    require(images=={f'frame-{number:06d}.png' for number in range(1,count+1)},'PNG frame set has gaps, extras or duplicates')
    for number,record in enumerate(records,1):
        validate_record(record,number,expected['camera_schedule'])
        if verify_images:verify_png(folder/f'frame-{number:06d}.png',record)
    return {'name':name,'frames':count,'production_root':str(production),'render_contract_sha256':render_digest(contract),'evidence_sha256':sha(evidence_path),'provenance_sha256':digest(expected),'frame_records_sha256':digest(records),'source_sha256':source['sha256'],'status':'PASS'}

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--production',required=True);p.add_argument('--contract',required=True);p.add_argument('--film',choices=list(FILMS),required=True);p.add_argument('--report',required=True)
    a=p.parse_args();contract=load_contract(a.contract);verify_sources(contract['plan'],contract['sources']['manifest_sha256'])
    folder=below(a.production,PROJECT/'Logs/full-cinematic-renders')/a.film
    with film_lock(folder): result=validate_film(a.production,a.contract,a.film)
    atomic_json(below(a.report,PROJECT/'Logs'),result);print('FRAME_GATE_PASS',a.film,result['frames'])
