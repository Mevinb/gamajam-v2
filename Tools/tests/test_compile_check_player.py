import importlib.util,json,tempfile,time,os,unittest
from pathlib import Path
HERE=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('playercheck',HERE/'compile_check_player.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class CacheContractTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);self.project=self.root/'project';self.editor=self.root/'unity/Editor/Data'
  self.rsp=self.project/'Library/Bee/artifacts/abcP.dag/Parasite.Runtime.rsp';self.meta=self.project/'Library/Bee/abcP-inputdata.json'
  self.engine=self.editor/'PlaybackEngines/LinuxStandaloneSupport/Variations/mono/Managed/UnityEngine.CoreModule.dll';self.package=self.rsp.parent/'Example.ref.dll'
  self.files={self.project/'ProjectSettings/ProjectVersion.txt':'m_EditorVersion: 6000.3.29f1\n',self.project/'Packages/manifest.json':'{}',self.project/'Packages/packages-lock.json':'{}',self.project/'Assets/Runtime.asmdef':'{}',self.engine:'engine-api-fixture',self.package:'package-api-fixture'}
  for p,s in self.files.items():p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s)
  self.lines=[f'-r:"{self.engine}"',f'-r:"{self.package}"','-define:UNITY_STANDALONE_LINUX','-define:ENABLE_MONO','-out:"discarded.dll"','-analyzer:"discarded-generator.dll"','Assets/RemovedHistoricalSource.cs']
  self.rsp.write_text('\n'.join(self.lines)+'\n')
  self.data={'BeeBuildProgramCommon.Data.ConfigurationData':{'UnityVersion':'6000.3.29f1','EditorContentsPath':str(self.editor),'Packages':[]},'ScriptCompilationBuildProgram.Data.ScriptCompilationData':{'BuildTarget':'StandaloneLinux64','Assemblies':[{'Asmdef':'Assets/Runtime.asmdef'}]}}
  self.save_meta();self.stamp=time.time_ns()+1_000_000_000;os.utime(self.meta,ns=(self.stamp,self.stamp))
 def save_meta(self):self.meta.write_text(json.dumps(self.data))
 def run_cache(self):return module.validate_cache(self.project,self.editor,self.rsp)
 def test_current_linux_cache_only_reuses_references_and_defines(self):
  opts,_=self.run_cache();self.assertEqual(len(opts),4);self.assertFalse(any('discarded' in x or 'RemovedHistorical' in x for x in opts))
 def test_missing_reference_fails(self):
  self.package.unlink()
  with self.assertRaisesRegex(module.InvalidCache,'Missing player reference'):self.run_cache()
 def test_editor_reference_fails(self):
  bad=self.editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll';bad.parent.mkdir(parents=True);bad.write_text('editor')
  self.rsp.write_text('\n'.join(self.lines+[f'-r:"{bad}"']))
  with self.assertRaisesRegex(module.InvalidCache,'Editor reference'):self.run_cache()
 def test_editor_define_fails(self):
  self.rsp.write_text('\n'.join(self.lines+['-define:UNITY_EDITOR_LINUX']))
  with self.assertRaisesRegex(module.InvalidCache,'Editor defines'):self.run_cache()
 def test_other_backend_fails(self):
  self.rsp.write_text('\n'.join(self.lines+['-define:ENABLE_IL2CPP']))
  with self.assertRaisesRegex(module.InvalidCache,'Linux Mono'):self.run_cache()
 def test_other_platform_fails(self):
  self.data['ScriptCompilationBuildProgram.Data.ScriptCompilationData']['BuildTarget']='StandaloneWindows64';self.save_meta()
  with self.assertRaisesRegex(module.InvalidCache,'not Linux'):self.run_cache()
 def test_unity_version_mismatch_fails(self):
  self.data['BeeBuildProgramCommon.Data.ConfigurationData']['UnityVersion']='6000.2.1f1';self.save_meta()
  with self.assertRaisesRegex(module.InvalidCache,'Unity version differs'):self.run_cache()
 def test_package_manifest_lock_asmdef_and_unity_version_freshness(self):
  for relative in ['Packages/manifest.json','Packages/packages-lock.json','Assets/Runtime.asmdef','ProjectSettings/ProjectVersion.txt']:
   with self.subTest(relative=relative):
    p=self.project/relative;old=p.stat().st_mtime_ns;os.utime(p,ns=(self.stamp+1,self.stamp+1))
    with self.assertRaisesRegex(module.InvalidCache,'stale after dependency change'):self.run_cache()
    os.utime(p,ns=(old,old))
 def test_virtual_package_asmdef_uses_recorded_physical_cache(self):
  real=self.project/'Library/PackageCache/com.example@hash/Runtime/Example.asmdef';real.parent.mkdir(parents=True);real.write_text('{}');os.utime(real,ns=(self.stamp-1,self.stamp-1))
  self.data['BeeBuildProgramCommon.Data.ConfigurationData']['Packages']=[{'Name':'com.example','ResolvedPath':'Library/PackageCache/com.example@hash'}]
  self.data['ScriptCompilationBuildProgram.Data.ScriptCompilationData']['Assemblies'].append({'Asmdef':'Packages/com.example/Runtime/Example.asmdef'});self.save_meta();os.utime(self.meta,ns=(self.stamp,self.stamp));self.run_cache()
 def test_missing_cache_has_clear_failure_without_editor_fallback(self):
  self.rsp.unlink()
  with self.assertRaisesRegex(module.InvalidCache,'no Editor fallback'):module.choose_cache(self.project,self.editor)
 def test_malformed_reference_is_rejected(self):
  self.rsp.write_text('-r:"unterminated\n')
  with self.assertRaisesRegex(module.InvalidCache,'Malformed'):self.run_cache()

if __name__=='__main__':unittest.main(verbosity=2)
