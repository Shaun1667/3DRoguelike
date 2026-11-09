using System.Linq;
using Game.Rules;
using NUnit.Framework;

namespace Game.Tests
{
    public class RulesAssemblyTests
    {
        [Test]
        public void RulesAssembly_DoesNotReferenceUnity()
        {
            var unityReferences = typeof(RulesAssembly).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("Unity"))
                .ToArray();

            Assert.That(unityReferences, Is.Empty);
        }
    }
}