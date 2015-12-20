using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace MGT.Utilities.Timer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MultimediaTimerCaps
    {
        public int periodMin;
        public int periodMax;
    }
}
