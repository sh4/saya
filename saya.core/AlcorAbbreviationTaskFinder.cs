using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace saya.core
{
    public class AlcorAbbreviationTaskFinder : ILaunchTaskFinder
    {
        private readonly int MaxRecentlyUsedPathCount = 30;
        private ConcurrentBag<string> RecentlyUsedPathSet = new ConcurrentBag<string>();
        private ConcurrentQueue<string> RecentlyPathSetQueue = new ConcurrentQueue<string>();

        public IReadOnlyCollection<string> RecentlyUsedFilePaths => RecentlyUsedPathSet;
        public string Query { set; get; }

        public IEnumerable<ScoredLaunchTask> Find(IEnumerable<ILaunchTask> tasks)
        {
            var query = Query;
            var candidateItems = from x in tasks
                                 let score = x.Aliases.Max(y => AlcorAbbreviationScorer.Compute(y, query))
                                 where score > 0.0f
                                 select new ScoredLaunchTask
                                 {
                                     Score = score * Multiplier(x),
                                     LaunchTask = x,
                                 };

            return candidateItems;
        }

        public void AddRecentlyUsed(string filePath)
        {
            RecentlyPathSetQueue.Enqueue(filePath);
            if (RecentlyPathSetQueue.Count > MaxRecentlyUsedPathCount)
            {
                string r;
                RecentlyPathSetQueue.TryDequeue(out r);
            }
            RecentlyUsedPathSet.Add(filePath);
        }

        private float Multiplier(ILaunchTask task)
        {
            if (RecentlyUsedPathSet.Contains(task.FilePath))
            {
                return 2.0f;
            }
            
            switch (task.FileExtension)
            {
                case ".lnk":
                    return 1.5f;
                case ".cmd":
                case ".bat":
                    return 0.8f;
                default:
                    break;
            }

            return 1.0f;
        }
    }
}
