using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Windows.Input;
using System.IO;

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
        public ReactiveCommand OpenCandidateItemPathCommand { get; private set; }
        public ReactiveCommand CopyPathCommand { get; private set; }

        private readonly CandidateLaunchItemVm[] EmptyCandidateLaunchItems = new CandidateLaunchItemVm[] { };
        private readonly BackgroundSearcher Searcher;

        public MainWindowVm()
        {
            CommandText = new ReactiveProperty<string>().AddTo(CompositeDisposable);
            CommandText
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Subscribe(x => Searcher.Query = x)
                .AddTo(CompositeDisposable);

            CandidateLaunchItems = new ReactiveProperty<CandidateLaunchItemVm[]>().AddTo(CompositeDisposable);
            CandidateLaunchItems
                .Where(x => x != null)
                // 検索結果が更新されたら最初の項目を選択済み状態にする
                .Subscribe(items => SelectedCandidateLaunchItem.Value = items.FirstOrDefault());


            SelectedCandidateLaunchItem = new ReactiveProperty<CandidateLaunchItemVm>()
                .AddTo(CompositeDisposable);

            LaunchCandidateItemCommand = SelectedCandidateLaunchItem
                .Select(x => x != null)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            LaunchCandidateItemCommand.Subscribe(_ => LaunchItem(SelectedCandidateLaunchItem.Value));

            ShortcutLaunchCandidateItemCommand = CandidateLaunchItems
                .Select(x => x != null && x.Length > 0)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            ShortcutLaunchCandidateItemCommand.Subscribe(e =>
            {
                var items = CandidateLaunchItems.Value;
                int index;
                if (int.TryParse(e as string, out index) && index < items.Length)
                {
                    LaunchItem(items[index]);
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
            //ScanCommand.Subscribe(_ => Scan());

            OpenCandidateItemPathCommand = SelectedCandidateLaunchItem
                .Select(x => x != null && x.LaunchTask != null)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            OpenCandidateItemPathCommand.Subscribe(_ =>
            {
                var task = SelectedCandidateLaunchItem.Value.LaunchTask;
                var directoryName = Path.GetDirectoryName(task.FilePath);

                if (!Directory.Exists(directoryName))
                {
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directoryName,
                    UseShellExecute = true,
                });
            });

            CopyPathCommand = SelectedCandidateLaunchItem
                .Select(x => x != null && x.LaunchTask != null)
                .ToReactiveCommand()
                .AddTo(CompositeDisposable);
            CopyPathCommand.Subscribe(_ =>
            {
                var task = SelectedCandidateLaunchItem.Value.LaunchTask;
                System.Windows.Clipboard.SetText(task.FilePath);
            });

            Searcher = new BackgroundSearcher(CandidateLaunchItems).AddTo(CompositeDisposable);

            // ショートカットキー
            var toggleKey = new KeyGesture(Key.Tab, ModifierKeys.Control);
            new HotKey(toggleKey, g =>
            {
                ClearCandidateItems();
                Messenger.Default.Send(new MainWindow.ToggleMessage());
            }).AddTo(CompositeDisposable);

            // View からのメッセージによるランチャー起動
            Messenger.Default.Register<MainWindow.LaunchMessage>(_ => LaunchItem(SelectedCandidateLaunchItem.Value));
        }

        private void EnsureSelectNextItem(IEnumerable<CandidateLaunchItemVm> items)
        {
            var item = GetNextCandidateItem(items);
            if (item != null)
            {
                SelectedCandidateLaunchItem.Value = item;
            }
        }

        private void LaunchItem(CandidateLaunchItemVm itemVm)
        {
            var task = itemVm.LaunchTask;
            task.Launch();

            Searcher.AddRecentlyUsed(task.FilePath);

            ClearCandidateItems();
            Close();
        }

        private void ClearCandidateItems()
        {
            // 検索結果のクリア
            CommandText.Value = string.Empty;
            CandidateLaunchItems.Value = EmptyCandidateLaunchItems;
        }

        private void Close()
        {
            Messenger.Default.Send(new MainWindow.CloseMessage());
        }

        private CandidateLaunchItemVm GetNextCandidateItem(IEnumerable<CandidateLaunchItemVm> items)
        {
            var selectedItem = SelectedCandidateLaunchItem.Value;
            var nextItem = items.SkipWhile(x => x != selectedItem).Skip(1).FirstOrDefault();
            return nextItem;
        }
    }
}
