using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace saya.core
{
    public class EverythingTaskStore : ILaunchTaskStore
    {
        #region Everything API
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern int Everything_SetSearchW(string lpSearchString);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchPath(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchCase(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchWholeWord(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetRegex(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMax(int dwMax);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetOffset(int dwOffset);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetReplyWindow(IntPtr hWnd);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetReplyID(int nId);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchPath();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchCase();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchWholeWord();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetRegex();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetMax();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetOffset();
        [DllImport("Everything64.dll")]
        public static extern string Everything_GetSearch();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetLastError();
        [DllImport("Everything64.dll")]
        public static extern IntPtr Everything_GetReplyWindow();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetReplyID();

        [DllImport("Everything64.dll")]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsQueryReply(int message, IntPtr wParam, IntPtr lParam, uint nId);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SortResultsByPath();

        [DllImport("Everything64.dll")]
        public static extern int Everything_GetNumFileResults();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetNumFolderResults();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetNumResults();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetTotFileResults();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetTotFolderResults();
        [DllImport("Everything64.dll")]
        public static extern int Everything_GetTotResults();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsVolumeResult(int nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFolderResult(int nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFileResult(int nIndex);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern void Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);
        [DllImport("Everything64.dll")]
        public static extern void Everything_Reset();
        #endregion

        public Task Sync()
        {
            return Task.CompletedTask;
        }

        public IEnumerable<ScoredLaunchTask> Find(ILaunchTaskFinder finder)
        {
            return finder.Find(DoEverythingSearch(finder.Query));
        }

        private IEnumerable<ILaunchTask> DoEverythingSearch(string finderQuery)
        {
            var query = BuildEverythingQuery(finderQuery);

            Everything_Reset();
            Everything_SetSearchW(query);
            Everything_QueryW(true);

            var numFile = Everything_GetNumFileResults();
            var maxPathLength = 512;
            var path = new StringBuilder(maxPathLength);
            for (var i = 0; i < numFile; i++)
            {
                Everything_GetResultFullPathNameW(i, path, maxPathLength);
                yield return new ProcessLaunchTask()
                {
                    FilePath = path.ToString(),
                };
            }
        }

        private string BuildEverythingQuery(string text)
        {
            var queries = new List<string>()
            {
                "ext:exe;bat", // 実行ファイルのみ
            };

            // 既に別の方法でスキャンする場所は Everything の検索対象から除外
            var excludePaths = new[]
            {
                Environment.GetEnvironmentVariable("SystemRoot"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramData"),
                Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "AppData"),
            };
            queries.AddRange(excludePaths.Select(x => $"!\"{x}\""));

            // 検索キーワードを文字に分割して、すべての文字がパスに含まれるエントリだけを検索
            queries.Add(string.Join("*", text.ToArray()) + "*");

            return string.Join(" ", queries);
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~EverythingTaskStore() {
        //   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //   Dispose(false);
        // }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
