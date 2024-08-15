using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a tire file (GT3).
/// </summary>
public class TireFileConfig
{
    public uint UnkTriStripRelated { get; set; }
    public uint TriStripFlags { get; set; }
    public float Unk3 { get; set; }
    public string TexturePath { get; set; }
}
