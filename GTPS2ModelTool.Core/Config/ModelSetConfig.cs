using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

namespace GTPS2ModelTool.Core.Config
{
    public class ModelSetConfig
    {
        public Dictionary<string, ModelConfig> Models { get; set; } = new();
        public Dictionary<string, TextureConfig> Textures { get; set; } = new();
    }
}
