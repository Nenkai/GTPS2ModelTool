using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Textures.PS2;

using GTPS2ModelTool.Core.Config.Callbacks;

namespace GTPS2ModelTool.Core.Config;

/// <summary>
/// Configuration for building a model's LOD.
/// </summary>
public class LODConfig
{
    /// <summary>
    /// Meshes/shapes of the LOD.
    /// </summary>
    public Dictionary<string, MeshConfig> MeshParameters { get; set; } = [];

    /// <summary>
    /// Taillamp callback.
    /// </summary>
    public TailLampCallback TailLampCallback { get; set; } = new();

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;
            foreach (var kv in MeshParameters)
                hash += HashCode.Combine(kv.Key, kv.Value);

            if (TailLampCallback is not null)
                hash += TailLampCallback.GetHashCode();

            return hash;
        }
    }
}
