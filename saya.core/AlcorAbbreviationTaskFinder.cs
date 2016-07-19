using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public class AlcorAbbreviationTaskFinder : ILaunchTaskFinder
    {
        private readonly int MaxRecentlyUsedPathCount = 30;
        private Queue<string> RecentlyUsedPath = new Queue<string>();
        private HashSet<string> RecenflyUsedPathSet = new HashSet<string>();

        public IReadOnlyCollection<string> RecentlyUsedFilePaths => RecentlyUsedPath;
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
            RecentlyUsedPath.Enqueue(filePath);
            if (RecentlyUsedPath.Count > MaxRecentlyUsedPathCount)
            {
                RecenflyUsedPathSet.Remove(RecentlyUsedPath.Dequeue());
            }
            RecenflyUsedPathSet.Add(filePath);
        }

        private float Multiplier(ILaunchTask task)
        {
            if (RecenflyUsedPathSet.Contains(task.FilePath))
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
