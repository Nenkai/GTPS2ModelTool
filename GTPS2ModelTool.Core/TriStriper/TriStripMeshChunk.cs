using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.TriStriper
{
    /// <summary>
    /// Represents a tristripped mesh chunk.
    /// </summary>
    public class TristripMeshChunk
    {
        public int StripCount;
        public int VertexCount;

        public List<int> TristripNumbers { get; set; } = new();
        public List<Vector3> Vertices { get; set; } = new();
        public List<Vector3> Normals { get; set; } = new();
        public List<Vector2> TextureCords { get; set; } = new();
        public List<Vector3> Colors { get; set; } = new();

        public int ObjMatId { get; set;  }
        public int PGLUMatId { get; set; }

    }
}
