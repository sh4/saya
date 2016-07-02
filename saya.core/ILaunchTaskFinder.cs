using System.Collections.Generic;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskFinder
    {
        IEnumerable<ScoredLaunchTask> Find(IEnumerable<ILaunchTask> tasks);
    }
}
