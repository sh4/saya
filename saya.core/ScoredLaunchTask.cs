using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace saya.core
{
    public class ScoredLaunchTask
    {
        public float Score { get; set; }
        public ILaunchTask LaunchTask { get; set; }
    }
}
