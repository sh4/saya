using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskRepository : ILaunchTaskStore
    {
        void Register(ILaunchTaskStore taskStore);
        void Unregister(ILaunchTaskStore taskStore);
    }
}
