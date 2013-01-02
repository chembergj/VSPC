using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core
{
    public interface IVSPCLogConsumer
    {
        void Log(string level, string message);
    }
}
