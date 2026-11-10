//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

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
