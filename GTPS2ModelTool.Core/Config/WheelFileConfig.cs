using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a wheel file (GT3).
/// </summary>
public class WheelFileConfig
{
    public uint UnkFlags { get; set; }
    public string ModelSetPath { get; set; }
}
