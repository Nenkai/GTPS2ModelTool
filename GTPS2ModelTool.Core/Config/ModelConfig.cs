using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

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
