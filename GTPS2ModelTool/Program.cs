using CommandLine;

using NLog;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

using PDTools.Files.Models.PS2.CarModel1;
using PDTools.Files.Textures.PS2;
using PDTools.Files.Models.PS2;
using PDTools.Files.Models.PS2.ModelSet;

using GTPS2ModelTool.Core;
using GTPS2ModelTool.Core.Config;

namespace GTPS2ModelTool
{
    internal class Program
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public const string Version = "1.0.1";

        static void Main(string[] args)
        {
            Logger.Info("-----------------------------------------");
            Logger.Info($"- GTPS2ModelTool {Version} by Nenkai");
            Logger.Info("-----------------------------------------");
            Logger.Info("- https://github.com/Nenkai");
            Logger.Info("- https://nenkai.github.io/gt-modding-hub/");
            Logger.Info("-----------------------------------------");
            Logger.Info("");


            var p = Parser.Default.ParseArguments<MakeCarModelVerbs, MakeModelSet1Verbs, MakeTireVerbs, MakeWheelVerbs, DumpVerbs, SplitCarModelVerbs>(args)
                .WithParsed<MakeCarModelVerbs>(MakeCarModelFile)
                .WithParsed<MakeModelSet1Verbs>(MakeModelSet1)
                .WithParsed<MakeTireVerbs>(MakeTireFile)
                .WithParsed<MakeWheelVerbs>(MakeWheelFile)
                .WithParsed<SplitCarModelVerbs>(SplitCarModel)
                .WithParsed<DumpVerbs>(Dump);

            /*
            var builder = new TextureSetBuilder();

            string[] files = Directory.GetFiles(@"C:\Users\nenkai\source\repos\PDTools\PDTools.TextureTool\bin\Debug\net6.0\output_textures");
            foreach (var file in files.OrderBy(e => int.Parse(Path.GetFileNameWithoutExtension(e))))
            {
                builder.AddImage(file, SCE_GS_PSM.SCE_GS_PSMT4);
            }
            builder.Build();

            TextureSet1 set = builder.TextureSet;

            using (var fs = new FileStream("output.tex", FileMode.Create))
                set.Serialize(fs);
            */
        }

        static void MakeModelSet(ModelSetBuildVersion version, IEnumerable<string> inputFiles, string outputPath)
        {
            var builder = new ModelSetBuilder(version);
            if (inputFiles.Count() == 1 && inputFiles.FirstOrDefault().EndsWith(".yaml"))
            {
                var file = inputFiles.FirstOrDefault();
                builder.InitFromConfig(file);
            }
            else
            {
                foreach (string objFile in inputFiles)
                {
                    Logger.Info($"Adding '{objFile}' as new model..");

                    var conf = new ModelConfig()
                    {
                        LODs = new Dictionary<string, LODConfig>()
                        {
                            { objFile, new LODConfig() }
                        }
                    };
                    builder.AddModel(conf);
                }
            }

            ModelSetPS2Base modelSet = builder.Build();

            if (string.IsNullOrEmpty(outputPath))
            {
                var first = inputFiles.FirstOrDefault();
                string dir = Path.GetDirectoryName(first);
                string name = Path.GetFileNameWithoutExtension(first);

                outputPath = Path.Combine(dir, name + ".mdl");
            }

            Logger.Info($"Serializing ModelSet to '{outputPath}'...");

            if (modelSet is ModelSet1 modelSet1)
            {
                using var fs = new FileStream(outputPath, FileMode.Create);
                var serializer = new ModelSet1Serializer(modelSet1);
                serializer.Write(fs);

                Logger.Info($"Serialized ModelSet1. Size: {fs.Length} bytes (0x{fs.Length:X8})");
            }
            else if (modelSet is ModelSet2 modelSet2)
            {
                using var fs = new FileStream(outputPath, FileMode.Create);
                var serializer = new ModelSet2Serializer(modelSet2);
                serializer.Write(fs);

                Logger.Info($"Serialized ModelSet2. Size: {fs.Length} bytes (0x{fs.Length:X8})");
            }

            Logger.Info("Done.");

            DumpFile(outputPath);
        }

        static void MakeModelSet1(MakeModelSet1Verbs makeVerbs)
        {
            Logger.Info("Create ModelSet (MDL1) task started.");

            MakeModelSet(ModelSetBuildVersion.ModelSet1, makeVerbs.InputFiles, makeVerbs.OutputPath);
        }

        /*
        static void MakeModelSet2(MakeModelSet2Verbs makeVerbs)
        {
            Logger.Info("Create ModelSet2 (MDLS) task started.");

            MakeModelSet(ModelSetBuildVersion.ModelSet2, makeVerbs.InputFiles, makeVerbs.OutputPath);
        }
        */

        static void MakeTireFile(MakeTireVerbs makeTire)
        {
            Logger.Info("Create tire file task started.");

            if (!File.Exists(makeTire.InputFile))
            {
                Logger.Error("Input file does not exist.");
                return;
            }

            try
            {
                Logger.Info("Loading texture file for tire...");

                var file = new FileStream(makeTire.InputFile, FileMode.Open);
                var textureSet = new TextureSet1();
                textureSet.FromStream(file);

                var tileFile = new TireFile();
                tileFile.TextureSet = textureSet;

                using var output = new FileStream(makeTire.OutputPath, FileMode.Create);
                tileFile.Write(output);

                Logger.Info($"Created tire file at '{makeTire.OutputPath}'.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to make tire file");
            }
        }

        static void MakeWheelFile(MakeWheelVerbs makeWheel)
        {
            Logger.Info("Create wheel file task started.");

            if (!File.Exists(makeWheel.InputFile))
            {
                Logger.Error("Input file does not exist.");
                return;
            }

            try
            {
                Logger.Info("Loading model file for wheel...");

                using var file = new FileStream(makeWheel.InputFile, FileMode.Open);
                var modelSet = new ModelSet1();
                modelSet.FromStream(file);

                var wheelFile = new WheelFile();
                wheelFile.ModelSet = modelSet;

                using var output = new FileStream(makeWheel.OutputPath, FileMode.Create);
                wheelFile.Write(output);

                Logger.Info($"Created wheel file at '{makeWheel.OutputPath}'.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to make wheel file");
            }
        }

        static void MakeCarModelFile(MakeCarModelVerbs makeCarModelVerbs)
        {
            Logger.Info("Create car model file task started.");

            if (!File.Exists(makeCarModelVerbs.ModelSet))
            {
                Logger.Error("Input model set file does not exist.");
                return;
            }

            if (!File.Exists(makeCarModelVerbs.CarInfo))
            {
                Logger.Error("Input car info file does not exist.");
                return;
            }

            if (!File.Exists(makeCarModelVerbs.Wheel))
            {
                Logger.Error("Input wheel file does not exist.");
                return;
            }

            if (!File.Exists(makeCarModelVerbs.Tire))
            {
                Logger.Error("Input tire file does not exist.");
                return;
            }

            try
            {
                Logger.Info($"Loading model set file '{makeCarModelVerbs.ModelSet}'...");
                using var file = new FileStream(makeCarModelVerbs.ModelSet, FileMode.Open);
                var mainModel = new ModelSet1();
                mainModel.FromStream(file);
                Logger.Info($"Model set loaded - {mainModel.Models.Count} models, {mainModel.Shapes.Count} shapes, {mainModel.Materials.Count} materials, {mainModel.TextureSets.Count} texture sets");

                Logger.Info($"Loading wheel file '{makeCarModelVerbs.Wheel}'...");
                using var wheelFileStream = new FileStream(makeCarModelVerbs.Wheel, FileMode.Open);
                var wheelFile = new WheelFile();
                wheelFile.FromStream(wheelFileStream);
                Logger.Info($"Wheel file loaded - {wheelFile.ModelSet.Shapes.Count} shapes...");

                Logger.Info($"Loading tire file '{makeCarModelVerbs.Tire}'...");
                using var tireFileStream = new FileStream(makeCarModelVerbs.Tire, FileMode.Open);
                var tireFile = new TireFile();
                tireFile.FromStream(tireFileStream);
                Logger.Info($"Tire file loaded");

                Logger.Info($"Loading car info '{makeCarModelVerbs.CarInfo}'...");
                string carInfoJson = File.ReadAllText(makeCarModelVerbs.CarInfo);
                CarInfo info = CarInfo.FromJson(carInfoJson);
                Logger.Info("Car info loaded");

                var carModel = new CarModel1();
                carModel.ModelSet = mainModel;
                carModel.Wheel = wheelFile;
                carModel.Tire = tireFile;
                carModel.CarInfo = info;

                Logger.Info("All loaded. Serializing...");

                string dir = Path.GetDirectoryName(makeCarModelVerbs.OutputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using var output = new FileStream(makeCarModelVerbs.OutputPath, FileMode.Create);
                carModel.Write(output);

                Logger.Info($"Created car model file at '{makeCarModelVerbs.OutputPath}'.");
                Logger.Info($"Size: 0x{output.Length:X8} ({output.Length} bytes)");

                if (output.Length > CarModel1.MaxSizeMenu)
                    Logger.Warn($"Model file is larger than maximum menu model size (0x{CarModel1.MaxSizeMenu:X8})");
                else if (output.Length > CarModel1.MaxSizeRace)
                    Logger.Warn($"Model file is larger than maximum race model size (0x{CarModel1.MaxSizeRace:X8})");
                else
                    Logger.Info("Size is OK");

            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to make car model file");
            }
        }

        static void SplitCarModel(SplitCarModelVerbs splitVerbs)
        {
            if (string.IsNullOrEmpty(splitVerbs.InputFile) || !File.Exists(splitVerbs.InputFile))
            {
                Logger.Error("Input car model file does not exist.");
                return;
            }

            using var fs = new FileStream(splitVerbs.InputFile, FileMode.Open);

            var mod = new CarModel1();
            mod.FromStream(fs);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            string js = mod.CarInfo.AsJson();

            fs.Position = 0;

            string dir = Path.GetDirectoryName(splitVerbs.InputFile);
            string fileName = Path.GetFileNameWithoutExtension(splitVerbs.InputFile);
            string outputDir = Path.Combine(dir, $"{fileName}_split");

            CarModel1.Split(fs, outputDir);

            File.WriteAllText(Path.Combine(outputDir, "car_info.json"), js);
        }

        static void Dump(DumpVerbs dumpVerbs)
        {
            if (Directory.Exists(dumpVerbs.InputFile))
            {
                foreach (var file in Directory.GetFiles(dumpVerbs.InputFile, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        Console.WriteLine($"Processing: {file}");
                        DumpFile(file);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Skipped: {file} - {e.Message}");
                    }
                }
            }
            else
            {
                DumpFile(dumpVerbs.InputFile);
            }
        }

        static void DumpFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var bs = new BinaryReader(fs);

            uint magic = bs.ReadUInt32();
            if (magic == 0x34524143) // CAR4
            {
                bs.BaseStream.Position = 0x18;
                uint modelSetOffset = bs.ReadUInt32();
                bs.BaseStream.Position = modelSetOffset;

                var modelSet = new ModelSet2();
                modelSet.FromStream(fs);
                string name = Path.GetFileNameWithoutExtension(path);
                string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");

                DumpModelSet2(modelSet, modelSetOutputDir);
                return;
            }
            else if (magic == 0x534C444D) // MDLS
            {
                fs.Position = 0;

                var modelSet = new ModelSet2();
                modelSet.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                DumpModelSet2(modelSet, modelSetOutputDir);
                return;
            }
            else if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0;
                var modelSet = new ModelSet1();
                modelSet.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                DumpModelSet(modelSet, modelSetOutputDir);
                return;
            }
            else if (magic == TireFile.MAGIC)
            {
                fs.Position = 0x20;
                var texSet = new TextureSet1();
                texSet.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");
                DumpTextureSet(texSet, texSetOutputDir);
                return;
            }
            else if (magic == WheelFile.MAGIC)
            {
                fs.Position = 0x20;
                var modelSet = new ModelSet1();
                modelSet.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                DumpModelSet(modelSet, modelSetOutputDir);
                return;
            }
            else if (magic == TextureSet1.MAGIC) // Tex1
            {
                fs.Position = 0;
                var texSet = new TextureSet1();
                texSet.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");
                DumpTextureSet(texSet, texSetOutputDir);
                return;
            }
            else if (magic == 0x52425447) // GTBR
            {
                fs.Position = 0;
                DumpBrakeFile(path, fs);
                return;
            }

            // GT3 Car Model
            fs.Position = 0x04;
            uint possibleCarInfoOffset = bs.ReadUInt32();
            fs.Position = (int)possibleCarInfoOffset;

            magic = bs.ReadUInt32();
            if (magic == CarInfo.MAGIC)
            {
                fs.Position = 0;

                var carModel = new CarModel1();
                carModel.FromStream(fs);
                DumpCarModel1(path, carModel);
                return;
            }

            // GT4 Crs MDLS
            fs.Position = 0x100;
            magic = bs.ReadUInt32();
            if (magic == 0x534C444D)
            {
                fs.Position = 0x100;
                //DumpModelSet2(path, fs);
                return;
            }


            fs.Position = 0x20;
            magic = bs.ReadUInt32();
            if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0x20;
                var modelSet = new ModelSet1();
                modelSet.FromStream(fs);
                DumpModelSet(modelSet, path);
                return;
            }

            fs.Position = 0x5180;
            magic = bs.ReadUInt32();
            if (magic == ModelSet1.MAGIC)
            {
                fs.Position = 0x5180;
                var modelSet = new ModelSet1();
                modelSet.FromStream(fs);
                DumpModelSet(modelSet, path);
                return;
            }

            Console.WriteLine("Can not process this file");
        }

        static void DumpCarModel1(string path, CarModel1 carModel)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");

            DumpModelSet(carModel.ModelSet, Path.Combine(modelSetOutputDir, "MainModel"));
            DumpModelSet(carModel.Wheel.ModelSet, Path.Combine(modelSetOutputDir, "Wheel"));
            DumpTextureSet(carModel.Tire.TextureSet, Path.Combine(modelSetOutputDir, "TireTextures"));
        }

        static void DumpBrakeFile(string path, Stream stream)
        {
            var br = new BinaryReader(stream);
            stream.Position = 0x10;
            uint texSetOffset = br.ReadUInt32();
            stream.Position = texSetOffset;

            var texSet = new TextureSet1();
            texSet.FromStream(stream);

            DumpTextureSet(texSet, path);
        }

        static void DumpTextureSet(TextureSet1 texSet, string dir)
        {
            texSet.Dump();

            Directory.CreateDirectory(dir);
            for (int i = 0; i < texSet.pgluTextures.Count; i++)
            {
                using var image = texSet.GetTextureImage(i);

                if (texSet.pgluTextures.Count == 1)
                    image.Save(Path.Combine(dir, Path.GetFileNameWithoutExtension(dir) + ".png"));
                else
                {

                    image.Save(Path.Combine(dir, $"{i}.png"));
                }
            }
        }

        static void DumpModelSet(ModelSet1 modelSet, string dir)
        {
            string textureOutputDir = Path.Combine(dir, "Textures");
            Directory.CreateDirectory(textureOutputDir);

            for (int i = 0; i < modelSet.TextureSets.Count; i++)
            {
                TextureSet1 texSet = modelSet.TextureSets[i];
                for (int k = 0; k < texSet.pgluTextures.Count; k++)
                {
                    try
                    {
                        using var image = texSet.GetTextureImage(k);
                        image.Save(Path.Combine(textureOutputDir, $"{i}.{k}.png"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Skipped {k} - {e.Message}");
                    }
                }
            }

            for (int i = 0; i < modelSet.Models.Count; i++)
            {
                modelSet.DumpModel(i, dir);
            }

            string shapeOutputDir = Path.Combine(dir, "Shapes");
            Directory.CreateDirectory(shapeOutputDir);

            for (int i = 0; i < modelSet.Shapes.Count; i++)
            {
                PGLUshape shape = modelSet.Shapes[i];
                PGLUshapeConverted data = shape.GetShapeData();

                using var objWriter = new StreamWriter(Path.Combine(shapeOutputDir, $"{i}.obj"));
                using var mtlWriter = new StreamWriter(Path.Combine(shapeOutputDir, $"{i}.mtl"));

                data.Dump(objWriter, mtlWriter, 0, 0, 0);
            }
        }

        static void DumpModelSet2(ModelSet2 modelSet, string dir)
        {
            string textureOutputDir = Path.Combine(dir, "Textures");
            Directory.CreateDirectory(textureOutputDir);
            Directory.CreateDirectory(textureOutputDir);

            for (int i = 0; i < modelSet.TextureSetLists[0].Count; i++)
            {
                TextureSet1 texSet = modelSet.TextureSetLists[0][i];
                for (int k = 0; k < texSet.pgluTextures.Count; k++)
                {
                    try
                    {
                        using var image = texSet.GetTextureImage(k);
                        image.Save(Path.Combine(textureOutputDir, $"{i}.{k}.png"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Skipped {k} - {e.Message}");
                    }
                }
            }

            for (int i = 0; i < modelSet.Models.Count; i++)
            {
                modelSet.DumpModel(i, dir);
            }

            string shapeOutputDir = Path.Combine(dir, "Shapes");
            Directory.CreateDirectory(shapeOutputDir);

            for (int i = 0; i < modelSet.Shapes.Count; i++)
            {
                PGLUshape shape = modelSet.Shapes[i];
                PGLUshapeConverted data = shape.GetShapeData();

                using var objWriter = new StreamWriter(Path.Combine(shapeOutputDir, $"{i}.obj"));
                using var mtlWriter = new StreamWriter(Path.Combine(shapeOutputDir, $"{i}.mtl"));

                data.Dump(objWriter, mtlWriter, 0, 0, 0);
            }
        }

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
            [Option("model", Required = true, HelpText = "Input model set file (GTM1).")]
            public string ModelSet { get; set; }

            [Option("car-info", Required = true, HelpText = "Input car info json file (json). (you can get one from splitting an original car model)")]
            public string CarInfo { get; set; }

            [Option("tire", Required = true, HelpText = "Input tire file (GTTR).")]
            public string Tire { get; set; }

            [Option("wheel", Required = true, HelpText = "Input wheel file (GTTW).")]
            public string Wheel { get; set; }

            [Option('o', "output", Required = true, HelpText = "Output car model file.")]
            public string OutputPath { get; set; }
        }

        [Verb("dump", HelpText = "Dumps files into standard formats. Supported: Tex1, GTTR, GTTW, GTM1, CAR4, MDLS, PRM0, PRZ0, GTBR")]
        public class DumpVerbs
        {
            [Option('i', "input", Required = true, HelpText = "Input file. Supported: Tex1, GTTR, GTTW, GTM1, CAR4, MDLS, PRM0, PRZ0, GTBR")]
            public string InputFile { get; set; }
        }

        [Verb("split-car-model", HelpText = "Splits a car model file into multiple files.")]
        public class SplitCarModelVerbs
        {
            [Option('i', "input", Required = true, HelpText = "Input car model file.")]
            public string InputFile { get; set; }
        }
    }
}