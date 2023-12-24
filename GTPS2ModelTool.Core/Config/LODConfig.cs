using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

using GTPS2ModelTool.Core.Config.Callbacks;

namespace GTPS2ModelTool.Core.Config
{
    public class LODConfig
    {
        public Dictionary<string, MeshConfig> MeshParameters { get; set; } = new();

        public TailLampCallback TailLampCallback { get; set; }
    }
}
