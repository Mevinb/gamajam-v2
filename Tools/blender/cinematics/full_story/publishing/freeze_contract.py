#!/usr/bin/env python3
"""Freeze approved metadata/source expectations without loading Blender or rendering/encoding anything."""
import argparse, copy, datetime
from common import *

def freeze(plan_path,opening_profile,lake_profile,out):
    plan=normalize_plan(read_json(plan_path));source_check=verify_sources(plan);cuts=schedules()
    sources={row['name']:row for row in plan['sources']}
    opening_data=read_json(opening_profile);opening_source=copy.deepcopy(sources['OpeningFull']);revision_chain=None
    if opening_data.get('source_sha256')!=opening_source['sha256']:
        revision_path=project_file('ArtSource/FullCinematics/Opening/camera04-revision.json');revision=read_json(revision_path)
        pilot_path=project_file('ArtSource/FullCinematics/Opening/rtx-denoise-pilot.json');pilot=read_json(pilot_path)
        require(revision.get('base_source_sha256')==opening_data.get('source_sha256') and revision.get('revised_source_sha256')==opening_source['sha256'],'Unapproved opening quality-template source revision')
        require(revision.get('timing_seconds')==[19,26] and revision.get('scope')=='camera 04 and focus only; actors/materials/other cameras/captions unchanged','Quality template reuse is limited to the approved camera04-only revision')
        require(pilot.get('source_sha256')==revision['base_source_sha256'] and pilot.get('quality')==[1920,1080,48] and pilot.get('status')=='complete','Opening A/B quality reference lacks its completed audit')
        revision_chain={'path':str(revision_path.relative_to(PROJECT)),'sha256':sha(revision_path),'pilot_sha256':sha(pilot_path),'base_source_sha256':revision['base_source_sha256'],'production_source_sha256':revision['revised_source_sha256'],'scope':revision['scope']}
        opening_source['sha256']=revision['base_source_sha256']
    opening=reference_profile(opening_data,plan,opening_source,cuts['OpeningFull'])
    lake=reference_profile(read_json(lake_profile),plan,sources['LakeArrival'],cuts['LakeArrival'])
    require(opening['devices']==lake['devices'],'Approved profiles use different GPU identities')
    expected={}
    for name in FILMS:
        profile=copy.deepcopy(opening if name=='OpeningFull' else lake)
        profile['source_sha256']=sources[name]['sha256'];profile['scene_frame_range']=[1,FILMS[name]];profile['camera_schedule']=cuts[name]
        expected[name]=profile
    result={'schema':1,'kind':'root-approved-full-film-evidence-contract','created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'plan':plan,'sources':source_check,'references':{'opening':{'path':str(Path(opening_profile).resolve()),'sha256':sha(opening_profile),'template_source_sha256':opening_data['source_sha256'],'camera_revision_chain':revision_chain},'lake':{'path':str(Path(lake_profile).resolve()),'sha256':sha(lake_profile)}},
        'expected_provenance':expected,'limits':'Checks evidence consistency against approved packed sources/driver/timelines; does not independently re-execute camera rigs or certify visual seam/audio synchronization.'}
    result['render_contract_sha256']=render_digest(result)
    out=below(out,PROJECT/'Logs');atomic_json(out,result)
    print('FROZEN_CONTRACT',out,'sha256='+sha(out))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',required=True);p.add_argument('--opening-profile',required=True);p.add_argument('--lake-profile',required=True);p.add_argument('--out',required=True)
    a=p.parse_args();freeze(a.plan,a.opening_profile,a.lake_profile,a.out)
