//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a model for a model set.
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// LODs of the model.
    /// </summary>
    public Dictionary<string, LODConfig> LODs { get; set; } = [];
}
