using System.Collections.Generic;
using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTask
    {
        string Name { get; }
        string Description { get; }

        IEnumerable<string> Aliases { get; }

        Task Launch();
    }
}
