//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a tire file (GT3).
/// </summary>
public class TireFileConfig
{
    public uint UnkTriStripRelated { get; set; }
    public uint TriStripFlags { get; set; }
    public float Unk3 { get; set; }
    public string TexturePath { get; set; }
}
