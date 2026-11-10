//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using System.ComponentModel;

using PDTools.Files.Textures.PS2;

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a model set.
/// </summary>
public class ModelSetConfig
{
    /// <summary>
    /// Number of color variations for this model.
    /// </summary>
    [DefaultValue(1)]
    public int NumVariations { get; set; } = 1;

    public Dictionary<string, ModelConfig> Models { get; set; } = [];
    public Dictionary<string, TextureConfig> Textures { get; set; } = [];
}
