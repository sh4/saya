using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskFinder
    {
        Task Sync();
        Task<IEnumerable<string>> Find(string query);
    }
}
