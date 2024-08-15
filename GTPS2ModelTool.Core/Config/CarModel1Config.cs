using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
