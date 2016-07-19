using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public class MemoryTaskRepository : ILaunchTaskRepository
    {
        private List<ILaunchTaskStore> TaskStores = new List<ILaunchTaskStore>();
        private IEnumerable<ScoredLaunchTask> CachedScoredLaunchTasks;
        private string CachedQuery;

        public IEnumerable<ScoredLaunchTask> Find(ILaunchTaskFinder finder)
        {
            IEnumerable<ScoredLaunchTask> scoredLaunchTasks;

#if false
            if (CachedQuery != null 
                && finder.Query != null
                && finder.Query.StartsWith(CachedQuery))
            {
                scoredLaunchTasks = finder.Find(
                    CachedScoredLaunchTasks.Select(x => x.LaunchTask));
            }
            else
#endif
            {
                scoredLaunchTasks = TaskStores.SelectMany(x => x.Find(finder));
            }
            CachedScoredLaunchTasks = scoredLaunchTasks;
            CachedQuery = finder.Query;
            return scoredLaunchTasks;
        }

        public Task Sync()
        {
            foreach (var store in TaskStores)
            {
                store.Sync().Wait();
            }
            return Task.CompletedTask;
        }

        public void Register(ILaunchTaskStore taskStore)
        {
            TaskStores.Add(taskStore);
        }

        public void Unregister(ILaunchTaskStore taskStore)
        {
            TaskStores.Remove(taskStore);
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
                    foreach (var store in TaskStores)
                    {
                        store.Dispose();
                    }
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~MemoryTaskRepository() {
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
