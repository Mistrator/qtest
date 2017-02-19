using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace qtest
{
    public enum Verdict
    {
        Accepted,
        ExecutionOk,
        WrongAnswer,
        TimeLimitExceeded,
        MemoryLimitExceeded,
        RuntimeError,
        TimeLimitPassed
    }
}
