using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
