using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Runs the shared acceptance scenarios (Parasite.Testing.AutoTest) inside the Editor play mode.</summary>
    public class AcceptanceTests
    {
        [UnityTest, Timeout(2700000)]
        public IEnumerator AllAcceptanceScenariosPass()
        {
            yield return AutoTest.RunAll();
            foreach (var r in AutoTest.Results) UnityEngine.Debug.Log($"[Acceptance] {(r.pass ? "PASS" : "FAIL")} {r.name} — {r.detail}");
            var failed = AutoTest.Results.Where(r => !r.pass).Select(r => $"{r.name}: {r.detail}").ToArray();
            Assert.IsNotEmpty(AutoTest.Results);
            Assert.IsEmpty(failed, string.Join("\n", failed));
        }
    }
}
