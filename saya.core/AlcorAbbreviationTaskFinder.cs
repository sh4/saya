using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public class AlcorAbbreviationTaskFinder : ILaunchTaskFinder
    {
        public string Query { set; get; }

        public IEnumerable<ScoredLaunchTask> Find(IEnumerable<ILaunchTask> tasks)
        {
            var query = Query;
            var candidateItems = from x in tasks
                                 let score = x.Aliases.Max(y => AlcorAbbreviationScorer.Compute(y, query))
                                 where score > 0.0f
                                 select new ScoredLaunchTask { Score = score, LaunchTask = x };

            return candidateItems;
        }
    }
}
