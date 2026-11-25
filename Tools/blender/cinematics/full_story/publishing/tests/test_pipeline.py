"""Pure metadata/command/atomic-filesystem tests; no movies, render frames, encoders or GPU jobs are created."""
import copy,json,sys,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import common,encode,publish
from validate_frames import validate_film

SCHEDULE=[{'frame':1,'marker':'First cut','camera':'First camera'},{'frame':121,'marker':'Second cut','camera':'Second camera'}]
def record(number=1):
    row=common.at_frame(SCHEDULE,number)
    return {'frame':number,'bytes':100,'sha256':'a'*64,'camera':row['camera'],'camera_marker':row['marker'],'camera_marker_frame':row['frame'],
        'camera_world_matrix':[[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]],'camera_lens_mm':50,'camera_sensor_mm':[36,24],
        'dof':{'enabled':True,'fstop':5.6,'focus_object':None,'focus_distance':3},'seconds':5}
def plan():
    return {'samples':48,'resolution':[1920,1080],'fps':24,'ray_backend':'OPTIX','denoiser':'OPENIMAGEDENOISE','denoise_gpu':True,
        'driver_sha256':'a'*64,'policy_sha256':'b'*64,'total_frames':2760,
        'sources':[{'name':name,'source':'ArtSource/'+name+'.blend','frames':frames,'sha256':'c'*64} for name,frames in common.FILMS.items()]}

class EvidenceTests(unittest.TestCase):
    def test_exact_cut_boundary(self):
        self.assertEqual(common.at_frame(SCHEDULE,120)['camera'],'First camera')
        self.assertEqual(common.at_frame(SCHEDULE,121)['camera'],'Second camera')
        common.validate_record(record(120),120,SCHEDULE);common.validate_record(record(121),121,SCHEDULE)

    def test_wrong_frame_and_camera_refused(self):
        for key,value in [('frame',True),('camera','Wrong'),('camera_marker','Wrong'),('camera_marker_frame',0),('bytes',0),('sha256','bad')]:
            with self.subTest(key=key):
                data=record();data[key]=value
                with self.assertRaises(common.InvalidEvidence):common.validate_record(data,1,SCHEDULE)

    def test_nonfinite_or_invalid_pose_refused(self):
        for key,value in [('camera_world_matrix',[[1,0,0,0],[0,float('nan'),0,0],[0,0,1,0],[0,0,0,1]]),('camera_lens_mm',float('inf')),('camera_sensor_mm',[0,24]),('seconds',-1)]:
            with self.subTest(key=key):
                data=record();data[key]=value
                with self.assertRaises(common.InvalidEvidence):common.validate_record(data,1,SCHEDULE)

    def test_render_plan_is_full_and_high_quality(self):
        baseline=common.normalize_plan(plan());self.assertEqual(sum(row['frames'] for row in baseline['sources']),2760)
        for key,value in [('samples',8),('denoise_gpu',False),('resolution',[1280,720]),('ray_backend','CPU')]:
            data=plan();data[key]=value
            with self.assertRaises(common.InvalidEvidence):common.normalize_plan(data)
        data=plan();data['sources'][0]['frames']=24
        with self.assertRaises(common.InvalidEvidence):common.normalize_plan(data)

    def test_incomplete_supervisor_cannot_start_encoder(self):
        with tempfile.TemporaryDirectory(prefix='encoder-negative-test-',dir=common.PROJECT/'Logs') as temporary:
            base=Path(temporary);production=base/'production';folder=production/'LakeArrival';folder.mkdir(parents=True)
            # Validator path confinement remains active: this is moved beneath the approved root only for the fixture.
            with patch('encode.below',side_effect=lambda p,*args:Path(p).resolve()),patch('validate_frames.below',side_effect=lambda p,*args:Path(p).resolve()):
                common.atomic_json(production/'production-plan.json',plan());common.atomic_json(production/'production-summary.json',{'plan':plan(),'jobs':[]})
                (folder/'.render.lock').touch();contract=base/'contract.json'
                data={'schema':1,'kind':'root-approved-full-film-evidence-contract','plan':common.normalize_plan(plan()),'sources':{'manifest_sha256':'a'*64},'expected_provenance':{name:{} for name in common.FILMS}}
                data['render_contract_sha256']=common.render_digest(data);common.atomic_json(contract,data)
                with patch('encode.verify_sources'),patch('encode.subprocess.run') as process:
                    with self.assertRaisesRegex(common.InvalidEvidence,'Supervisor has not completed'):encode.encode(production,contract,'LakeArrival',base/'movies')
                    process.assert_not_called();self.assertFalse((base/'movies').exists())

    def test_encoding_recipe_preserves_frames_silence_and_threads(self):
        recipe=encode.commands(Path('/frames'),'OpeningFull',1920,Path('/work'),Path('/work/OpeningFull.webm'),8_000_000)
        self.assertEqual(len(recipe),2)
        for command in recipe:
            self.assertIn('-an',command);self.assertEqual(command[command.index('-frames:v')+1],'1920')
            self.assertEqual(command[command.index('-framerate')+1],'24');self.assertIn('libvpx',command)
            self.assertEqual(command[command.index('-threads')+1],'2');self.assertEqual(command[command.index('-filter_threads')+1],'2')
            self.assertFalse(any(value in command for value in ['-f','lavfi']) and 'lavfi' in command)

    def test_lake_gate_and_full_gate_are_distinct(self):
        from bundle import names_for
        self.assertEqual(len(names_for('lake')),4);self.assertNotIn('OpeningFull',names_for('lake'))
        self.assertEqual(set(names_for('full')),set(common.FILMS))

    def test_documentation_refresh_does_not_relabel_rendered_content(self):
        first={'plan':common.normalize_plan(plan()),'expected_provenance':{'source_sha256':'c'*64},'sources':{'manifest_sha256':'old'},'created_utc':'earlier'}
        second=copy.deepcopy(first);second['sources']['manifest_sha256']='new';second['created_utc']='later'
        self.assertEqual(common.render_digest(first),common.render_digest(second))
        second['expected_provenance']['source_sha256']='d'*64
        self.assertNotEqual(common.render_digest(first),common.render_digest(second))

    def test_atomic_exchange_keeps_both_versions(self):
        with tempfile.TemporaryDirectory(prefix='publish-exchange-test-',dir=common.PROJECT/'Logs') as temporary:
            base=Path(temporary);a=base/'candidate';b=base/'existing';a.mkdir();b.mkdir();(a/'proof.txt').write_text('new');(b/'proof.txt').write_text('old')
            publish.exchange(a,b)
            self.assertEqual((b/'proof.txt').read_text(),'new');self.assertEqual((a/'proof.txt').read_text(),'old')

if __name__=='__main__':unittest.main()
