//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using GTPS2ModelTool.Core.WavefrontObj;

namespace GTPS2ModelTool.Core.TriStriper
{
    public class TriStripMesh
    {
        public List<TristripMeshChunk> Chunks { get; set; } = new List<TristripMeshChunk>();
        public List<Face> Faces { get; set; } = new List<Face>();
    }
}
