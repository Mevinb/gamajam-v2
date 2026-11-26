#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Parasite.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    // Actual imported avatars/controller/clips, evaluated by Animator. CPU pose
    // evidence only; Root's independently baked images assess visible lip closure.
    public sealed class HumanoidNeutralJawTests
    {
        readonly List<Object> owned = new();
        static readonly int Close = Array.IndexOf(HumanTrait.MuscleName, "Jaw Close");
        static readonly int LeftRight = Array.IndexOf(HumanTrait.MuscleName, "Jaw Left-Right");
        static readonly EditorCurveBinding CloseBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "Jaw Close");
        static readonly EditorCurveBinding SideBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "Jaw Left-Right");
        const string Folder = "Assets/Parasite/Art/Characters/Animations/";

        static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Folder + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        static bool Supply(AnimationClip clip, bool sourceHasJaw, bool additive)
        {
            var importer = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Parasite.EditorTools.HumanAnimationImporter")).First(t => t != null);
            return (bool)importer.GetMethod("SupplyNeutralJawCurve", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { clip, sourceHasJaw, additive });
        }
        static object ImporterCall(string method, params object[] arguments)
        {
            var importer = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Parasite.EditorTools.HumanAnimationImporter")).First(t => t != null);
            return importer.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, arguments);
        }
        static bool SupplySource(AnimationClip clip, bool mapped = false, bool raw = false, bool custom = false, bool additive = false)
            => (bool)ImporterCall("SupplyNeutralJawCurveFromSource", clip, mapped, raw, custom, additive);
        AnimationClip GeneratedZeroPair()
        {
            var clip = CopyWithoutJaw();
            AnimationUtility.SetEditorCurve(clip, CloseBinding, AnimationCurve.Constant(0, clip.length, 0));
            AnimationUtility.SetEditorCurve(clip, SideBinding, AnimationCurve.Constant(0, clip.length, 0));
            return clip;
        }
        AnimationClip CopyWithoutJaw()
        {
            var copy = Object.Instantiate(Clip("Idle_Neutral")); owned.Add(copy);
            foreach (var binding in AnimationUtility.GetCurveBindings(copy))
                if (binding.type == typeof(Animator) && binding.propertyName.StartsWith("Jaw ", StringComparison.Ordinal)) AnimationUtility.SetEditorCurve(copy, binding, null);
            return copy;
        }
        HumanoidRig Actor(string id)
        {
            var entry = CharacterCatalog.Load().FindById(id); Assert.That(entry, Is.Not.Null);
            var root = new GameObject("Actual jaw regression " + id); owned.Add(root);
            var rig = root.AddComponent<HumanoidRig>(); rig.VariantOverride = id; rig.AmbientGestures = false;
            rig.Build(entry.profession, 731, id == "DrVale"); rig.enabled = false;
            Assert.That(rig.UsesAnimator && rig.Animator.avatar && rig.Animator.avatar.isHuman, Is.True);
            rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rig.Animator.SetBool(HumanAnimParams.Grounded, true); rig.Animator.SetInteger(HumanAnimParams.Profession, (int)Profession.Civilian);
            rig.Animator.SetInteger(HumanAnimParams.State, (int)HostState.Normal); rig.Animator.SetInteger(HumanAnimParams.Pose, (int)ActorPose.None);
            rig.Animator.SetFloat(HumanAnimParams.Speed, 0); rig.Animator.SetFloat(HumanAnimParams.Variant, 0);
            return rig;
        }
        static HumanPose Pose(HumanoidRig rig)
        {
            var pose = new HumanPose(); using var handler = new HumanPoseHandler(rig.Animator.avatar, rig.Animator.transform);
            handler.GetHumanPose(ref pose); return pose;
        }
        static void Idle(HumanoidRig rig, float normalizedTime)
        { rig.Animator.Play(HumanAnimParams.StateIdleCivilian, 0, normalizedTime); rig.Animator.Update(0); }
        static Quaternion SourceRest(string id)
        {
            var source = CharacterCatalog.Load().FindById(id).prefab;
            return source.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Jaw").localRotation;
        }

        [UnitySetUp]
        public IEnumerator EmptyAnimationScene()
        {
            RunState.NewGame(); OpeningCinematic.CancelPendingNewGame(); Time.timeScale = 1;
            SceneManager.LoadScene(GameManager.MenuScene); yield return null; yield return null;
            var sandbox = SceneManager.CreateScene("Actual neutral jaw retarget regression"); SceneManager.SetActiveScene(sandbox);
            yield return SceneManager.UnloadSceneAsync(GameManager.MenuScene);
            Assert.That(Close >= 0 && LeftRight >= 0, Is.True);
        }

        [Test]
        public void MissingOnlyFallbackPreservesAuthoredChannelsAndAdditiveOrMappedSources()
        {
            var missing = CopyWithoutJaw();
            Assert.That(Supply(missing, false, false), Is.True);
            var fallback = AnimationUtility.GetEditorCurve(missing, CloseBinding);
            Assert.That(fallback, Is.Not.Null); Assert.That(fallback.Evaluate(0), Is.EqualTo(1).Within(.00001f));
            Assert.That(fallback.Evaluate(missing.length), Is.EqualTo(1).Within(.00001f));
            Assert.That(Supply(missing, false, false), Is.False, "Reprocessing must preserve the existing channel.");

            var authored = CopyWithoutJaw();
            var close = new AnimationCurve(new Keyframe(0,-.5f), new Keyframe(authored.length*.5f,.5f), new Keyframe(authored.length,-.5f));
            var side = new AnimationCurve(new Keyframe(0,-.25f), new Keyframe(authored.length*.5f,.25f), new Keyframe(authored.length,-.25f));
            AnimationUtility.SetEditorCurve(authored, CloseBinding, close); AnimationUtility.SetEditorCurve(authored, SideBinding, side);
            var closeBefore = AnimationUtility.GetEditorCurve(authored, CloseBinding).keys;
            var sideBefore = AnimationUtility.GetEditorCurve(authored, SideBinding).keys;
            Assert.That(Supply(authored, false, false), Is.False);
            CollectionAssert.AreEqual(closeBefore, AnimationUtility.GetEditorCurve(authored, CloseBinding).keys);
            CollectionAssert.AreEqual(sideBefore, AnimationUtility.GetEditorCurve(authored, SideBinding).keys);
            var mapped = CopyWithoutJaw(); Assert.That(Supply(mapped, true, false), Is.False);
            var additive = CopyWithoutJaw(); Assert.That(Supply(additive, false, true), Is.False);
            Assert.That(AnimationUtility.GetEditorCurve(mapped, CloseBinding), Is.Null);
            Assert.That(AnimationUtility.GetEditorCurve(additive, CloseBinding), Is.Null);
        }

        [Test]
        public void GeneratedZerosRequireSourceProvenanceAndExplicitCustomZerosSurvivePolicyRebuild()
        {
            var generated = GeneratedZeroPair();
            var lateral = AnimationUtility.GetEditorCurve(generated, SideBinding).keys;
            Assert.That(SupplySource(generated), Is.True);
            Assert.That(AnimationUtility.GetEditorCurve(generated, CloseBinding).Evaluate(0), Is.EqualTo(1));
            CollectionAssert.AreEqual(lateral, AnimationUtility.GetEditorCurve(generated, SideBinding).keys);
            Assert.That(SupplySource(generated), Is.False, "A previously supplied nonzero default is not a generated-zero pair.");

            foreach (string guard in new[] { "mapped", "raw", "custom", "additive" })
            {
                var protectedClip = GeneratedZeroPair(); var original = AnimationUtility.GetEditorCurve(protectedClip, CloseBinding).keys;
                Assert.That(SupplySource(protectedClip, guard == "mapped", guard == "raw", guard == "custom", guard == "additive"), Is.False, guard);
                CollectionAssert.AreEqual(original, AnimationUtility.GetEditorCurve(protectedClip, CloseBinding).keys);
            }
            var nonzero = GeneratedZeroPair(); AnimationUtility.SetEditorCurve(nonzero, CloseBinding, AnimationCurve.Constant(0, nonzero.length, .2f));
            var nonzeroKeys = AnimationUtility.GetEditorCurve(nonzero, CloseBinding).keys;
            Assert.That(SupplySource(nonzero), Is.False); CollectionAssert.AreEqual(nonzeroKeys, AnimationUtility.GetEditorCurve(nonzero, CloseBinding).keys);
            var varying = GeneratedZeroPair(); AnimationUtility.SetEditorCurve(varying, CloseBinding,
                new AnimationCurve(new Keyframe(0,0),new Keyframe(varying.length*.5f,.3f),new Keyframe(varying.length,0)));
            var varyingKeys = AnimationUtility.GetEditorCurve(varying, CloseBinding).keys;
            Assert.That(SupplySource(varying), Is.False); CollectionAssert.AreEqual(varyingKeys, AnimationUtility.GetEditorCurve(varying, CloseBinding).keys);

            var shaped = GeneratedZeroPair();
            AnimationUtility.SetEditorCurve(shaped, CloseBinding, new AnimationCurve(new Keyframe(0,0,0,1),new Keyframe(shaped.length,0,-1,0)));
            var shapedKeys = AnimationUtility.GetEditorCurve(shaped, CloseBinding).keys;
            Assert.That(AnimationUtility.GetEditorCurve(shaped, CloseBinding).Evaluate(shaped.length*.5f), Is.GreaterThan(0));
            Assert.That(SupplySource(shaped), Is.False, "Two zero endpoint values alone do not establish a constant generated curve.");
            CollectionAssert.AreEqual(shapedKeys, AnimationUtility.GetEditorCurve(shaped, CloseBinding).keys);

            foreach (string explicitName in new[] { "Jaw Close", "Jaw Left-Right" })
            {
                var configured = new ModelImporterClipAnimation { name = "Idle_Neutral", takeName = "Scene", curves = new[] {
                    new ClipAnimationInfoCurve { name = explicitName, curve = AnimationCurve.Constant(0,1,0) },
                    new ClipAnimationInfoCurve { name = "Artist breath", curve = AnimationCurve.Linear(0,.2f,1,.5f) }
                } };
                var rebuilt = new ModelImporterClipAnimation { name = "Scene", takeName = "Scene" };
                ImporterCall("RetainConfiguredCurves", rebuilt, new[] { configured }, "Idle_Neutral");
                Assert.That(rebuilt.curves.Length, Is.EqualTo(2));
                Assert.That(rebuilt.curves[0].name, Is.EqualTo(explicitName));
                CollectionAssert.AreEqual(configured.curves[0].curve.keys, rebuilt.curves[0].curve.keys);
                CollectionAssert.AreEqual(configured.curves[1].curve.keys, rebuilt.curves[1].curve.keys);
                bool authored = (bool)ImporterCall("HasExplicitJawImportCurve", new object[] { new[] { rebuilt } });
                Assert.That(authored, Is.True, "Policy rebuild must not discard the custom zero that establishes authorship.");
                var customZero = GeneratedZeroPair(); var keys = AnimationUtility.GetEditorCurve(customZero, CloseBinding).keys;
                Assert.That(SupplySource(customZero, custom: authored), Is.False);
                CollectionAssert.AreEqual(keys, AnimationUtility.GetEditorCurve(customZero, CloseBinding).keys);
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ActualFourAvatarsKeepClosedImportedJawRestAcrossNeutralVariantsAndGesture()
        {
            foreach (string id in new[] { "LakeGirl", "DrPetrov", "DrVale", "CivOmar" })
            {
                var rest = SourceRest(id); var rig = Actor(id); var jaw = rig.Animator.GetBoneTransform(HumanBodyBones.Jaw);
                Assert.That(jaw, Is.Not.Null);
                foreach (float variant in new[] { 0f, .5f, 1f })
                {
                    rig.Animator.SetFloat(HumanAnimParams.Variant, variant); Idle(rig,.25f);
                    Assert.That(Quaternion.Angle(rest,jaw.localRotation), Is.LessThan(1f), id + " retained the unwanted ~10deg neutral retarget offset.");
                    Assert.That(Pose(rig).muscles[Close], Is.GreaterThan(.98f));
                }
                // Existing additive gesture clips must contribute no second closure.
                rig.Animator.SetFloat(HumanAnimParams.Variant, 0); Idle(rig,.25f);
                rig.Animator.Play("Gesture.Gesture_Explain",1,.35f); rig.Animator.Update(0);
                Assert.That(Quaternion.Angle(rest,jaw.localRotation), Is.LessThan(1f), id + " additive gesture changed neutral jaw rest.");
                Assert.That(Pose(rig).muscles[Close], Is.EqualTo(1).Within(.03f));
                rig.Animator.Play("Gesture.Empty",1,0); rig.Animator.Update(0);
                Object.Destroy(rig.gameObject); owned.Remove(rig.gameObject); yield return null;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ExplicitJawMotionSurvivesImporterAndActuallyAnimatesTheRealAvatar()
        {
            var authored = CopyWithoutJaw();
            authored.name = "Transient explicit jaw authored regression";
            AnimationUtility.SetEditorCurve(authored, CloseBinding, new AnimationCurve(new Keyframe(0,-.5f),new Keyframe(authored.length*.5f,.5f),new Keyframe(authored.length,-.5f)));
            AnimationUtility.SetEditorCurve(authored, SideBinding, new AnimationCurve(new Keyframe(0,-.25f),new Keyframe(authored.length*.5f,.25f),new Keyframe(authored.length,-.25f)));
            Assert.That(Supply(authored,false,false), Is.False);
            var rig = Actor("LakeGirl"); var original = Clip("Idle_Neutral");
            var replacement = new AnimatorOverrideController(rig.Animator.runtimeAnimatorController); owned.Add(replacement); replacement[original.name] = authored;
            rig.Animator.runtimeAnimatorController = replacement; rig.Animator.Rebind();
            rig.Animator.SetBool(HumanAnimParams.Grounded,true); rig.Animator.SetInteger(HumanAnimParams.Profession,(int)Profession.Civilian);
            rig.Animator.SetInteger(HumanAnimParams.State,(int)HostState.Normal); rig.Animator.SetFloat(HumanAnimParams.Speed,0); rig.Animator.SetFloat(HumanAnimParams.Variant,0);
            Idle(rig,.10f); var first = Pose(rig); var firstJaw = rig.Animator.GetBoneTransform(HumanBodyBones.Jaw).localRotation;
            Idle(rig,.50f); var second = Pose(rig); var secondJaw = rig.Animator.GetBoneTransform(HumanBodyBones.Jaw).localRotation;
            Assert.That(first.muscles[Close], Is.LessThan(-.2f)); Assert.That(second.muscles[Close], Is.GreaterThan(.35f));
            Assert.That(first.muscles[LeftRight], Is.LessThan(-.08f)); Assert.That(second.muscles[LeftRight], Is.GreaterThan(.15f));
            Assert.That(Quaternion.Angle(firstJaw,secondJaw), Is.GreaterThan(4f), "Actual authored jaw motion must not be clamped to rest.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in owned) if (item is GameObject) Object.Destroy(item);
            yield return null;
            foreach (var item in owned) if (item && !(item is GameObject)) Object.DestroyImmediate(item);
            owned.Clear(); Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
#endif
