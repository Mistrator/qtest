using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qtest
{
    class Result
    {
        public Verdict result;
        public string[] output; // either program output or crash message, depending on result
        public int timeMillis;
        public long memoryBytes;
    }
}
