using System.Collections.Generic;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTaskFinder
    {
        string Query { get; set; }

        IEnumerable<ScoredLaunchTask> Find(IEnumerable<ILaunchTask> tasks);
    }
}
