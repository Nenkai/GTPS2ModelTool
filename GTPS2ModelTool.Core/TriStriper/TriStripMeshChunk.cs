//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using System.Numerics;

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
        public List<Vector4> Colors { get; set; } = new();

        public int ObjMatId { get; set;  }
        public int PGLUMatId { get; set; }

    }
}
