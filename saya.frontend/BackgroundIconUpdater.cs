using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Windows.Media;
using System.Reactive.Disposables;

namespace saya.frontend
{
    class BackgroundIconUpdater : IDisposable
    {
        private readonly ShellIcon ShellIcon;
        private readonly CancellationTokenSource Cts;
        private readonly ConcurrentDictionary<int, WeakReference> IconUpdateSlots = new ConcurrentDictionary<int, WeakReference>();
        private readonly Task Worker;
        private readonly CompositeDisposable CompositeDisposable = new CompositeDisposable();

        public readonly ReactiveProperty<ImageSource>[] Slots;
        public readonly ManualResetEventSlim Signal;

        public BackgroundIconUpdater(int maxResultsCount)
        {
            ShellIcon = new ShellIcon().AddTo(CompositeDisposable);
            Slots = Enumerable.Range(0, maxResultsCount).Select(x =>
            {
                return new ReactiveProperty<ImageSource>().AddTo(CompositeDisposable);
            }).ToArray();
            Cts = new CancellationTokenSource().AddTo(CompositeDisposable);
            Signal =  new ManualResetEventSlim().AddTo(CompositeDisposable);
            Worker = Task.Factory.StartNew(IconUpdateWorker, Cts.Token).AddTo(CompositeDisposable);
        }

        public void UpdateSlot(int index, core.ILaunchTask task)
        {
            var weakTask = new WeakReference(task);
            IconUpdateSlots.AddOrUpdate(index, weakTask, (key, _) => weakTask);
        }

        private void IconUpdateWorker()
        {
            // Vm が Dispose されるまでループは回り続ける
            try
            {
                while (true)
                {
                    Signal.Reset();
                    foreach (var slot in IconUpdateSlots)
                    {
                        if (slot.Value.IsAlive)
                        {
                            Slots[slot.Key].Value = GetIconImageSource(slot.Value.Target as core.ILaunchTask);
                        }
                    }
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



        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
