using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    internal interface ICompressionDefaults
    {
        CompressionType LevelFastest { get; }
        CompressionType LevelOptimal { get; }
        CompressionType LevelSmallestSize { get; }


    }
}
