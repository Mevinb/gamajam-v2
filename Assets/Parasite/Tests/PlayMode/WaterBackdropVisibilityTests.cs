using System.Collections;
using System.IO;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class WaterBackdropVisibilityTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator ActualDownwardViewsExposeRiverAndBerthAndKeepPhysicalFooting()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Actual rendered visibility needs the released RTX GPU.");
            yield return WaterVisibilityAcceptance.Run(Path.GetFullPath("Logs/water-occlusion"));
        }
    }
}
