using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.Config.Callbacks;

public class TailLampCallback
{
    /// <summary>
    /// Shapes when off.
    /// </summary>
    public List<string> Off { get; set; } = [];

    /// <summary>
    /// Shapes when on.
    /// </summary>
    public List<string> On { get; set; } = [];

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;
            foreach (var elem in Off)
                hash += elem.GetHashCode();

            foreach (var elem in On)
                hash += elem.GetHashCode();

            return hash;
        }
    }
}
