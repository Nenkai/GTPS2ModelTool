using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

namespace GTPS2ModelTool.Core.Config
{
    public class MeshConfig
    {
        public bool UseExternalTexture { get; set; }
        public List<string> CommandsBefore { get; set; }
        public List<string> CommandsAfter { get; set; }
    }
}
