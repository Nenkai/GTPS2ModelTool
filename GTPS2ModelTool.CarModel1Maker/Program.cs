using CommandLine;
using CommandLine.Text;
using PDTools.Files.Models.PS2.CarModel1;

using YamlDotNet;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GTPS2ModelTool.CarModel1Maker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var p = Parser.Default.ParseArguments<MakeVerbs, SplitVerbs>(args)
                .WithParsed<MakeVerbs>(Make)
                .WithParsed<SplitVerbs>(Split);
        }


        static void Make(MakeVerbs makeVerbs)
        {
            
        }

        static void Split(SplitVerbs makeVerbs)
        {
            using var fs = new FileStream(makeVerbs.InputFile, FileMode.Open);

            var mod = new CarModel1();
            mod.FromStream(fs);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            string js = mod.CarInfo.AsJson();

            fs.Position = 0;
            CarModel1.Split(fs, "test");
        }
    }

    [Verb("make", HelpText = "Makes a car model file (GT3).")]
    public class MakeVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input model files (.obj).")]
        public IEnumerable<string> InputFiles { get; set; }

        [Option('o', "output", HelpText = "Output file. Optional, defaults to <file_name>.mdl if not provided.")]
        public string OutputPath { get; set; }
    }

    [Verb("split", HelpText = "Splits a car model file into multiple components.")]
    public class SplitVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input model file (.obj).")]
        public string InputFile { get; set; }
    }
}