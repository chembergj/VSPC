using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class WindDeltaMessage: AMessage
    {
        public int Speed { get; set; }
        public int Direction { get; set; }

        public override string ToString()
        {
            return string.Format("WindDeltaMessage Speed: {0}, Direction: {1}", Speed, Direction);
        }
    }
}
