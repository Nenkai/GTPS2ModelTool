//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a car file (GT3).
/// </summary>
public class CarModel1Config
{
    public string CarInfo { get; set; }
    public string CarModelSet { get; set; }
    public string Tire { get; set; }
    public string Wheel { get; set; }
}
