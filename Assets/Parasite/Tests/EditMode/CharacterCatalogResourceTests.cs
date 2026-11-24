using NUnit.Framework;
using Parasite.Characters;
using UnityEngine;

namespace Parasite.Tests
{
    public class CharacterCatalogResourceTests
    {
        [TestCase("Dr. Hana Petrov · riverside")]
        [TestCase("DR. HANA PETROV · RIVERSIDE")]
        [TestCase("Dr. Hana Petrov · field clinic")]
        public void DecoratedPetrovUsesTheRealNamedResourceEntry(string displayName)
        {
            var catalog = Resources.Load<CharacterCatalog>(CharacterCatalog.Resource);
            Assert.IsNotNull(catalog);
            var exact = catalog.FindByName("Dr. Hana Petrov");
            Assert.IsNotNull(exact);
            var decorated = catalog.FindByName(displayName);
            Assert.AreSame(exact, decorated, "A location label must not send a named character through anonymous Pick.");
            Assert.AreSame(catalog.FindById("DrPetrov"), decorated);
            Assert.AreEqual("DrPetrov", decorated.id);
            Assert.IsNotNull(decorated.prefab);
            Assert.AreSame(exact.prefab, decorated.prefab);
            Assert.IsTrue(decorated.unique, "HumanoidRig preserves the named variant and its authored presentation.");
        }

        [TestCase("Dr. Hana Petrov Jr.")]
        [TestCase("Dr. Hana Petrovich · riverside")]
        [TestCase("Dr. Hana Petrov· riverside")]
        [TestCase("Dr. Hana Petrov - riverside")]
        [TestCase("Dr. Hana Petrov ·")]
        [TestCase("Dr. Hana Petrov · ")]
        [TestCase("Dr. Hana Petrov ·    ")]
        [TestCase("Unknown · riverside")]
        public void LongerNamesAndMalformedLabelsDoNotBecomeLoosePrefixAliases(string displayName)
        {
            var catalog = Resources.Load<CharacterCatalog>(CharacterCatalog.Resource);
            Assert.IsNotNull(catalog);
            Assert.IsNull(catalog.FindByName(displayName));
        }

        [Test]
        public void ExplicitDecoratedAliasKeepsPriorityOverItsUnderlyingBaseName()
        {
            var resources = Resources.Load<CharacterCatalog>(CharacterCatalog.Resource);
            Assert.IsNotNull(resources);
            var petrov = resources.FindById("DrPetrov");
            var vale = resources.FindById("DrVale");
            Assert.IsNotNull(petrov); Assert.IsNotNull(vale);
            var catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
            try
            {
                catalog.entries.Add(petrov);
                var exactDecorated = new CharacterCatalog.Entry
                {
                    id = "ExplicitDecoratedAlias", prefab = vale.prefab,
                    names = new[] { "Dr. Hana Petrov · riverside" }, unique = true,
                };
                catalog.entries.Add(exactDecorated);
                Assert.AreSame(exactDecorated, catalog.FindByName("Dr. Hana Petrov · riverside"),
                    "The complete alias wins even when the base alias appears earlier in the catalog.");
                Assert.AreSame(exactDecorated, catalog.FindByName("DR. HANA PETROV · RIVERSIDE"));
                Assert.AreSame(petrov, catalog.FindByName("Dr. Hana Petrov"));
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
