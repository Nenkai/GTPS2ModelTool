using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GTPS2ModelTool.Core.WavefrontObj;

namespace GTPS2ModelTool.Core.TriStriper
{
    public class TriStripMesh
    {
        public List<TristripMeshChunk> Chunks { get; set; } = new List<TristripMeshChunk>();
        public List<Face> Faces { get; set; } = new List<Face>();
    }
}
