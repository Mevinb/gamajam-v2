using System.Reflection;
using NUnit.Framework;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Tests
{
    public class SecurityCameraTests
    {
        GameObject root;
        static readonly FieldInfo Deadline = typeof(SecurityCamera).GetField("disabledUntil", BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root);
        }

        [Test]
        public void MaintenancePulseCannotUndoPermanentCampaignSabotage()
        {
            root = new GameObject("Sabotaged CCTV regression");
            var camera = root.AddComponent<SecurityCamera>();
            camera.DisableFor(float.PositiveInfinity); // MissionManager.RestoreCampaignState
            camera.DisableFor(20f);                   // maintenance profession ability
            Assert.IsTrue(camera.Disabled);
            Assert.IsTrue(float.IsPositiveInfinity((float)Deadline.GetValue(camera)),
                "A temporary power cut must not schedule a permanently sabotaged camera to reactivate.");
        }

        [Test]
        public void RepeatPowerCutsOnlyExtendAnExistingOutage()
        {
            root = new GameObject("CCTV power-cut regression");
            var camera = root.AddComponent<SecurityCamera>();
            camera.DisableFor(30f);
            float initialDeadline = (float)Deadline.GetValue(camera);
            camera.DisableFor(2f);
            Assert.That((float)Deadline.GetValue(camera), Is.EqualTo(initialDeadline),
                "An overlapping shorter pulse must not reduce the remaining escape window.");
            camera.DisableFor(60f);
            Assert.That((float)Deadline.GetValue(camera), Is.GreaterThan(initialDeadline));
        }
    }
}
