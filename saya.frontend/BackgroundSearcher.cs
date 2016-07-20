using System;
using System.Linq;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Windows.Media;
using System.Reactive.Disposables;

namespace saya.frontend
{
    class BackgroundSearcher : IDisposable
    {
        private readonly ShellIcon ShellIcon;
        private readonly CancellationTokenSource Cts;
        private readonly Task Worker;
        private readonly CompositeDisposable CompositeDisposable = new CompositeDisposable();

        private readonly int MaxResultsCount = 5;
        private readonly UserProfileManager ProfileManager = new UserProfileManager();
        private core.ILaunchTaskRepository TaskRepository = new core.MemoryTaskRepository();
        private core.AlcorAbbreviationTaskFinder TaskFinder = new core.AlcorAbbreviationTaskFinder();

        private CandidateLaunchItemVm[] CandidateLaunchItems;
        private readonly ManualResetEventSlim Signal;
        private readonly ReactiveProperty<CandidateLaunchItemVm[]> ResultCandidateLaunchItems;

        private string m_Query = string.Empty;
        public string Query
        {
            get { return m_Query; }
            set
            {
                m_Query = value;
                Signal.Set();
            }
        }

        public BackgroundSearcher(ReactiveProperty<CandidateLaunchItemVm[]> candidateLaunchItems)
        {
            ResultCandidateLaunchItems = candidateLaunchItems;

            // TODO: ちゃんとした実装に置き換える
            TaskRepository.Register(new core.StartMenuTaskStore());
            TaskRepository.Register(new core.EverythingTaskStore());
            TaskRepository.Sync().Wait();

            ShellIcon = new ShellIcon().AddTo(CompositeDisposable);
            Cts = new CancellationTokenSource().AddTo(CompositeDisposable);
            Signal = new ManualResetEventSlim().AddTo(CompositeDisposable);

            // 設定を読み込む
            ProfileManager.Load();
            foreach (var path in ProfileManager.Profile.RecentlyUsedFilePath)
            {
                TaskFinder.AddRecentlyUsed(path);
            }

            CandidateLaunchItems = new CandidateLaunchItemVm[MaxResultsCount];
            for (var i = 0; i < MaxResultsCount; i++)
            {
                CandidateLaunchItems[i] = new CandidateLaunchItemVm("Ctrl+" + (i + 1));
            }

            Worker = Task.Factory.StartNew(FindWorkerLoop, Cts.Token).AddTo(CompositeDisposable);
        }

        public void AddRecentlyUsed(string filePath)
        {
            TaskFinder.AddRecentlyUsed(filePath);
        }

        private void FindWorkerLoop()
        {
            // Vm が Dispose されるまでループは回り続ける
            try
            {
                while (true)
                {
                    Signal.Reset();
                    ExecuteSearch();
                    Signal.Wait(Cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // cancellation
            }

            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
            Dispatcher.Run();
        }

        private ImageSource GetIconImageSource(core.ILaunchTask task)
        {
            // FIXME: ダウンキャストのせいで密結合になってつらいのでなんとかする

            // saya.core には WPF 周りの UI 実装を組み入れたくない
            // （それは View であって Model に事情を組み入れたくない）
            if (task is core.ProcessLaunchTask)
            {
                var t = task as core.ProcessLaunchTask;
                return ShellIcon.GetIcon(t.FilePath);
            }
            else
            {
                return null;
            }
        }

        private void ExecuteSearch()
        {
            TaskFinder.Query = Query;

            // 検索スコアの降順に並び替え
            var launchItems = TaskRepository
                .Find(TaskFinder)
                .OrderByDescending(y => y.Score)
                .Take(MaxResultsCount)
                .Select(y => y.LaunchTask);

            // 検索結果を現在のバッファに設定
            var i = 0;
            var items = CandidateLaunchItems;
            foreach (var launchItem in launchItems)
            {
                items[i].LaunchTask = launchItem;
                items[i].Icon.Value = GetIconImageSource(launchItem);
                i++;
            }
            for (; i < MaxResultsCount; i++)
            {
                items[i].LaunchTask = null;
            }
            ResultCandidateLaunchItems.Value = items;
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // 設定の書き出し
                    ProfileManager.Profile.RecentlyUsedFilePath = TaskFinder.RecentlyUsedFilePaths.ToList();
                    ProfileManager.Save();
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    Cts.Cancel();
                    Worker.Wait();
                    CompositeDisposable.Dispose();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        // ~BackgroundIconUpdater() {
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
