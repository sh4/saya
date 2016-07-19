using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace saya.core
{
    public class StartMenuTaskStore : ILaunchTaskStore
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

        private HashSet<FileUtils.Shortcut> ShortcutStore = new HashSet<FileUtils.Shortcut>(new FileUtils.ShortcutEqualityComparer());
        private List<ILaunchTask> Tasks = new List<ILaunchTask>();

        public Task Sync()
        {
            foreach (var searchDirectory in SearchDirectories)
            {
                EnumerateExecutables(searchDirectory);
            }
            return Task.CompletedTask;
        }

        public IEnumerable<ScoredLaunchTask> Find(ILaunchTaskFinder finder)
        {
            return finder.Find(Tasks);
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
                        Tasks.Add(new ProcessLaunchTask
                        {
                            FilePath = execPath,
                            ExistProcessFilePath = execPath,
                        });
                        break;
                    case ".lnk":
                        try
                        {
                            var shortcut = new FileUtils.Shortcut(execPath);
                            if (ShortcutStore.Contains(shortcut))
                            {
                                return; // 既に登録済みのショートカット
                            }
                            ShortcutStore.Add(shortcut);
                            Tasks.Add(new ProcessLaunchTask
                            {
                                FilePath = execPath,
                                ExistProcessFilePath = shortcut.TargetPath,
                                ExistProcessArgument = shortcut.Arguments,
                            });
                        }
                        catch (Exception)
                        {
                            // FIXME: ログを出すようになったら元に戻す
                        }
                        break;
                    default:
                        // 拡張子が存在しないか取得に失敗
                        break;
                }
            }

        }

        public void Dispose()
        {
        }
    }
}
