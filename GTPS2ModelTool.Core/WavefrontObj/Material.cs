//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

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
