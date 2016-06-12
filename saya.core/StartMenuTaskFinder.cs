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
        };

        private class LunchItem
        {
            public string FilePath { get; set; }
            public string Name { get; set; }
        }

        private List<LunchItem> LunchItems = new List<LunchItem>();

        public Task Sync()
        {
            foreach (var searchDirectory in SearchDirectories)
            {
                var executables = EnumerateFileEntries(searchDirectory);
                foreach (var executableFilePath in executables)
                {
                    // FIXME: アクセス権限のないディレクトリ以下をスキャンしようとして死ぬ
                    switch (Path.GetExtension(executableFilePath)?.ToLowerInvariant())
                    {
                        case ".exe": // TODO: .exe の場合は実行ファイルのメタ情報も検索対象とする
                        case ".bat":
                            LunchItems.Add(new LunchItem
                            {
                                FilePath = executableFilePath,
                                Name = Path.GetFileNameWithoutExtension(executableFilePath),
                            });
                            break;
                        case ".lnk": // TODO: リンク先のパスを正規化して同一の .exe/.bat 等を指し示したい
                            LunchItems.Add(new LunchItem
                            {
                                FilePath = executableFilePath,
                                Name = Path.GetFileNameWithoutExtension(executableFilePath),
                            });
                            break;
                        default:
                            // 拡張子が存在しないか取得に失敗
                            break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<string>> Find(string query)
        {
            var lunchCandidateItems = LunchItems
                .OrderByDescending(x => AlcorAbbreviationScorer.Compute(x.Name, query))
                .Select(x => x.FilePath);

            return lunchCandidateItems;
        }

        private static IEnumerable<string> EnumerateFileEntries(string path)
        {
            var directories = Enumerable.Empty<string>();
            try
            {
                directories = Directory.EnumerateDirectories(path);
            }
            catch (UnauthorizedAccessException) {}

            foreach (var directoryPath in directories)
            {
                var files = Enumerable.Empty<string>();
                try
                {
                    files = Directory.EnumerateFiles(directoryPath);
                }
                catch (UnauthorizedAccessException) { }
                
                foreach (var filePath in files)
                {
                    yield return filePath;
                }
                foreach (var filePath in EnumerateFileEntries(directoryPath))
                {
                    yield return filePath;
                }
            }
        }
    }
}
