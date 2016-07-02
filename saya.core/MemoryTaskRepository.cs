using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public class MemoryTaskRepository : ILaunchTaskRepository
    {
        private List<ILaunchTaskStore> TaskStores = new List<ILaunchTaskStore>();

        public Task<IEnumerable<ScoredLaunchTask>> Find(ILaunchTaskFinder finder)
        {
            var scoredLaunchTasks = TaskStores.SelectMany(x => finder.Find(x.LaunchTasks().Result));
            return Task.FromResult(scoredLaunchTasks);
        }

        public Task Sync()
        {
            foreach (var store in TaskStores)
            {
                store.Sync().Wait();
            }
            return Task.CompletedTask;
        }

        public void Register(ILaunchTaskStore taskStore)
        {
            TaskStores.Add(taskStore);
        }

        public void Unregister(ILaunchTaskStore taskStore)
        {
            TaskStores.Remove(taskStore);
        }
    }
}
