//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using System.Numerics;

namespace GTPS2ModelTool.Core.WavefrontObj
{
    public struct Vertex
    {
        public Vector3 Position { get; set; }
        public Vector4? Color { get; set; }
    }
}
