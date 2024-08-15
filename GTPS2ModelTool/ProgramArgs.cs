using CommandLine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTPS2ModelTool;

[Verb("make-model-set", HelpText = "Makes a ModelSet1 (GTM1) file (GT3/GTC).")]
public class MakeModelSet1Verbs
{
    [Option('i', "input", Required = true, HelpText = "Input model files (.obj).")]
    public IEnumerable<string> InputFiles { get; set; }

    [Option('o', "output", HelpText = "Output file. Optional, defaults to <file_name>.mdl if not provided.")]
    public string OutputPath { get; set; }
}

/*
[Verb("make-model-set2", HelpText = "Makes a ModelSet2 (MDLS) file (GT4/TT).")]
public class MakeModelSet2Verbs
{
    [Option('i', "input", Required = true, HelpText = "Input model files (.obj).")]
    public IEnumerable<string> InputFiles { get; set; }

    [Option('o', "output", HelpText = "Output file. Optional, defaults to <file_name>.mdl if not provided.")]
    public string OutputPath { get; set; }
}*/

[Verb("make-tire", HelpText = "Makes a tire (GTTR) file.")]
public class MakeTireVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input texture set file.")]
    public string InputFile { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output file.")]
    public string OutputPath { get; set; }
}

[Verb("make-wheel", HelpText = "Makes a wheel (GTTW) file.")]
public class MakeWheelVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input model (GTM1) file.")]
    public string InputFile { get; set; }

    [Option('o', "output", Required = true, HelpText = "Output file.")]
    public string OutputPath { get; set; }
}

[Verb("make-car-model", HelpText = "Makes a car model file (GT3).")]
public class MakeCarModelVerbs
{
    [Option('i', "input", HelpText = "Input config file.")]
    public string InputFile { get; set; }

    [Option('o', "output", HelpText = "Output car model file.")]
    public string OutputPath { get; set; }
}

[Verb("dump", HelpText = "Dumps files into standard formats. Supported: \n" +
        "GTM0 - ModelSet0 (GT2K)\n" +
        "GTM1 - ModelSet1 (GT3P, GT3, GT4P)\n" +
        "MDLS - ModelSet2 (GT4, TT)\n" +
        "UTex - TextureSet0 (GT2K)\n" +
        "Tex1 - TextureSet1 (GT3P, GT4, GT4P, TT)\n" +
        "CAR4 - CarModel GT4 (GT4, TT)\n" +
        "GTTR - Tire Model (GT3P, GT3, GT4P)\n" +
        "GTTW - Wheel Model (GT3P, GT3, GT4P)\n" +
        "PRM0 - UI Model (GT3)\n" +
        "PRZ0 - Prize Model (GT3)\n" +
        "GTBR (GT3)\n" +
        "car.dat - Car Data (GT2K)\n" +
        "font.dat - Font Data (GT2K)\n" +
        "Course Data (GT2K, shapes only)\n" +
        "CourseData (GT4)")]
public class DumpVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input file.")]
    public string InputFile { get; set; }
}
