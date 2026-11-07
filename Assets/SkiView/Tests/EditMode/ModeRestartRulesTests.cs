using NUnit.Framework;
using SkiView.Logic;

namespace SkiView.Tests
{
    public class ModeRestartRulesTests
    {
        [Test]
        public void Harness_Passes()
        {
            int n = ModeRestartHarness.Run(m => Assert.Fail(m));
            Assert.AreEqual(0, n);
        }
    }
}
