using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool.Core.Config.Callbacks
{
    public class TailLampCallback
    {
        /// <summary>
        /// Shapes when off.
        /// </summary>
        public List<string> Off { get; set; } = new List<string>();

        /// <summary>
        /// Shapes when on.
        /// </summary>
        public List<string> On { get; set; } = new List<string>();
    }
}
