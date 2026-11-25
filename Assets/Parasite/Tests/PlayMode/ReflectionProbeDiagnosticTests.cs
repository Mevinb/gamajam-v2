using System.Collections;
using System.IO;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
namespace Parasite.Tests
{
    public class ReflectionProbeDiagnosticTests
    {
        [UnityTest,Timeout(90000)]
        public IEnumerator QualitySlicingAndTargetMatrixRequiresActualFinishedFaceImages()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Native probe completion/image diagnosis requires the released GPU.");
            yield return ReflectionProbeDiagnosticAcceptance.Run(Path.GetFullPath("Logs/reflection-probe-diagnostic/results"));
        }
    }
}
