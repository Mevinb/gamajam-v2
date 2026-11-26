using System.Linq;
using NUnit.Framework;
using Parasite.Testing;
namespace Parasite.Tests
{
    // These inspect the actual registration/selection policy; they do not execute a gameplay factory.
    public sealed class RailNativeSelectionTests
    {
        [Test] public void DefaultSuiteStillContainsExactlyTheOriginal27Ids()
        {
            var names = AutoTest.SelectedScenarioNames(null);
            Assert.That(names.Length, Is.EqualTo(27));
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 27).Select(i => i.ToString("D2")).ToArray(), names.Select(n => n.Substring(0, 2)).ToArray());
        }
        [TestCase("90"), TestCase("91"), TestCase("92"), TestCase("93"), TestCase("94"), TestCase("95"), TestCase("96")]
        public void ExactOptInSelectsOnlyItsOwnRegisteredCase(string id)
        {
            var names = AutoTest.SelectedScenarioNames(new[] { " " + id + " " });
            Assert.That(names.Length, Is.EqualTo(1)); Assert.That(names[0].Substring(0, 2), Is.EqualTo(id));
        }
        [TestCase("9"), TestCase("92x"), TestCase("94extra"), TestCase("96x"), TestCase("999")]
        public void UnknownOrBroadNineFilterSelectsNothing(string token)
            => Assert.That(AutoTest.SelectedScenarioNames(new[] { token }), Is.Empty);
        [Test] public void LegacyPrefixAndEmptyTokenNeverAccidentallyOptIn()
        {
            foreach (var tokens in new[] { new[] { "0" }, new[] { "" }, new[] { "9", "01" } })
                Assert.That(AutoTest.SelectedScenarioNames(tokens).Any(n => int.Parse(n.Substring(0, 2)) >= 90), Is.False);
        }
        [Test] public void CommaTokenListSelectsOnlyExplicitOptInCases()
        {
            var names = AutoTest.SelectedScenarioNames(new[] { "92", " 95", "96" });
            CollectionAssert.AreEqual(new[] { "92", "95", "96" }, names.Select(n => n.Substring(0, 2)).ToArray());
        }
    }
}
