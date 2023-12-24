using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

namespace GTPS2ModelTool.Core.Config
{
    public class ModelConfig
    {
        public Dictionary<string, LODConfig> LODs { get; set; } = new();
    }
}
