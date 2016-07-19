using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Disposables;
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

        private readonly int MaxResultsCount = 5;
        private readonly TimeSpan CommandSampleTime = TimeSpan.FromSeconds(0.1);
        private core.ILaunchTaskRepository TaskRepository = new core.MemoryTaskRepository();
        private core.AlcorAbbreviationTaskFinder TaskFinder = new core.AlcorAbbreviationTaskFinder();
        private readonly BackgroundIconUpdater IconUpdater;
        private readonly UserProfileManager ProfileManager = new UserProfileManager();

        private CompositeDisposable ResultCompositeDisposable = new CompositeDisposable();

        public MainWindowVm()
        {
            ProfileManager.Load();
            foreach (var path in ProfileManager.Profile.RecentlyUsedFilePath)
            {
                TaskFinder.AddRecentlyUsed(path);
            }

            // TODO: ちゃんとした実装に置き換える
            TaskRepository.Register(new core.StartMenuTaskStore());
            TaskRepository.Register(new core.EverythingTaskStore());

            Scan();

            CommandText = new ReactiveProperty<string>().AddTo(CompositeDisposable);
            CandidateLaunchItems = CommandText
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Sample(CommandSampleTime)
                .Select(x =>
                {
                    // 前回の検索結果を破棄する
                    ResetResultCompositeDisposable();

                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    TaskFinder.Query = x;

                    // 検索スコアの降順に並び替え
                    var launchItems = TaskRepository
                        .Find(TaskFinder);
                    
                    var resultLaunchItems = launchItems
                        .OrderByDescending(y => y.Score)
                        .Take(MaxResultsCount)
                        .Select(y => y.LaunchTask);

                    // 新しい検索結果を次回の Dispose 対象とする
                    var results = resultLaunchItems
                        .Select((y, i) =>
                        {
                            var vm = new CandidateLaunchItemVm(
                                y,
                                "Ctrl+" + (i + 1),
                                IconUpdater.Slots[i]
                            ).AddTo(ResultCompositeDisposable);
                            // アイコンの更新を予約
                            IconUpdater.UpdateSlot(i, y);
                            return vm;
                        })
                        .ToArray();

                    sw.Stop();
                    Console.WriteLine($"{x} => {sw.ElapsedMilliseconds}ms ({launchItems.Count()} items)");

                    return results;
                })
                .ToReactiveProperty()
                .AddTo(CompositeDisposable);

            // 検索結果が更新されたら最初の項目を選択済み状態にする
            CandidateLaunchItems
                .Where(x => x != null)
                .Subscribe(items =>
                {
                    IconUpdater.Signal.Set();
                    SelectedCandidateLaunchItem.Value = items.FirstOrDefault();
                });


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
            ScanCommand.Subscribe(_ => Scan());

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

            CompositeDisposable.Add(ResultCompositeDisposable);
            IconUpdater = new BackgroundIconUpdater(MaxResultsCount).AddTo(CompositeDisposable);

            // ショートカットキー
            var toggleKey = new KeyGesture(Key.Tab, ModifierKeys.Control);
            new HotKey(toggleKey, g =>
            {
                ClearCandidateItems();
                Messenger.Default.Send(new MainWindow.ToggleMessage());
            }).AddTo(CompositeDisposable);

            // View からのメッセージによるランチャー起動
            Messenger.Default.Register<MainWindow.LaunchMessage>(_ => LaunchItem(SelectedCandidateLaunchItem.Value));
            // View からの終了メッセージによる後処理
            Messenger.Default.Register<MainWindow.ExitMessage>(_ =>
            {
                ProfileManager.Profile.RecentlyUsedFilePath = TaskFinder.RecentlyUsedFilePaths.ToList();
                ProfileManager.Save();
            });
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
            TaskFinder.AddRecentlyUsed(task.FilePath);
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

        private void Scan()
        {
            TaskRepository.Sync().Wait();
        }
    }
}
