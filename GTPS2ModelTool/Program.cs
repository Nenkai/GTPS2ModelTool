using CommandLine;

using NLog;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

using PDTools.Files.Models.PS2.CarModel1;
using PDTools.Files.Textures.PS2;
using PDTools.Files.Models.PS2;
using PDTools.Files.Models.PS2.ModelSet;
using PDTools.Files.Textures.PS2.GSPixelFormats;

using GTPS2ModelTool.Core;
using GTPS2ModelTool.Core.Config;
using System.Formats.Tar;

namespace GTPS2ModelTool;

internal class Program
{
    private static Logger Logger = LogManager.GetCurrentClassLogger();

    public const string Version = "1.0.2";

    static void Main(string[] args)
    {
        Logger.Info("-----------------------------------------");
        Logger.Info($"- GTPS2ModelTool {Version} by Nenkai");
        Logger.Info("-----------------------------------------");
        Logger.Info("- https://github.com/Nenkai");
        Logger.Info("- https://nenkai.github.io/gt-modding-hub/");
        Logger.Info("-----------------------------------------");
        Logger.Info("");

        if (args.Length == 1)
        {
            Dump(new DumpVerbs() { InputFile = args[0] });
            return;
        }

        var p = Parser.Default.ParseArguments<MakeCarModelVerbs, MakeModelSet1Verbs, MakeTireVerbs, MakeWheelVerbs, DumpVerbs>(args)
            .WithParsed<MakeCarModelVerbs>(MakeCarModelFile)
            .WithParsed<MakeModelSet1Verbs>(MakeModelSet1)
            .WithParsed<MakeTireVerbs>(MakeTireFile)
            .WithParsed<MakeWheelVerbs>(MakeWheelFile)
            .WithParsed<DumpVerbs>(Dump);
    }

    static void MakeModelSet(ModelSetBuildVersion version, IEnumerable<string> inputFiles, string outputPath)
    {
        var builder = new ModelSetBuilder(version);
        if (inputFiles.Count() == 1 && inputFiles.FirstOrDefault().EndsWith(".yaml"))
        {
            var file = inputFiles.FirstOrDefault();
            if (!builder.InitFromConfig(file))
            {
                Logger.Error("Model build failed.");
                return;
            }
        }
        else
        {
            foreach (string objFile in inputFiles)
            {
                if (Path.GetExtension(objFile) != ".obj")
                {
                    Logger.Error("Input files must be obj files (file: {file:l}).", objFile);
                    return;
                }

                Logger.Info($"Adding '{objFile}' as new model..");

                var conf = new ModelConfig()
                {
                    LODs = new Dictionary<string, LODConfig>()
                    {
                        { objFile, new LODConfig() }
                    }
                };
                builder.AddModel(objFile, conf);
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

        Dumper.DumpFile(outputPath);
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

            TireFile tireFile = BuildTireFile(makeTire.InputFile);
            if (tireFile is null)
            {
                Logger.Error("Failed to make tire file");
                return;
            }

            using var output = new FileStream(makeTire.OutputPath, FileMode.Create);
            tireFile.Write(output);

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

            WheelFile wheelFile = BuildWheelFile(makeWheel.InputFile);
            if (wheelFile is null)
            {
                Logger.Error("Failed to make wheel file");
                return;
            }

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

        if (!File.Exists(makeCarModelVerbs.InputFile))
        {
            Logger.Error("Input car model config file does not exist.");
            return;
        }

        Logger.Info("");
        Logger.Info("[Step 1/6] Loading car model build config..");

        var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .Build();

        string confFile = File.ReadAllText(makeCarModelVerbs.InputFile);
        CarModel1Config carModelConfig = deserializer.Deserialize<CarModel1Config>(confFile);

        Logger.Info($"- Model Set: '{carModelConfig.CarModelSet}'");
        Logger.Info($"- Car Info: '{carModelConfig.CarInfo}'");
        Logger.Info($"- Tire: '{carModelConfig.Tire}'");
        Logger.Info($"- Wheel: '{carModelConfig.Wheel}'");

        string dir = Path.GetDirectoryName(Path.GetFullPath(makeCarModelVerbs.InputFile));

        carModelConfig.CarModelSet = Path.Combine(dir, carModelConfig.CarModelSet);
        if (!File.Exists(carModelConfig.CarModelSet))
        {
            Logger.Error($"Input model set file '{carModelConfig.CarModelSet}' does not exist.");
            return;
        }

        carModelConfig.CarInfo = Path.Combine(dir, carModelConfig.CarInfo);
        if (!File.Exists(carModelConfig.CarInfo))
        {
            Logger.Error($"Input car info file '{carModelConfig.CarInfo}' does not exist.");
            return;
        }

        carModelConfig.Wheel = Path.Combine(dir, carModelConfig.Wheel);
        if (!File.Exists(carModelConfig.Wheel))
        {
            Logger.Error($"Input wheel file '{carModelConfig.CarInfo}' does not exist.");
            return;
        }

        carModelConfig.Tire = Path.Combine(dir, carModelConfig.Tire);
        if (!File.Exists(carModelConfig.Tire))
        {
            Logger.Error($"Input tire file '{carModelConfig.Tire}' does not exist.");
            return;
        }

        try
        {
            Logger.Info("");
            Logger.Info($"[Step 2/6] Loading model set file '{carModelConfig.CarModelSet}'...");
            ModelSet1 mainModel = LoadOrBuildModelSet(carModelConfig.CarModelSet);
            if (mainModel is null)
            {
                Logger.Error("Failed to load/build main model set for car model.");
                return;
            }
            Logger.Info($"[Step 2/6] Model set loaded - {mainModel.Models.Count} models, {mainModel.Shapes.Count} shapes, {mainModel.Materials.Count} materials, {mainModel.TextureSets.Count} texture sets");

            Logger.Info("");
            Logger.Info($"[Step 3/6] Loading wheel file '{carModelConfig.Wheel}'...");

            WheelFile wheelFile = LoadOrBuildWheelFile(carModelConfig.Wheel);
            if (wheelFile is null)
            {
                Logger.Error("Failed to load/build wheel file for car model.");
                return;
            }
            Logger.Info($"[Step 3/6] Wheel file loaded - {wheelFile.ModelSet.Shapes.Count} shapes...");

            Logger.Info("");
            Logger.Info($"[Step 4/6] Loading tire file '{carModelConfig.Tire}'...");

            TireFile tireFile = LoadOrBuildTireFile(carModelConfig.Tire);
            if (tireFile is null)
            {
                Logger.Error("Failed to load/build tire file for car model.");
                return;
            }
            Logger.Info($"[Step 4/6] Tire file loaded");

            Logger.Info("");
            Logger.Info($"[Step 5/6] Loading car info '{carModelConfig.CarInfo}'...");

            CarInfo info;
            if (Path.GetExtension(carModelConfig.CarInfo) == ".json")
            {
                string carInfoJson = File.ReadAllText(carModelConfig.CarInfo);
                info = CarInfo.FromJson(carInfoJson);

                Logger.Info("[Step 5/6] Car info loaded (.json)");
            }
            else
            {
                using var infoStream = File.OpenRead(carModelConfig.CarInfo);
                info = new CarInfo();
                info.FromStream(infoStream);

                Logger.Info("[Step 5/6] Car info loaded (raw binary)");
            }

            var carModel = new CarModel1
            {
                ModelSet = mainModel,
                Wheel = wheelFile,
                Tire = tireFile,
                CarInfo = info
            };

            Logger.Info("");
            Logger.Info("[Step 6/6] All loaded. Serializing...");

            if (string.IsNullOrEmpty(makeCarModelVerbs.OutputPath))
                makeCarModelVerbs.OutputPath = Path.Combine(Path.GetDirectoryName(makeCarModelVerbs.InputFile), Path.GetFileNameWithoutExtension(makeCarModelVerbs.InputFile) + "_build");

            string outputDir = Path.GetDirectoryName(makeCarModelVerbs.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            using var output = new FileStream(makeCarModelVerbs.OutputPath, FileMode.Create);
            carModel.Write(output);

            Logger.Info($"Created car model file at '{makeCarModelVerbs.OutputPath}'.");
            Logger.Info($"Size: 0x{output.Length:X8} ({output.Length} bytes)");

            if (mainModel.TextureSets[0].TotalBlockSize >= 0x2A0)
                Logger.Warn("LOD0's Texture Set block size is larger than any original GT3 car model ({size:X8}). Try to make some texture size optimizations.", mainModel.TextureSets[0].TotalBlockSize);

            if (output.Length > CarModel1.MaxSizeMenu)
                Logger.Warn($"Model file is larger than maximum menu model size (0x{CarModel1.MaxSizeMenu:X8})");
            else if (output.Length > CarModel1.MaxSizeRace)
                Logger.Warn($"Model file is larger than maximum race model size (0x{CarModel1.MaxSizeRace:X8})");
            else
                Logger.Info("Size is OK!");

        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to make car model file");
        }
    }

    /// <summary>
    /// Loads or builds a model set from the specified path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static ModelSet1 LoadOrBuildModelSet(string path)
    {
        ModelSet1 mainModel;
        if (Path.GetExtension(path) == ".yaml")
        {
            Logger.Info($"Building model set '{path}'...");
            var builder = new ModelSetBuilder(ModelSetBuildVersion.ModelSet1);
            if (!builder.InitFromConfig(path))
                return null;

            mainModel = (ModelSet1)builder.Build();
        }
        else
        {
            Logger.Info($"Loading raw/binary model set '{path}'...");

            using var file = new FileStream(path, FileMode.Open);
            mainModel = new ModelSet1();
            mainModel.FromStream(file);
        }

        return mainModel;
    }

    private static WheelFile LoadOrBuildWheelFile(string path)
    {
        WheelFile wheelFile;
        if (Path.GetExtension(path) == ".yaml")
        {
            Logger.Info($"Building wheel file '{path}'...");
            wheelFile = BuildWheelFile(path);
        }
        else
        {
            Logger.Info($"Loading raw/binary wheel file '{path}'...");

            using var wheelFileStream = new FileStream(path, FileMode.Open);
            wheelFile = new WheelFile();
            wheelFile.FromStream(wheelFileStream);
        }

        return wheelFile;
    }

    private static WheelFile BuildWheelFile(string inputConfigPath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        string wheelFileConfig = File.ReadAllText(inputConfigPath);
        WheelFileConfig config = deserializer.Deserialize<WheelFileConfig>(wheelFileConfig);

        string dir = Path.GetDirectoryName(Path.GetFullPath(inputConfigPath));
        config.ModelSetPath = Path.Combine(dir, config.ModelSetPath);
        if (!File.Exists(config.ModelSetPath))
        {
            Logger.Error($"Wheel model set file '{config.ModelSetPath}' referenced by config does not exist");
            return null;
        }

        var modelSet = LoadOrBuildModelSet(config.ModelSetPath);
        if (modelSet == null)
        {
            Logger.Error($"Failed to load or build model set '{config.ModelSetPath}' for wheel file.");
            return null;
        }

        WheelFile wheelFile = new()
        {
            UnkFlags = config.UnkFlags,
            ModelSet = modelSet,
        };

        return wheelFile;
    }

    private static TireFile LoadOrBuildTireFile(string path)
    {
        TireFile tireFile;
        if (Path.GetExtension(path) == ".yaml")
        {
            Logger.Info($"Building tire file '{path}'...");
            tireFile = BuildTireFile(path);
        }
        else
        {
            Logger.Info($"Loading raw/binary tire file '{path}'...");

            using var tireFileStream = new FileStream(path, FileMode.Open);
            tireFile = new TireFile();
            tireFile.FromStream(tireFileStream);
        }

        return tireFile;
    }

    private static TireFile BuildTireFile(string inputConfigPath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        string tireConfigFile = File.ReadAllText(inputConfigPath);
        TireFileConfig config = deserializer.Deserialize<TireFileConfig>(tireConfigFile);

        string dir = Path.GetDirectoryName(Path.GetFullPath(inputConfigPath));
        config.TexturePath = Path.Combine(dir, config.TexturePath);
        if (!File.Exists(config.TexturePath))
        {
            Logger.Error($"Tire texture file '{config.TexturePath}' referenced by config does not exist");
            return null;
        }

        TireFile tireFile = new()
        {
            UnkTriStripRelated = config.UnkTriStripRelated,
            TriStripFlags = config.TriStripFlags,
            Unk3 = config.Unk3
        };

        var textureSetBuilder = new TextureSetBuilder();
        textureSetBuilder.AddImage(config.TexturePath, new TextureConfig() { Format = SCE_GS_PSM.SCE_GS_PSMT4 });
        tireFile.TextureSet = textureSetBuilder.Build();

        return tireFile;
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
                    Dumper.DumpFile(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Skipped: {file} - {e.Message}");
                }
            }
        }
        else
        {
            Dumper.DumpFile(dumpVerbs.InputFile);
        }
    }
}