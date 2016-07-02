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
            ILaunchTaskStore finder = new StartMenuTaskStore();

            var stopWatch = Stopwatch.StartNew();
            Console.WriteLine("FilePath list constructing...");
            Console.Out.Flush();
            finder.Sync().Wait();
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
                var lunchItems = finder.Find(text).Result;
                foreach (var item in lunchItems.Take(5))
                {
                    Console.WriteLine(item);
                }

            }
        }
    }
}
