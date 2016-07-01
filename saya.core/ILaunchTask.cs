using System.Threading.Tasks;

namespace saya.core
{
    public interface ILaunchTask
    {
        string Name { get; }
        string Description { get; }
        Task Launch();
    }
}
