using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.WavefrontObj
{
    /// <summary>
    /// Represents a tri-striped mesh.
    /// </summary>
    public class ModelMesh
    {
        public string Name { get; set; }
        public List<Face> Faces { get; set; } = new List<Face>();

        public ModelMesh(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return $"{Name ?? "unnamed"} - {Faces.Count} faces";
        }
    }
}
