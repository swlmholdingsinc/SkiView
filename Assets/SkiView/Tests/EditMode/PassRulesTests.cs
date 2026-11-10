using NUnit.Framework;
using SkiView.Logic;

namespace SkiView.Tests
{
    public class PassRulesTests
    {
        [Test]
        public void Harness_Passes()
        {
            int n = PassRulesHarness.Run(m => Assert.Fail(m));
            Assert.AreEqual(0, n);
        }
    }
}
