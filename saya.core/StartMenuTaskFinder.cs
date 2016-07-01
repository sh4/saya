using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace saya.core
{
    public class StartMenuTaskFinder : ILaunchTaskFinder
    {
        private static readonly string[] SearchDirectories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.ExpandEnvironmentVariables(@"%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
        };

        private class LaunchContext
        {
            public IEnumerable<string> Aliases { get; set; }
            public ILaunchTask Task { get; set; }
        }

        private HashSet<FileUtils.Shortcut> ShortcutStore = new HashSet<FileUtils.Shortcut>(new FileUtils.ShortcutEqualityComparer());
        private List<LaunchContext> LaunchContexts = new List<LaunchContext>();

        public Task Sync()
        {
            foreach (var searchDirectory in SearchDirectories)
            {
                EnumerateExecutables(searchDirectory);
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ILaunchTask>> Find(string query)
        {
            var lunchCandidateItems = LaunchContexts
                .OrderByDescending(x => x.Aliases.Max(y => AlcorAbbreviationScorer.Compute(y, query)))
                .Select(x => x.Task);

            return Task.FromResult(lunchCandidateItems);
        }

        private void EnumerateExecutables(string searchDirectory)
        {
            var executables = FileUtils.EnumerateFileEntries(searchDirectory);
            foreach (var execPath in executables)
            {
                switch (Path.GetExtension(execPath)?.ToLowerInvariant())
                {
                    case ".exe": // TODO: .exe の場合は実行ファイルのメタ情報も検索対象とする
                    case ".bat":
                    case ".cmd":
                        LaunchContexts.Add(new LaunchContext
                        {
                            Aliases = new[] { Path.GetFileNameWithoutExtension(execPath) },
                            Task = new ProcessLaunchTask
                            {
                                FilePath = execPath,
                                ExistProcessFilePath = execPath,
                            }
                        });
                        break;
                    case ".lnk":
                        // TODO: リンク先のパスを正規化して同一の .exe/.bat 等を指し示したい
                        //       …のだが、実際にやろうとすると引数を付与した別のショートカットだったりして同一視するとまずい
                        {
                            var shortcut = new FileUtils.Shortcut(execPath);
                            if (Directory.Exists(shortcut.TargetPath))
                            {
                                EnumerateExecutables(shortcut.TargetPath);
                            }
                            else
                            {
                                if (ShortcutStore.Contains(shortcut))
                                {
                                    return; // 既に登録済みのショートカット
                                }
                                ShortcutStore.Add(shortcut);
                                LaunchContexts.Add(new LaunchContext
                                {
                                    Aliases = new[] { Path.GetFileNameWithoutExtension(execPath) },
                                    Task = new ProcessLaunchTask
                                    {
                                        FilePath = execPath,
                                        ExistProcessFilePath = shortcut.TargetPath,
                                        ExistProcessArgument = shortcut.Arguments,
                                    }
                                });
                            }
                        }
                        break;
                    default:
                        // 拡張子が存在しないか取得に失敗
                        break;
                }
            }

        }
    }
}
