//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a wheel file (GT3).
/// </summary>
public class WheelFileConfig
{
    public uint UnkFlags { get; set; }
    public string ModelSetPath { get; set; }
}
