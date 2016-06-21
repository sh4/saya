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
                var executables = FileUtils.EnumerateFileEntries(searchDirectory);
                foreach (var executableFilePath in executables)
                {
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

    }
}
