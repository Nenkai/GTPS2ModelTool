using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files;

namespace GTPS2ModelTool.Core.WavefrontObj
{
    public class Material
    {
        public Material(string name)
        {
            Name = name;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public Color4f Ambient { get; set; }
        public Color4f Diffuse { get; set; }
        public Color4f Specular { get; set; }
        public Color4f Emissive { get; set; }

        public string MapAmbient { get; set; }
        public string MapDiffuse { get; set; }
    }
}
