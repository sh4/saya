using System.Threading.Tasks;
using System.Windows.Media;
using System.Reactive.Linq;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace saya.frontend
{
    public class CandidateLaunchItemVm : Vm
    {
        public readonly core.ILaunchTask LaunchTask;

        public ReactiveProperty<ImageSource> Icon { get; private set; }
        public ReactiveProperty<string> Name { get; private set; }
        public ReactiveProperty<string> Description { get; private set; }
        public ReactiveProperty<string> ShortcutKey { get; private set; }

        public CandidateLaunchItemVm(
            core.ILaunchTask task, 
            string shortcutKeyText,
            ReactiveProperty<ImageSource> imageSource)
        {
            LaunchTask = task;

            Icon = imageSource;
            Name = new ReactiveProperty<string>(task.Name).AddTo(CompositeDisposable);
            Description = new ReactiveProperty<string>(task.Description).AddTo(CompositeDisposable);
            ShortcutKey = new ReactiveProperty<string>(shortcutKeyText).AddTo(CompositeDisposable);
        }
    }
}
