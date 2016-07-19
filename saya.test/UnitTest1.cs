using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shell32;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using saya.core;
using saya.core.native;

namespace saya.test
{
    [TestClass]
    public class NativeTest
    {
        [TestMethod]
        public void ProcessCommandLineTest()
        {
            var pid = Process.GetCurrentProcess().Id;
            using (var info = new ProcessInfo())
            {
                var cmdLine = info.GetProcessCommandLine(pid);
                Assert.IsTrue(cmdLine.Contains("te.processhost.managed.exe"), cmdLine);
                var args = info.GetProcessArguments(pid);
                Assert.IsTrue(args.StartsWith("/role"), args);
            }
        }
    }

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
            AreScoreEqual(null, null, 0.9f);
            AreScoreEqual("", null, 0.9f);
            AreScoreEqual(null, "", 0.9f);
        }

        private static void AreScoreEqual(string text, string abbrevaition, float expectedScore, float delta = 0.0001f)
        {
            Assert.AreEqual(AlcorAbbreviationScorer.Compute(text, abbrevaition), expectedScore, delta);
        }
    }

    static class ShortcutUtils
    {
        public static IEnumerable<string> GetLocalShortcuts()
        {
            var startMenuPaths = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            };
            foreach (var startMenuDir in startMenuPaths)
            {
                foreach (var path in FileUtils.EnumerateFileEntries(startMenuDir))
                {
                    if ((Path.GetExtension(path) ?? string.Empty).Equals(".lnk", StringComparison.InvariantCultureIgnoreCase))
                    {
                        yield return path;
                    }
                }
            }

        }
    }

    [TestClass]
    public class ShortcutParserTest
    {
        [TestMethod]
        public void LocalFileShortcut()
        {
            foreach (var path in ShortcutUtils.GetLocalShortcuts()
                // Shellの方が誤ったパスを返す関係で Assert に失敗するので、それらはスキップ
                .Where(x => !Path.GetFileName(x).Contains("Snoop")))
            {
                AreEqualShortcut(path);
            }
        }

        private static void AreEqualShortcut(string shortcutPath)
        {
            Assert.IsTrue(File.Exists(shortcutPath), $"File not found: {shortcutPath}");
            try
            {
                var expected = GetShell32LinkPath(shortcutPath);
                var actual = FileUtils.GetShortcutPath(shortcutPath) ?? string.Empty;
                Assert.AreEqual(expected, actual, true, System.Globalization.CultureInfo.InvariantCulture, shortcutPath);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    FileUtils.GetShortcutPath(shortcutPath);
                }
                catch (Exception e)
                {
                    Assert.Fail($"Raised excpetion: {e} (Shortcut is '{shortcutPath}')");
                }
            }
        }

#if false
        [TestMethod]
        public void AdvertisedShortcutParseTest()
        {
            var lnk = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Evernote\Evernote.lnk";
            var shortcut = new FileUtils.Shortcut(lnk);
            Assert.Fail();
        }
#endif

        private static string GetShell32LinkPath(string shortcutPath)
        {
            var directoryPath = Path.GetDirectoryName(shortcutPath);
            var fileName = Path.GetFileName(shortcutPath);
            var shell = new Shell();
            var namespaceObj = shell.NameSpace(directoryPath);
            var shortcutObj = namespaceObj.ParseName(fileName);
            return shortcutObj.GetLink.Path;
        }
    }

    [TestClass]
    public class ShortcutParsePerformanceTeset
    {
        [TestMethod]
        public void CompareParsePerformanceTest()
        {
            var needForPerformanceTuningTime = TimeSpan.FromMilliseconds(100 * ShortcutUtils.GetLocalShortcuts().Count());

            var elapsed = ElapsedTime(() =>
            {
                foreach (var path in ShortcutUtils.GetLocalShortcuts())
                {
                    FileUtils.GetShortcutPath(path);
                }
            });

            var shell32LinkParserElapsed = ElapsedTime(() =>
            {
                var shell32 = new Shell();
                foreach (var path in ShortcutUtils.GetLocalShortcuts())
                {
                    var directoryPath = Path.GetDirectoryName(path);
                    var fileName = Path.GetFileName(path);
                    try
                    {
                        var targetPath = shell32.NameSpace(directoryPath).ParseName(fileName).GetLink.Path;
                    }
                    catch (UnauthorizedAccessException) { }
                }
            });

            Assert.IsTrue(elapsed < shell32LinkParserElapsed, $"Elapsed time: {elapsed.Milliseconds}ms");
            Assert.IsTrue(elapsed < needForPerformanceTuningTime, $"Elapsed time: {elapsed.Milliseconds}ms");
        }

        private static TimeSpan ElapsedTime(Action action)
        {
            var stopWatch = Stopwatch.StartNew();
            action();
            stopWatch.Stop();
            return TimeSpan.FromTicks(stopWatch.ElapsedTicks);
        }
    }
}
