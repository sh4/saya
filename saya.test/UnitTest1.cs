using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using saya.core;

namespace saya.test
{
    [TestClass]
    public class ProcessLauncherTest
    {
        [TestMethod]
        public void ProcessLaunchAndForget()
        {
#if false
            new ProcessLaunchTask().Launch(new[]
            {
                "cmd.exe",
                "/c",
                "dir",
            });
#endif
        }
    }

    [TestClass]
    public class AlcorAbbreviationScorerTest
    {
        [TestMethod]
        public void SimpleAbbreviation()
        {
            AreScoreEqual("hoge", "hg", 0.725f);

            AreScoreEqual("hello", "hello", 1.0f);
            AreScoreEqual("hello", "h", 0.92f);
            AreScoreEqual("hello", "he", 0.94f);
            AreScoreEqual("hello", "hel", 0.96f);
            AreScoreEqual("hello", "llo", 0.6f);
            AreScoreEqual("hello", "lo", 0.4f);
            AreScoreEqual("hello", "ho", 0.4f);
            AreScoreEqual("hello", "hx", 0.0f);
        }

        [TestMethod]
        public void SpecialAbbreviation()
        {
            AreScoreEqual("how_are_you", "ay", 0.3454355f);
            AreScoreEqual("how are you", "ay", 0.913636f);
            AreScoreEqual("HowAreYou", "ay", 0.8000f);
            AreScoreEqual("howareyou", "ay", 0.4222f);
        }

        [TestMethod]
        public void InvalidAbbreviation()
        {
            AreScoreEqual("", "hoge", 0.0f);
            AreScoreEqual("hoge", "", 0.9f);
            AreScoreEqual("", "", 0.9f);
        }

        private static void AreScoreEqual(string text, string abbrevaition, float expectedScore, float delta = 0.0001f)
        {
            Assert.AreEqual(AlcorAbbreviationScorer.Compute(text, abbrevaition), expectedScore, delta);
        }
    }
}
