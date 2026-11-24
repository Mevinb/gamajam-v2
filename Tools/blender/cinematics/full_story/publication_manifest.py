"""Explicit source publication allowlist; no staging, committing, render output or log globs."""
import sys
sys.dont_write_bytecode=True
import argparse,datetime,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
SOURCE=Path('ArtSource/FullCinematics')
TOOLS=Path('Tools/blender/cinematics/full_story')
MANIFEST=SOURCE/'source-publication-manifest.json'
PATHSPEC=SOURCE/'source-publication.paths'

TOOL_FILES=[
 'RENDER_DRIVER.md','audit_opening.py','build_opening.sh','common.py','export_captions.py','opening_scene.py','plan.py',
 'render_policy.py','render_story.py','test_render_policy.py','smoke_driver.py','publication_manifest.py',
 'lake_story/README.md','lake_story/author_lake.py','lake_story/make_storyboard.py',
 'motion/__init__.py','motion/build_library.py','motion/build_speech.py','motion/draw_poses.py','motion/draw_speech.py',
 'motion/library.py','motion/speech.py','motion/verify_standalone.py',
]
SOURCE_FILES=[
 'README.md','STATUS.md','driver-smoke-evidence.json',
 'Opening/OpeningFull.blend','Opening/authoring-audit.json','Opening/opening-captions-review.srt',
 'Opening/opening-timeline.json','Opening/preview-evidence.json',
 'Opening/Review/02-petrov.png','Opening/Review/03-recognition.png','Opening/Review/05-two-bodies.png',
 'Opening/Review/06-contact.png','Opening/Review/06-contact-fall.png','Opening/Review/08-interlock.png',
 'Opening/Review/08-interlock-escape.png','Opening/Review/09-conduit.png','Opening/Review/10-outside.png',
 'Lake/DELIVERY.md','Lake/PROVENANCE.md','Lake/manifest.json','Lake/audit.json','Lake/preview_verification.json',
 'Lake/Lake_Storyboard_CPU.png','Lake/Lake_Arrival.blend','Lake/Lake_Hold.blend','Lake/Lake_Peace.blend','Lake/Lake_Dominion.blend',
 'Lake/previews/arrival_0001_cpu.png','Lake/previews/arrival_0144_cpu.png','Lake/previews/arrival_0264_cpu.png',
 'Lake/previews/arrival_0360_cpu.png','Lake/previews/arrival_evidence.json',
 'Lake/previews/hold_0001_cpu.png','Lake/previews/hold_0073_cpu.png','Lake/previews/hold_0144_cpu.png','Lake/previews/hold_evidence.json',
 'Lake/previews/peace_0040_cpu.png','Lake/previews/peace_0102_cpu.png','Lake/previews/peace_0168_cpu.png','Lake/previews/peace_evidence.json',
 'Lake/previews/dominion_0048_cpu.png','Lake/previews/dominion_0096_cpu.png','Lake/previews/dominion_0168_cpu.png','Lake/previews/dominion_evidence.json',
 'Motion/README.md','Motion/manifest.json','Motion/UAL_story_source.blend','Motion/Actors_story_actions.blend',
 'Motion/verification.json','Motion/standalone_verification.json','Motion/pose_diagrams.json',
 'Motion/opening_speech_envelopes.json','Motion/speech_verification.json',
 'Motion/DrPetrov_pose_verification.png','Motion/MaintEli_pose_verification.png','Motion/opening_speech_envelopes.png',
 'Motion/DrPetrov_pose_verification.svg','Motion/MaintEli_pose_verification.svg',
]
ALLOWLIST=sorted([TOOLS/entry for entry in TOOL_FILES]+[SOURCE/entry for entry in SOURCE_FILES])


def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def category(path):
    if path.suffix=='.blend':return 'packed_scene_or_motion_library'
    if path.suffix in {'.py','.sh'}:return 'authoring_or_verification_module'
    if path.suffix in {'.png','.svg'}:return 'intentional_small_review_image'
    if path.suffix=='.md':return 'documentation'
    if path.suffix=='.srt':return 'preserved_caption_timing'
    if path.name=='opening_speech_envelopes.json':return 'source_measured_speech_performance'
    return 'audit_manifest_or_provenance'


def rows():
    result=[]
    for relative in ALLOWLIST:
        path=ROOT/relative
        if not path.is_file():raise RuntimeError('Missing intentional source artifact: '+str(relative))
        if not path.resolve().is_relative_to(ROOT):raise RuntimeError('Publication path leaves project')
        if path.suffix in {'.log','.pyc','.blend1'} or any(part in {'working','Renders','__pycache__'} for part in relative.parts):
            raise RuntimeError('Working/log/cache path in publication allowlist')
        size=path.stat().st_size
        if size>=100_000_000:raise RuntimeError('Source exceeds individual GitHub file limit: '+str(relative))
        result.append({'path':relative.as_posix(),'category':category(relative),'bytes':size,'sha256':sha(path)})
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');args=parser.parse_args()
    current=rows()
    expected_paths='\n'.join([MANIFEST.as_posix(),PATHSPEC.as_posix()]+[row['path'] for row in current])+'\n'
    if args.check:
        saved=json.loads((ROOT/MANIFEST).read_text())
        if saved['files']!=current:raise RuntimeError('Publication file content/allowlist changed; review and regenerate manifest')
        if (ROOT/PATHSPEC).read_text()!=expected_paths:raise RuntimeError('Publication pathspec differs from allowlist')
        print('Publication manifest verified:',len(current),'intentional files;',saved['total_bytes'],'bytes');return
    audit=json.loads((ROOT/SOURCE/'Opening/authoring-audit.json').read_text())
    smoke=json.loads((ROOT/SOURCE/'driver-smoke-evidence.json').read_text())
    if audit['status']!='passed' or smoke['status']!='passed':raise RuntimeError('Authoring and actual driver smoke must pass before publication')
    data={'schema':1,'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'publication_state':'full_story_authoring_sources',
          'final_movies_rendered':False,'manifest_path':MANIFEST.as_posix(),'pathspec_path':PATHSPEC.as_posix(),
          'opening_source_sha256':audit['source_sha256'],'driver_smoke_source_sha256':smoke['source_sha256'],
          'excluded_categories':['authoring logs','temporary or reference-only experiments','render working frames','render caches','Python bytecode'],
          'total_bytes':sum(row['bytes'] for row in current),'largest_file_bytes':max(row['bytes'] for row in current),
          'file_count':len(current),'files':current}
    (ROOT/MANIFEST).write_text(json.dumps(data,indent=2)+'\n');(ROOT/PATHSPEC).write_text(expected_paths)
    print('Prepared',len(current),'intentional files;',data['total_bytes'],'bytes; largest',data['largest_file_bytes'])


if __name__=='__main__':main()
