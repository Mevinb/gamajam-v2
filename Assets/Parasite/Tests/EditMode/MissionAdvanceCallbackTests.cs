using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    // A small logical progression fixture; this does not award playable story missions.
    public sealed class MissionAdvanceCallbackTests
    {
        GameObject root;
        int previousIndex;
        object previousToast, previousBanner, previousInstance;
        static readonly FieldInfo InstanceField = typeof(MissionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo BannerField = typeof(MissionManager).GetField(nameof(MissionManager.Banner), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo ToastField = typeof(GameEvents).GetField(nameof(GameEvents.Toast), BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void Preserve()
        {
            previousIndex = RunState.MissionIndex; previousToast = ToastField.GetValue(null); previousBanner = BannerField.GetValue(null); previousInstance = InstanceField.GetValue(null);
            ToastField.SetValue(null, null); BannerField.SetValue(null, null); RunState.MissionIndex = 0;
            root = new GameObject("Progress notification fault fixture"); root.SetActive(false);
        }

        [TearDown]
        public void Cleanup()
        {
            ToastField.SetValue(null, previousToast); BannerField.SetValue(null, previousBanner); RunState.MissionIndex = previousIndex;
            if (root) Object.DestroyImmediate(root);
            InstanceField.SetValue(null, previousInstance);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SatisfiedPrefixCommitsBeforeAnyCompletionNotification(bool announce)
        {
            var manager = root.AddComponent<MissionManager>();
            var blocked = new Mission { title = "Unsatisfied boundary", completeWhen = () => false };
            typeof(MissionManager).GetProperty(nameof(MissionManager.Missions)).GetSetMethod(true).Invoke(manager, new object[] {
                new List<Mission> {
                    new Mission { title = "First completed", completeWhen = () => true },
                    new Mission { title = "Second completed", completeWhen = () => true }, blocked,
                    new Mission { title = "Later complete but unreachable", completeWhen = () => true }
                }
            });
            int observedIndex = -1, calls = 0; string observedText = null;
            GameEvents.Toast += (message, color) => {
                calls++; observedIndex = RunState.MissionIndex; observedText = message;
                throw new InvalidOperationException("Injected completion toast failure");
            };
            var advance = typeof(MissionManager).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            if (announce)
            {
                var error = Assert.Throws<TargetInvocationException>(() => advance.Invoke(manager, new object[] { true }));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(error.InnerException.Message, Is.EqualTo("Injected completion toast failure"));
                Assert.That(calls, Is.EqualTo(1)); Assert.That(observedIndex, Is.EqualTo(2));
                Assert.That(observedText, Is.EqualTo("MISSION COMPLETE — First completed"));
            }
            else
            {
                Assert.DoesNotThrow(() => advance.Invoke(manager, new object[] { false }));
                Assert.That(calls, Is.Zero);
            }
            Assert.That(RunState.MissionIndex, Is.EqualTo(2));
            Assert.That(manager.Current, Is.SameAs(blocked), "Notification failures cannot strand or over-advance the satisfied prefix.");
        }
    }
}
