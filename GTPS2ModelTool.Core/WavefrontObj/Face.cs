using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.WavefrontObj
{
    /// <summary>
    /// Represents a mesh face.
    /// </summary>
    public class Face
    {
        public Vertex Vert1;
        public Vertex Vert2;
        public Vertex Vert3;

        public int V1Pos;
        public int V2Pos;
        public int V3Pos;

        public Vector2? UV1;
        public Vector2? UV2;
        public Vector2? UV3;

        public int UV1Pos = -1;
        public int UV2Pos = -1;
        public int UV3Pos = -1;

        public Vector3? Normal1;
        public Vector3? Normal2;
        public Vector3? Normal3;

        public int Normal1Pos = -1;
        public int Normal2Pos = -1;
        public int Normal3Pos = -1;

        public int MaterialId = -1;
        public int PGLUMatIndex = -1;
    }
}
