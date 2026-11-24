"""Fast policy checks only. These do not import Blender or create a render job."""
import copy,sys,unittest
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import render_policy as policy


def marker(frame,name,camera,**extra):
    return dict(frame=frame,marker=name,camera=camera,camera_type='CAMERA',linked_to_scene=True,**extra)


class RenderPolicyTests(unittest.TestCase):
    def setUp(self):
        self.schedule=policy.camera_schedule([marker(1,'Arrival','Wide'),marker(121,'Face','Close'),
                                               dict(frame=60,marker='Sound cue',camera=None)],1)

    def test_output_caches_are_confined_to_ignored_logs(self):
        project=Path(__file__).resolve().parents[4]
        allowed=project/'Logs/full-cinematic-renders/opening'
        self.assertEqual(allowed.resolve(),policy.output_path(project,allowed))
        for unsafe in [project/'ArtSource/FullCinematics/Renders',project/'Assets/Films',project/'Logs/full-cinematic-renders',
                       project/'Logs/full-cinematic-renders/../../outside']:
            with self.subTest(path=unsafe),self.assertRaises(ValueError):policy.output_path(project,unsafe)

    def test_requested_bounds_and_samples_are_explicit_and_valid(self):
        policy.validate_request(1,144,48,1,144)
        for values in [(0,24,48,1,144),(1,145,48,1,144),(24,1,48,1,144),(1,24,0,1,144),
                       (1,24,-1,1,144),(1,24,4097,1,144),(True,24,48,1,144),(1,24,8.5,1,144)]:
            with self.subTest(values=values),self.assertRaises(ValueError):policy.validate_request(*values)

    def test_camera_cut_uses_packed_binding_at_exact_frame(self):
        self.assertEqual('Wide',policy.camera_at_frame(self.schedule,1)['camera'])
        self.assertEqual('Wide',policy.camera_at_frame(self.schedule,120)['camera'])
        self.assertEqual('Close',policy.camera_at_frame(self.schedule,121)['camera'])
        self.assertEqual('Close',policy.camera_at_frame(self.schedule,200)['camera'])
        with self.assertRaises(ValueError):policy.camera_at_frame(self.schedule,0)

    def test_missing_ambiguous_or_unlinked_cameras_fail(self):
        cases=[[],[marker(2,'Late','Wide')],[marker(1,'A','Wide'),marker(1,'B','Close')],
               [dict(frame=1,marker='Bad',camera='Lamp',camera_type='LIGHT',linked_to_scene=True)],
               [dict(frame=1,marker='Bad',camera='Wide',camera_type='CAMERA',linked_to_scene=False)]]
        for markers in cases:
            with self.subTest(markers=markers),self.assertRaises(ValueError):policy.camera_schedule(markers,1)

    def test_resume_rejects_changed_driver_blender_backend_or_sampling(self):
        original={'driver_sha256':'a','policy_sha256':'b','source_sha256':'c','blender':{'version':[4,5,16]},
                  'backend':'OPTIX','devices':[{'name':'RTX4050','id':'gpu0'}],
                  'sampling':{'samples':48,'denoiser':'OPENIMAGEDENOISE','denoising_use_gpu':False},
                  'camera_schedule':self.schedule}
        policy.require_same_provenance(original,copy.deepcopy(original))
        changes=[('driver_sha256','new'),('policy_sha256','new'),('source_sha256','new'),
                 ('blender',{'version':[4,5,17]}),('backend','CPU'),('devices',[{'name':'Other GPU'}]),
                 ('sampling',{'samples':64,'denoiser':'OPENIMAGEDENOISE','denoising_use_gpu':False}),
                 ('sampling',{'samples':48,'denoiser':'OPTIX','denoising_use_gpu':True}),
                 ('camera_schedule',[marker(1,'Different','Other')])]
        for key,value in changes:
            altered=copy.deepcopy(original);altered[key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):policy.require_same_provenance(original,altered)

    def test_checkpoint_records_must_match_camera_and_real_image_digest(self):
        valid={'frame':121,'camera':'Close','camera_marker':'Face','camera_marker_frame':121,'bytes':1000,'sha256':'a'*64}
        policy.validate_record(valid,121,self.schedule)
        for key,value in [('frame',120),('camera','Wide'),('camera_marker','Arrival'),('camera_marker_frame',1),
                          ('bytes',0),('bytes',True),('sha256','z'*64),('sha256','short')]:
            altered=dict(valid);altered[key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):policy.validate_record(altered,121,self.schedule)

    def test_provenance_digest_is_order_independent_but_finite(self):
        self.assertEqual(policy.digest({'a':1,'b':2}),policy.digest({'b':2,'a':1}))
        with self.assertRaises(ValueError):policy.digest({'sample_threshold':float('nan')})


if __name__=='__main__':unittest.main()
