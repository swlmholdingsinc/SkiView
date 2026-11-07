using NUnit.Framework;
using SkiView.Logic;

namespace SkiView.Tests
{
    public class JumpRetryRulesTests
    {
        const int N = 3;   // SkiViewGame.JUMP_PASSES

        // ---- RESTART PASS ----
        [Test] public void Retry_Slalom_GoesToSlalomSetup() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnRetry(false, true, false, 0, N));
        [Test] public void Retry_SlalomOnLakeWithoutRamp_GoesToSlalomSetup() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnRetry(false, false, false, 0, N));
        [Test] public void Retry_SlalomIgnoresStaleJumpResults() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnRetry(false, true, true, N, N));
        [Test] public void Retry_JumpModeWithoutRamp_FallsBackToOldPath() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnRetry(true, false, false, 1, N));

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Retry_JumpMidRound_ReplaysPass(int done) =>
            Assert.AreEqual(JumpRetryAction.ReplayPass, JumpRetryRules.OnRetry(true, true, false, done, N));
        [Test] public void Retry_JumpRoundDone_StartsNewRound() =>
            Assert.AreEqual(JumpRetryAction.NewRound, JumpRetryRules.OnRetry(true, true, false, N, N));
        [Test] public void Retry_JumpResultsOpen_StartsNewRound() =>
            Assert.AreEqual(JumpRetryAction.NewRound, JumpRetryRules.OnRetry(true, true, true, N, N));
        [Test] public void Retry_JumpResultsOpenEarly_StartsNewRound() =>
            Assert.AreEqual(JumpRetryAction.NewRound, JumpRetryRules.OnRetry(true, true, true, 1, N));

        // ---- skip pill ----
        [Test] public void Skip_Slalom_GoesToSlalomSetup() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnSkip(false, true, false, 0, N));
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Skip_JumpMidRound_GoesToNextApproachNotSlalom(int done)
        {
            var a = JumpRetryRules.OnSkip(true, true, false, done, N);
            Assert.AreEqual(JumpRetryAction.ReplayPass, a);
            Assert.AreNotEqual(JumpRetryAction.NotJump, a);
        }
        [Test] public void Skip_JumpRoundDone_DoesNothing() =>
            Assert.AreEqual(JumpRetryAction.Ignore, JumpRetryRules.OnSkip(true, true, true, N, N));
        [Test] public void Skip_JumpModeWithoutRamp_FallsBackToOldPath() =>
            Assert.AreEqual(JumpRetryAction.NotJump, JumpRetryRules.OnSkip(true, false, false, 0, N));

        // ---- full round walk: retry never ends up in a slalom set-up while the round is live ----
        [Test]
        public void Round_WalkThroughThreePassesThenResults()
        {
            for (int done = 0; done < N; done++)
                Assert.AreEqual(JumpRetryAction.ReplayPass, JumpRetryRules.OnRetry(true, true, false, done, N), $"after {done} passes");
            Assert.AreEqual(JumpRetryAction.NewRound, JumpRetryRules.OnRetry(true, true, true, N, N));
            Assert.AreEqual(JumpRetryAction.ReplayPass, JumpRetryRules.OnRetry(true, true, false, 0, N), "after the new round resets");
        }
    }
}
