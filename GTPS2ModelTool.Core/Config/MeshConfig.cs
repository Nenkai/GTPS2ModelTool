﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a mesh/shape for a model set's model.
/// </summary>
public class MeshConfig
{
    /// <summary>
    /// Whether to use an external texture, provided by the engine (usually reflection).
    /// </summary>
    public bool UseExternalTexture { get; set; }

    public bool UseUnknownShadowFlag { get; set; }

    /// <summary>
    /// Commands to run before the shape is called/rendered.
    /// </summary>
    public List<string> Commands { get; set; } = [];


    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;
            foreach (var elem in Commands)
                hash += elem.GetHashCode();

            hash += UseExternalTexture.GetHashCode();
            hash += UseUnknownShadowFlag.GetHashCode();
            return hash;
        }
    }
}
