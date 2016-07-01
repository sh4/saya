using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Windows.Input;

namespace saya.frontend
{
    public class MainWindowVm : Vm
    {
        public ReactiveProperty<string> CommandText { get; private set; }
        public ReactiveProperty<CandidateLaunchItemVm[]> CandidateLaunchItems { get; private set; }
        public ReactiveProperty<CandidateLaunchItemVm> SelectedCandidateLaunchItem { get; private set; }

        public ReactiveCommand LaunchCandidateItemCommand { get; private set; }
        public ReactiveCommand SelectPreviousCandidateCommand { get; private set; }
        public ReactiveCommand SelectNextCandidateCommand { get; private set; }
        public ReactiveCommand ClearCandidateItemListCommand { get; private set; }
        public ReactiveCommand ShortcutLaunchCandidateItemCommand { get; private set; }
        public ReactiveCommand ScanCommand { get; private set; }

        private readonly int MaxResultsCount = 5;
        private core.ILaunchTaskFinder TaskFinder = new core.StartMenuTaskFinder();
        private ShellIcon ShellIcon = new ShellIcon();
        private CompositeDisposable ResultCompositeDisposable = new CompositeDisposable();

        public MainWindowVm()
        {
            // TODO: ちゃんとした実装に置き換える
            Scan();

            CommandText = new ReactiveProperty<string>().AddTo(CompositeDisposable);
            CandidateLaunchItems = CommandText
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x =>
                {
                    // 前回の検索結果を破棄する
                    ResetResultCompositeDisposable();
                    var launchItems = TaskFinder.Find(x).Result;
                    // 新しい検索結果を次回の Dispose 対象とする
                    var results = launchItems
                        .Take(MaxResultsCount)
                        .Select((y, i) => new CandidateLaunchItemVm(
                            y, 
                            GetIconImageSource(y),
                            "Ctrl+" + (i + 1)
                        ).AddTo(ResultCompositeDisposable));
                    return results.ToArray();
                })
                .ToReactiveProperty()
                .AddTo(CompositeDisposable);

            // 検索結果が更新されたら最初の項目を選択済み状態にする
            CandidateLaunchItems
                .Where(x => x != null)
                .Subscribe(items =>
                {
                    SelectedCandidateLaunchItem.Value = items.FirstOrDefault();
                });

            SelectedCandidateLaunchItem = new ReactiveProperty<CandidateLaunchItemVm>()
                .AddTo(CompositeDisposable);

            LaunchCandidateItemCommand = SelectedCandidateLaunchItem
                .Select(x => x != null)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            LaunchCandidateItemCommand.Subscribe(_ => LaunchItem());

            ShortcutLaunchCandidateItemCommand = CandidateLaunchItems
                .Select(x => x != null && x.Length > 0)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            ShortcutLaunchCandidateItemCommand.Subscribe(e =>
            {
                var items = CandidateLaunchItems.Value;
                int index;
                if (int.TryParse(e as string, out index) && index + 1 < items.Length)
                {
                    items[index].Launch();
                    ClearCandidateItems();
                    Close();
                }
            });

            SelectPreviousCandidateCommand = new ReactiveCommand().AddTo(CompositeDisposable);
            SelectPreviousCandidateCommand
                .Subscribe(_ => EnsureSelectNextItem(CandidateLaunchItems.Value.Reverse()));

            SelectNextCandidateCommand = new ReactiveCommand().AddTo(CompositeDisposable);
            SelectNextCandidateCommand
                .Subscribe(_ => EnsureSelectNextItem(CandidateLaunchItems.Value));

            ClearCandidateItemListCommand = new ReactiveCommand().AddTo(CompositeDisposable);
            ClearCandidateItemListCommand
                .Subscribe(_ =>
                {
                    ClearCandidateItems();
                    Close();
                });

            ScanCommand = new ReactiveCommand().AddTo(CompositeDisposable);
            ScanCommand.Subscribe(_ => Scan());

            CompositeDisposable.Add(ResultCompositeDisposable);
            CompositeDisposable.Add(ShellIcon);

            // ショートカットキー
            var toggleKey = new KeyGesture(Key.Tab, ModifierKeys.Control);
            new HotKey(toggleKey, g =>
            {
                ClearCandidateItems();
                Messenger.Default.Send(new MainWindow.ToggleMessage());
            }).AddTo(CompositeDisposable);

            // View からのメッセージによるランチャー起動
            Messenger.Default.Register<MainWindow.LaunchMessage>(_ => LaunchItem());
        }

        private void EnsureSelectNextItem(IEnumerable<CandidateLaunchItemVm> items)
        {
            var item = GetNextCandidateItem(items);
            if (item != null)
            {
                SelectedCandidateLaunchItem.Value = item;
            }
        }

        private void LaunchItem()
        {
            SelectedCandidateLaunchItem.Value.Launch();
            ClearCandidateItems();
            Close();
        }

        private void ClearCandidateItems()
        {
            // 検索結果のクリア
            ResetResultCompositeDisposable();
            CommandText.Value = string.Empty;
            CandidateLaunchItems.Value = new CandidateLaunchItemVm[] { };
        }

        private void Close()
        {
            Messenger.Default.Send(new MainWindow.CloseMessage());
        }

        private void ResetResultCompositeDisposable()
        {
            ResultCompositeDisposable.Dispose();
            ResultCompositeDisposable = new CompositeDisposable();
        }

        private CandidateLaunchItemVm GetNextCandidateItem(IEnumerable<CandidateLaunchItemVm> items)
        {
            var selectedItem = SelectedCandidateLaunchItem.Value;
            var nextItem = items.SkipWhile(x => x != selectedItem).Skip(1).FirstOrDefault();
            return nextItem;
        }

        private System.Windows.Media.ImageSource GetIconImageSource(core.ILaunchTask task)
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

        private void Scan()
        {
            TaskFinder.Sync().Wait();
        }
    }
}
