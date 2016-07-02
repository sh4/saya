using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskStore
    {
        Task Sync();
        Task<IEnumerable<ILaunchTask>> LaunchTasks();
    }
}
