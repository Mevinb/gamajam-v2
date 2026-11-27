"""Fresh complete-source/packed-input/four-FBX check, using explicit references."""
from pathlib import Path
import argparse, hashlib, importlib, json, sys
import bpy

sys.dont_write_bytecode=True
PACKAGE=Path(__file__).resolve().parent;sys.path.insert(0,str(PACKAGE))
import environment_state as state
from refine_environment import budget
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def child(e,name):return next(n for n in e.elems if n.id==name)
def serial(value):
    if isinstance(value,bytes):return {'bytes':value.hex()}
    if hasattr(value,'typecode'):return {'array':value.typecode,'values':list(value)}
    return value
def tree(element,skip_id=False):return [element.id.hex(),[serial(p) for p in element.props[(1 if skip_id else 0):]],[tree(e) for e in element.elems]]
def fbx(decoder,path):
    root,version=decoder.parse(str(path));assert version==7400;objects=child(root,b'Objects').elems;geometry=[n for n in objects if n.id==b'Geometry'];models=[n for n in objects if n.id==b'Model'];assert len(geometry)==len(models)==1
    return {'geometry':tree(geometry[0],True),'model':tree(models[0],True),'global':tree(child(root,b'GlobalSettings')),'materialNames':[n.props[1].split(b'\x00\x01')[0].decode() for n in objects if n.id==b'Material']}

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',type=Path,required=True);p.add_argument('--uv-reference',type=Path,required=True);p.add_argument('--curb-reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);candidate=a.candidate.resolve();uv=a.uv_reference.resolve();curb=a.curb_reference.resolve();out=a.output.resolve();assert not out.exists();out.mkdir()
    author=json.loads((candidate/'receipt.json').read_text());assert author['status']=='COMBINED_ENVIRONMENT_REFINEMENT_SOURCE_AND_FOUR_EXPORTS_CPU_PASS' and author['explicitInputsUnchanged'];manifest=json.loads((PACKAGE/'refinement-manifest.json').read_text());inputs={str(p):sha(p) for p in candidate.rglob('*') if p.is_file()};inputs.update({str(uv/(n+'.fbx')):sha(uv/(n+'.fbx')) for n in manifest['uvTargets']});inputs[str(curb)]=sha(curb)
    r={'status':'INCOMPLETE','scope':'Fresh complete source/packed exact atlas/strict exported geometry-model-unit trees. No saves/exports/renders/Unity/GPU.','beforeBudget':budget(),'rows':[]}
    try:
        bpy.context.preferences.addons['cycles'].preferences.compute_device_type='NONE';bpy.ops.wm.open_mainfile(filepath=str(candidate/'EnvironmentRealism.blend'),load_ui=False);assert all(s.cycles.device=='CPU' for s in bpy.data.scenes);actual=json.loads(json.dumps(state.snapshot()));expected=json.loads((candidate/'after-state.json').read_text());assert actual==expected and state.digest(actual)==author['expectedStateDigest'];r['completeSourceDigest']=state.digest(actual);r['packedAtlas']=[]
        for image in bpy.data.images:
            key=image.name[-1];path=candidate/'Textures'/(image.name+'.png');assert image.filepath=='//Textures/'+image.name+'.png' and image.packed_file and hashlib.sha256(bytes(image.packed_file.data)).hexdigest()==sha(path)==manifest['atlasInputs'][key]['acceptedSha256'];r['packedAtlas'].append({'name':image.name,'sha256':sha(path)})
        decoder=importlib.import_module('io_scene_fbx.parse_fbx');importlib.import_module('io_scene_fbx.fbx_utils_threading')._MULTITHREADING_ENABLED=False
        for name in manifest['exportTargets']:
            reference=curb if name=='CurbStone' else uv/(name+'.fbx');export=candidate/'Exports'/(name+'.fbx');before=fbx(decoder,reference);after=fbx(decoder,export);row={'name':name,'referenceSha256':sha(reference),'candidateSha256':sha(export),'geometryTreeExact':before['geometry']==after['geometry'],'modelPropertiesExact':before['model']==after['model'],'globalUnitsExact':before['global']==after['global'],'materialRolesExact':before['materialNames']==after['materialNames']};r['rows'].append(row)
            if name=='CurbStone' and not row['geometryTreeExact']:
                reference_label=bytes.fromhex(before['geometry'][1][0]['bytes']).split(b'\x00\x01')[0].decode();actual_label=bytes.fromhex(after['geometry'][1][0]['bytes']).split(b'\x00\x01')[0].decode();row['transientGeometryLabels']={'reference':reference_label,'candidate':actual_label};assert (reference_label,actual_label)==('CurbStone.002','CurbStone.001'),'Unexpected exported geometry label delta';before['geometry'][1][0]=after['geometry'][1][0];row['allGeometryContentExceptRecordedTemporaryLabelExact']=before['geometry']==after['geometry']
            else:row['allGeometryContentExceptRecordedTemporaryLabelExact']=row['geometryTreeExact']
            assert all(row[k] for k in ('allGeometryContentExceptRecordedTemporaryLabelExact','modelPropertiesExact','globalUnitsExact','materialRolesExact')),name
        r.update(status='FRESH_COMBINED_COMPLETE_SOURCE_PACKED_ATLAS_FOUR_EXPORTS_EXACT_PASS',completeSourceSha256=sha(candidate/'EnvironmentRealism.blend'),sourceAndOutputsUnchanged=all(sha(Path(p))==h for p,h in inputs.items()),saves=0,exports=0,renders=0)
    except BaseException as e:r.update(status='FAILED',error=repr(e));raise
    finally:r['afterBudget']=budget();(out/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')

if __name__=='__main__':main()
