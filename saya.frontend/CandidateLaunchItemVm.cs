using System.Threading.Tasks;
using System.Windows.Media;
using System.Reactive.Linq;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;

namespace saya.frontend
{
    public class CandidateLaunchItemVm : Vm
    {
        private core.ILaunchTask m_LaunchTask;
        public core.ILaunchTask LaunchTask
        {
            get
            {
                return m_LaunchTask;
            }
            set
            {
                m_LaunchTask = value;
                if (m_LaunchTask == null)
                {
                    IsActive.Value = false;
                    Name.Value = string.Empty;
                    Description.Value = string.Empty;
                }
                else
                {
                    IsActive.Value = true;
                    Name.Value = m_LaunchTask.Name;
                    Description.Value = m_LaunchTask.FilePath;
                }
            }
        }

        public ReactiveProperty<ImageSource> Icon { get; private set; }
        public ReactiveProperty<string> Name { get; private set; }
        public ReactiveProperty<string> Description { get; private set; }
        public ReactiveProperty<string> ShortcutKey { get; private set; }
        public ReactiveProperty<bool> IsActive { get; private set; }

        public CandidateLaunchItemVm(string shortcutKeyText)
        {
            Icon = new ReactiveProperty<ImageSource>().AddTo(CompositeDisposable);
            Name = new ReactiveProperty<string>(string.Empty).AddTo(CompositeDisposable);
            Description = new ReactiveProperty<string>(string.Empty).AddTo(CompositeDisposable);
            ShortcutKey = new ReactiveProperty<string>(shortcutKeyText).AddTo(CompositeDisposable);
            IsActive = new ReactiveProperty<bool>().AddTo(CompositeDisposable);
        }
    }
}
