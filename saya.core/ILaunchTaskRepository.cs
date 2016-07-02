using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskRepository
    {
        Task Sync();

        Task<IEnumerable<ScoredLaunchTask>> Find(ILaunchTaskFinder finder);

        void Register(ILaunchTaskStore taskStore);
        void Unregister(ILaunchTaskStore taskStore);
    }
}
