using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using saya.core;

namespace saya
{
    class Program
    {
        static void Main(string[] args)
        {
            var store = new StartMenuTaskStore();

            var stopWatch = Stopwatch.StartNew();
            Console.WriteLine("FilePath list constructing...");
            Console.Out.Flush();
            store.Sync().Wait();
            stopWatch.Stop();
            Console.WriteLine($"Done, Elapsed time = {stopWatch.ElapsedMilliseconds}ms");
            Console.Out.Flush();

            for (;;)
            {
                var text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }
                var launchItems = store.Find(new AlcorAbbreviationTaskFinder { Query = text });
                foreach (var item in launchItems.OrderBy(x => x.Score).Take(5).Select(x => x.LaunchTask))
                {
                    Console.WriteLine(item.Name);
                }

            }
        }
    }
}
