using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

using SixLabors.ImageSharp;

using PDTools.Files.Courses.PS2;
using PDTools.Files.Models.PS2.CarModel1;
using PDTools.Files.Models.PS2.ModelSet;
using PDTools.Files.Models.PS2.Commands;
using PDTools.Files.Models.PS2;
using PDTools.Files.Textures.PS2;

using GTPS2ModelTool.Core.Config;

namespace GTPS2ModelTool;

public class Dumper
{
    public static void DumpFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open);
        using var bs = new BinaryReader(fs);

        if (fs.Length < 0x04)
        {
            Console.WriteLine($"ERROR: File is too short to be dumpable ({path})");
            return;
        }

        // Try Heuristics for finding what type of file we're dealing with
        uint magic = bs.ReadUInt32();
        switch (magic) // CAR4
        {
            case 0x34524143:
                {
                    bs.BaseStream.Position = 0x18;
                    uint modelSetOffset = bs.ReadUInt32();
                    bs.BaseStream.Position = modelSetOffset;

                    var modelSet = new ModelSet2();
                    modelSet.FromStream(fs);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");

                    DumpModelSet(modelSet, modelSetOutputDir);
                    return;
                }

            case ModelSet2.MAGIC:
                {
                    fs.Position = 0;

                    var modelSet = new ModelSet2();
                    modelSet.FromStream(fs);

                    string name = Path.GetFileNameWithoutExtension(path);
                    string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                    DumpModelSet(modelSet, modelSetOutputDir);
                    return;
                }

            case ModelSet1.MAGIC:
                {
                    fs.Position = 0;
                    var modelSet = new ModelSet1();
                    modelSet.FromStream(fs);

                    string name = Path.GetFileNameWithoutExtension(path);
                    string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                    DumpModelSet(modelSet, modelSetOutputDir);
                    return;
                }
            case ModelSet0.MAGIC:
                {
                    fs.Position = 0;
                    var modelSet = new ModelSet0();
                    modelSet.FromStream(fs);

                    string name = Path.GetFileNameWithoutExtension(path);
                    string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                    DumpModelSet0(modelSet, modelSetOutputDir);
                    return;
                }

            case TireFile.MAGIC:
                {
                    fs.Position = 0x20;
                    var texSet = new TextureSet1();
                    texSet.FromStream(fs);

                    string name = Path.GetFileNameWithoutExtension(path);
                    string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");
                    DumpTextureSet(texSet, texSetOutputDir);
                    return;
                }

            case WheelFile.MAGIC:
                {
                    fs.Position = 0x20;
                    var modelSet = new ModelSet1();
                    modelSet.FromStream(fs);

                    string name = Path.GetFileNameWithoutExtension(path);
                    string modelSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");
                    DumpModelSet(modelSet, modelSetOutputDir);
                    return;
                }

            case UTextureSet.MAGIC:
                {
                    fs.Position = 0;
                    var texSet = new UTextureSet();
                    texSet.FromStream(fs);

                    if (texSet.pgluTextures.Count == 1)
                    {
                        using var image = texSet.GetTextureImage(0);
                        image.Save(Path.ChangeExtension(path, ".png"));
                        return;
                    }

                    string name = Path.GetFileNameWithoutExtension(path);
                    string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");
                    DumpTextureSet(texSet, texSetOutputDir);
                    return;
                }

            case TextureSet1.MAGIC:
                {
                    fs.Position = 0;
                    var texSet = new TextureSet1();
                    texSet.FromStream(fs);

                    if (texSet.pgluTextures.Count == 1)
                    {
                        using var image = texSet.GetTextureImage(0);
                        image.Save(Path.ChangeExtension(path, ".png"));
                        return;
                    }

                    string name = Path.GetFileNameWithoutExtension(path);
                    string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");
                    DumpTextureSet(texSet, texSetOutputDir);
                    return;
                }

            case 0x52425447:
                fs.Position = 0;
                DumpBrakeFile(path, fs);
                return;
        }

        // GT3 Car Model
        fs.Position = 0x04;
        uint possibleCarInfoOffset = bs.ReadUInt32();
        if (possibleCarInfoOffset < fs.Length)
        {
            fs.Position = (int)possibleCarInfoOffset;

            magic = bs.ReadUInt32();
            if (magic == CarInfo.MAGIC)
            {
                fs.Position = 0;

                var carModel = new CarModel1();
                carModel.FromStream(fs);
                fs.Dispose();

                DumpCarModel1(path, carModel);
                return;
            }
            else
            {
                fs.Position = (int)possibleCarInfoOffset + 4;
                uint possibleGtm0Offset = bs.ReadUInt32();

                bs.BaseStream.Position = possibleCarInfoOffset + possibleCarInfoOffset;
                magic = bs.ReadUInt32();
                if (magic == ModelSet0.MAGIC)
                {
                    fs.Position = 0;

                    var gt2kdata = new GT2KCarData();
                    gt2kdata.FromStream(fs);
                    DumpCarDataGT2K(path, gt2kdata);
                    return;
                }
            }
        }

        // GT4 Crs MDLS
        if (fs.Length > 0x100)
        {
            fs.Position = 0x100;
            magic = bs.ReadUInt32();
            if (magic == ModelSet2.MAGIC)
            {
                fs.Position = 0x00;

                string name = Path.GetFileNameWithoutExtension(path);
                string courseDataOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");

                var courseDataFile = new CourseDataFileGT4();
                courseDataFile.FromStream(fs);
                DumpCourseDataGT4(courseDataFile, courseDataOutputDir);
                return;
            }
        }

        // GT2K Course? Just model
        if (fs.Length > 0x50)
        {
            fs.Position = 0x50;
            magic = bs.ReadUInt32();
            if (magic == ModelSet0.MAGIC)
            {
                fs.Position = 0x50;
                var modelSet = new ModelSet0();
                modelSet.FromStream(fs);

                DumpModelSet0(modelSet, path);
                return;
            }
        }

        // GT2K font.dat
        if (fs.Length > 0x20)
        {
            fs.Position = 0x20;
            magic = bs.ReadUInt32();
            if (magic == UTextureSet.MAGIC)
            {
                fs.Position = 0x20;
                var utex = new UTextureSet();
                utex.FromStream(fs);

                string name = Path.GetFileNameWithoutExtension(path);
                string texSetOutputDir = Path.Combine(Path.GetDirectoryName(path), $"{name}_textures");

                DumpTextureSet(utex, texSetOutputDir);
                return;
            }
        }

        if (fs.Length > 0x20)
        {
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
        }

        if (fs.Length > 0x5180)
        {
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
        }

        Console.WriteLine($"ERROR: Could not determine file format for dumping ({path})");
    }

    static void DumpCarDataGT2K(string path, GT2KCarData carData)
    {
        Console.WriteLine("Dumping GT2K Car Data");

        string fullPath = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(fullPath);
        string outputDir = Path.Combine(dir, Path.GetFileNameWithoutExtension(fullPath) + "_dat_dump");

        for (int i = 0; i < carData.CarModels.Count; i++)
        {
            Console.WriteLine($"Dumping car model ({i})");

            var carInfo = carData.CarModels[i];

            DumpModelSet0(carInfo.ModelSet, Path.Combine(outputDir, $"Car{i}"));

            Console.WriteLine("Dumping UTex");
            DumpTextureSet(carInfo.TextureSet, Path.Combine(outputDir, $"Car{i}"));
        }
    }

    static void DumpModelSet0(ModelSet0 modelSet, string dir)
    {
        // This is so old it needs its own method lol
        Console.WriteLine("Dumping ModelSet0");

        string dumpDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dir)), Path.GetFileNameWithoutExtension(dir) + "_dump");
        for (int j = 0; j < modelSet.Models.Count; j++)
        {
            var model = (ModelSet0Model)modelSet.Models[j];
            ProcessShape(Path.Combine(dumpDir, $"Model{j}"), model.UnkModel0, "unk0");
            ProcessShape(Path.Combine(dumpDir, $"Model{j}"), model.MainModel, "main");
            ProcessShape(Path.Combine(dumpDir, $"Model{j}"), model.UnkModel2, "unk2");
            ProcessShape(Path.Combine(dumpDir, $"Model{j}"), model.ReflectionModel, "reflection");
            ProcessShape(Path.Combine(dumpDir, $"Model{j}"), model.UnkModel4, "unk0");
        }
    }

    private static void ProcessShape(string dir, PGLUshape shape, string name)
    {
        if (shape is null)
            return;

        Directory.CreateDirectory(dir);

        PGLUshapeConverted data = shape.GetShapeData();
        data.DumpToObjFile(Path.Combine(dir, $"{name}.obj"));
    }

    static void DumpCarModel1(string path, CarModel1 carModel)
    {
        Console.WriteLine($"Dumping CarModel1");

        string name = Path.GetFileNameWithoutExtension(path);
        string carModelOutput = Path.Combine(Path.GetDirectoryName(path), $"{name}_dump");

        DumpModelSet(carModel.ModelSet, Path.Combine(carModelOutput, "CarModelSet"));
        DumpGT3Wheel(carModel.Wheel, Path.Combine(carModelOutput, "Wheel"));
        DumpGT3Tire(carModel.Tire, Path.Combine(carModelOutput, "Tire"));
        File.WriteAllText(Path.Combine(carModelOutput, "car_info.json"), carModel.CarInfo.AsJson());

        // Dump two yamls for building
        // - One that links to the raw files already build
        // - The other links to yaml files for building the dependencies aswell

        using var fs = File.OpenRead(path);
        CarModel1.Split(fs, carModelOutput);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
            .Build();

        var binaryCarModelConfig = new CarModel1Config
        {
            CarInfo = "car_info.bin",
            CarModelSet = "car_model_set.mdl",
            Tire = "tire.bin",
            Wheel = "wheel.bin"
        };

        string file = serializer.Serialize(binaryCarModelConfig);
        File.WriteAllText(Path.Combine(carModelOutput, "car_config_binary.yaml"), file);

        var buildCarModelConfig = new CarModel1Config
        {
            CarInfo = "car_info.json",
            CarModelSet = "CarModelSet/model_set.yaml",
            Tire = "Tire/tire.yaml",
            Wheel = "Wheel/wheel.yaml"
        };

        file = serializer.Serialize(buildCarModelConfig);
        File.WriteAllText(Path.Combine(carModelOutput, "car_config.yaml"), file);
    }

    static void DumpGT3Wheel(WheelFile wheelFile, string dir)
    {
        Console.WriteLine($"Dumping GT3 wheel file");

        DumpModelSet(wheelFile.ModelSet, Path.Combine(dir, "Model"));

        var serializer = new SerializerBuilder()
           .WithNamingConvention(NullNamingConvention.Instance)
           .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
           .Build();

        var tireConfig = new WheelFileConfig
        {
            UnkFlags = wheelFile.UnkFlags,
            ModelSetPath = "Model/model_set.yaml",
        };

        string file = serializer.Serialize(tireConfig);
        File.WriteAllText(Path.Combine(dir, "wheel.yaml"), file);
    }

    static void DumpGT3Tire(TireFile tireFile, string dir)
    {
        Console.WriteLine($"Dumping GT3 tire file");

        DumpTextureSet(tireFile.TextureSet, dir);

        var serializer = new SerializerBuilder()
           .WithNamingConvention(NullNamingConvention.Instance)
           .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
           .Build();

        var tireConfig = new TireFileConfig
        {
            UnkTriStripRelated = tireFile.UnkTriStripRelated,
            TriStripFlags = tireFile.TriStripFlags,
            Unk3 = tireFile.Unk3,
            TexturePath = "Tire.png",
        };

        string file = serializer.Serialize(tireConfig);
        File.WriteAllText(Path.Combine(dir, "tire.yaml"), file);
    }

    static void DumpBrakeFile(string path, Stream stream)
    {
        Console.WriteLine($"Dumping brake file");

        var br = new BinaryReader(stream);
        stream.Position = 0x10;
        uint texSetOffset = br.ReadUInt32();
        stream.Position = texSetOffset;

        var texSet = new TextureSet1();
        texSet.FromStream(stream);

        DumpTextureSet(texSet, path);
    }

    static void DumpShape(PGLUshape shape, string dir)
    {
        if (shape is null)
            return;

        Directory.CreateDirectory(dir);
        shape.GetShapeData().DumpToObjFile(Path.Combine(dir, "shape.obj"));
    }

    static void DumpTextureSet(TextureSetPS2Base texSet, string dir)
    {
        if (texSet is null)
            return;

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

    static void DumpModelSet(ModelSetPS2Base modelSet, string dir)
    {
        if (modelSet is null)
            return;

        if (modelSet is not ModelSet0)
        {
            int numVars = modelSet.GetNumVariations();
            var modelSetConfig = new ModelSetConfig
            {
                NumVariations = numVars,
            };

            // Go through each variation of the model (variations alter the color of the model)
            for (int varIndex = 0; varIndex < numVars; varIndex++)
            {
                string varDir = numVars == 1 ? dir : Path.Combine(dir, $"Var{varIndex}");
                string textureOutputDir = Path.Combine(varDir, $"Textures");
                Directory.CreateDirectory(textureOutputDir);

                Console.WriteLine($"Dumping variation #{varIndex}");

                var texSetList = modelSet.GetTextureSetList();
                for (int lodIndex = 0; lodIndex < texSetList.Count; lodIndex++)
                {
                    Console.WriteLine($"Dumping textures for lod {lodIndex}");

                    TextureSet1 lodTexSet = texSetList[lodIndex];
                    for (int textureIndex = 0; textureIndex < lodTexSet.pgluTextures.Count; textureIndex++)
                    {
                        Console.WriteLine($"Dumping texture index {textureIndex}");
                        using var image = lodTexSet.GetTextureImage(textureIndex, varIndex);
                        image.Save(Path.Combine(textureOutputDir, $"{lodIndex}.{textureIndex}.png"));
                    }
                }

                int numModels = modelSet.GetNumModels();
                for (int modelIndex = 0; modelIndex < numModels; modelIndex++)
                    DumpModelSetModel(modelSet, dir, modelSetConfig, varIndex, varDir, modelIndex);
            }

            Console.WriteLine("Dumping model set textures");
            DumpModelSetTextures(modelSet, modelSetConfig);

            Console.WriteLine("Creating model_set.yaml");

            // Serialize the build config
            var serializer = new SerializerBuilder()
                        .WithNamingConvention(NullNamingConvention.Instance)
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
                        .Build();

            string file = serializer.Serialize(modelSetConfig);
            File.WriteAllText(Path.Combine(dir, "model_set.yaml"), file);
        }
    }

    private static void DumpModelSetTextures(ModelSetPS2Base modelSet, ModelSetConfig modelSetConfig)
    {
        var textureSets = modelSet.GetTextureSetList();
        for (int i = 0; i < textureSets.Count; i++)
        {
            TextureSet1 texSet = textureSets[i];
            for (int k = 0; k < texSet.pgluTextures.Count; k++)
            {
                PGLUtexture pgluTexture = texSet.pgluTextures[k];

                var textureConfig = new TextureConfig();
                textureConfig.Format = pgluTexture.tex0.PSM;
                textureConfig.WrapModeS = pgluTexture.ClampSettings.WMS;
                textureConfig.WrapModeT = pgluTexture.ClampSettings.WMT;

                if (textureConfig.WrapModeS == SCE_GS_CLAMP_PARAMS.SCE_GS_REPEAT)
                    textureConfig.RepeatWidth = (int)Math.Pow(2, pgluTexture.tex0.TW_TextureWidth);

                if (textureConfig.WrapModeT == SCE_GS_CLAMP_PARAMS.SCE_GS_REPEAT)
                    textureConfig.RepeatWidth = (int)Math.Pow(2, pgluTexture.tex0.TH_TextureHeight);

                modelSetConfig.Textures.Add($"{i}.{k}.png", textureConfig);
            }
        }
    }

    private static void DumpShape(ModelSetPS2Base modelSet, string shapeOutputDir, int i)
    {
        Console.WriteLine($"Dumping shape #{i}");

        PGLUshape shape = modelSet.GetShape(i);
        PGLUshapeConverted data = shape.GetShapeData();
        data.DumpToObjFile(Path.Combine(shapeOutputDir, $"{i}.obj"));
    }

    private static void DumpModelSetModel(ModelSetPS2Base modelSet, string dir, ModelSetConfig modelSetConfig, int varIndex, string varDir, int modelIndex)
    {
        Console.WriteLine($"Dumping model #{modelIndex}");

        List<DumpedLOD> lods = modelSet.DumpModelLODs(modelIndex, dir);
        DumpModelLodsToObj(modelSet, lods, varIndex, varDir, $"model{modelIndex}");

        if (varIndex == 0)
        {
            var model = new ModelConfig();
            for (int j = 0; j < lods.Count; j++)
            {
                var lodConfig = new LODConfig();

                string lodModelName = lods.Count == 1 ? $"model{modelIndex}" : $"model{modelIndex}.lod{j}";

                var lod = lods[j];
                model.LODs.Add(lodModelName, lodConfig);

                foreach (KeyValuePair<string, PGLUshapeConverted> shape in lod.Shapes)
                {
                    var meshConfig = new MeshConfig();
                    meshConfig.UseExternalTexture = shape.Value.UsesExternalTexture;
                    meshConfig.UseUnknownShadowFlag = (shape.Value.Unk1 & 4) != 0;

                    lodConfig.MeshParameters.Add(shape.Key, meshConfig);

                    foreach (var cmd in shape.Value.RenderCommands)
                    {
                        switch (cmd.Opcode)
                        {
                            case ModelSetupPS2Opcode.pglAlphaFunc:
                                {
                                    var alphaFunc = cmd as Cmd_pglAlphaFunc;
                                    meshConfig.Commands.Add($"AlphaFunction({alphaFunc.TST}, {alphaFunc.REF})");
                                }
                                break;
                            case ModelSetupPS2Opcode.pglDisableAlphaTest:
                                meshConfig.Commands.Add("DisableAlphaTest");

                                break;
                            case ModelSetupPS2Opcode.pglColorMask:
                                {
                                    var colorMask = cmd as Cmd_pglColorMask;
                                    meshConfig.Commands.Add($"ColorMask(0x{~colorMask.ColorMask:X8})");
                                }
                                break;
                            case ModelSetupPS2Opcode.pglDisableDepthMask:
                                meshConfig.Commands.Add("DisableDepthMask");
                                break;
                            case ModelSetupPS2Opcode.pglBlendFunc:
                                {
                                    var blendFunc = cmd as Cmd_pglBlendFunc;
                                    meshConfig.Commands.Add($"BlendFunction({blendFunc.A}, {blendFunc.B}, {blendFunc.C}, {blendFunc.D}, {blendFunc.FIX})");
                                }
                                break;
                            case ModelSetupPS2Opcode.pglGT3_2_4f:
                                {
                                    var cmd_ = cmd as Cmd_GT3_2_4f;
                                    meshConfig.Commands.Add($"UnkGT3_2_4f({cmd_.R}, {cmd_.G}, {cmd_.B}, {cmd_.A})");
                                }
                                break;
                            case ModelSetupPS2Opcode.pglEnableDestinationAlphaTest:
                                meshConfig.Commands.Add("EnableDestinationAlphaTest");
                                break;
                            case ModelSetupPS2Opcode.pglSetDestinationAlphaFunc:
                                meshConfig.Commands.Add($"DestinationAlphaFunc({(cmd as Cmd_pglSetDestinationAlphaFunc).Func})");
                                break;
                            case ModelSetupPS2Opcode.pglSetFogColor:
                                {
                                    var fogColor = (cmd as Cmd_pglSetFogColor);
                                    byte r = (byte)(fogColor.Color & 0xFF);
                                    byte g = (byte)((fogColor.Color >> 8) & 0xFF);
                                    byte b = (byte)((fogColor.Color >> 16) & 0xFF);

                                    meshConfig.Commands.Add($"FogColor({r}, {g}, {b})");
                                }
                                break;
                            case ModelSetupPS2Opcode.pglExternalTexIndex:
                                {
                                    meshConfig.Commands.Add($"ExternalTextureIndex({(cmd as Cmd_pgluSetExternalTexIndex).TexIndex})");
                                    break;

                                }
                            default:
                                throw new NotSupportedException();
                        }
                    }

                    meshConfig.Commands.Sort();
                }

                foreach (var cb in lod.Callbacks)
                {
                    switch (cb.Key)
                    {
                        case ModelCallbackParameter.IsTailLampActive:
                            if (cb.Value.Count >= 1)
                            for (int i = 0; i < cb.Value[0].Count; i++)
                                lodConfig.TailLampCallback.Off.Add(cb.Value[0][i]);

                            if (cb.Value.Count >= 2)
                            {
                                for (int i = 0; i < cb.Value[1].Count; i++)
                                    lodConfig.TailLampCallback.On.Add(cb.Value[1][i]);
                            }
                            break;
                    }
                }
            }

            modelSetConfig.Models.Add($"model{modelIndex}", model);
        }
    }

    private static void DumpModelLodsToObj(ModelSetPS2Base modelSet, List<DumpedLOD> lods, int varIndex, string dir, string modelName)
    {
        for (int i = 0; i < lods.Count; i++)
        {
            int vertIdxStart = 0;
            int vnIdxStart = 0;
            int vtIdxStart = 0;

            string name = lods.Count == 1 ? $"{modelName}" : $"{modelName}.lod{i}";
            using var objWriter = new StreamWriter(Path.Combine(dir, $"{name}.obj"));
            using var matWriter = new StreamWriter(Path.Combine(dir, $"{name}.mtl"));
            objWriter.WriteLine($"mtllib {name}.mtl");

            var lod = lods[i];
            foreach (KeyValuePair<string, PGLUshapeConverted> shape in lod.Shapes)
            {
                HashSet<int> texIndices = new HashSet<int>();
                for (int faceIdx = 0; faceIdx < shape.Value.Faces.Count; faceIdx++)
                {
                    texIndices.Add(shape.Value.Faces[faceIdx].TexId);
                }

                bool isExternalTextureReflection = texIndices.All(e => e == VIFDescriptor.EXTERNAL_TEXTURE);

                objWriter.WriteLine($"# Shape {shape.Value.ShapeIndex}");
                objWriter.WriteLine($"o {shape.Key}");

                shape.Value.DumpToObj(objWriter, matWriter, shape.Value.TextureSetIndex, vertIdxStart, vnIdxStart, vtIdxStart, modelSet.GetVariationMaterials(varIndex));
                objWriter.WriteLine();

                vertIdxStart += shape.Value.Vertices.Count;
                vnIdxStart += shape.Value.Normals.Count;
                vtIdxStart += shape.Value.UVs.Count;
            }
        }
    }

    static void DumpCourseDataGT4(CourseDataFileGT4 courseDataFile, string dir)
    {
        if (courseDataFile.World != null)
        {
            Console.WriteLine("Dumping CourseData->World");
            DumpModelSet(courseDataFile.World, Path.Combine(dir, "World"));
        }

        if (courseDataFile.Environment != null)
        {
            Console.WriteLine("Dumping CourseData->Environment");
            DumpModelSet(courseDataFile.Environment, Path.Combine(dir, "Environment"));
        }

        if (courseDataFile.Reflection != null)
        {
            Console.WriteLine("Dumping CourseData->Reflection");
            DumpModelSet(courseDataFile.Reflection, Path.Combine(dir, "Reflection"));
        }

        if (courseDataFile.ReflectionMask != null)
        {
            Console.WriteLine("Dumping CourseData->ReflectionMask");
            DumpModelSet(courseDataFile.ReflectionMask, Path.Combine(dir, "ReflectionMask"));
        }

        if (courseDataFile.After != null)
        {
            Console.WriteLine("Dumping CourseData->Sky");
            DumpModelSet(courseDataFile.Sky, Path.Combine(dir, "Sky"));
        }

        if (courseDataFile.After != null)
        {
            Console.WriteLine("Dumping CourseData->Sky");
            DumpModelSet(courseDataFile.After, Path.Combine(dir, "Sky"));
        }

        if (courseDataFile.Far != null)
        {
            Console.WriteLine("Dumping CourseData->Far");
            DumpModelSet(courseDataFile.Far, Path.Combine(dir, "Far"));
        }

        if (courseDataFile.EnvSky != null)
        {
            Console.WriteLine("Dumping CourseData->EnvSky");
            DumpModelSet(courseDataFile.EnvSky, Path.Combine(dir, "EnvSky"));
        }

        if (courseDataFile.MirrorSky != null)
        {
            Console.WriteLine("Dumping CourseData->MirrorSky");
            DumpModelSet(courseDataFile.MirrorSky, Path.Combine(dir, "MirrorSky"));
        }

        if (courseDataFile.RaceSmoke != null)
        {
            Console.WriteLine("Dumping CourseData->RaceSmoke");
            DumpTextureSet(courseDataFile.RaceSmoke, Path.Combine(dir, "RaceSmoke"));
        }

        if (courseDataFile.MiniMap != null)
        {
            Console.WriteLine("Dumping CourseData->MiniMap");
            DumpTextureSet(courseDataFile.MiniMap.TextureSet, Path.Combine(dir, "MiniMap"));
        }

        if (courseDataFile.UnusedSphereReflectionTexture != null)
        {
            Console.WriteLine("Dumping CourseData->UnusedSphereReflectionTexture");
            DumpTextureSet(courseDataFile.UnusedSphereReflectionTexture, Path.Combine(dir, "UnusedSphereReflectionTexture"));
        }

        if (courseDataFile.FlareShape != null)
        {
            Console.WriteLine("Dumping CourseData->FlareShape");
            DumpShape(courseDataFile.FlareShape, Path.Combine(dir, "FlareShape"));
        }

        if (courseDataFile.FlareTexture != null)
        {
            Console.WriteLine("Dumping CourseData->FlareTexture");
            DumpTextureSet(courseDataFile.FlareTexture, Path.Combine(dir, "FlareTexture"));
        }

        if (courseDataFile.ParticleTexture != null)
        {
            Console.WriteLine("Dumping CourseData->ParticleTexture");
            DumpTextureSet(courseDataFile.ParticleTexture, Path.Combine(dir, "ParticleTexture"));
        }

        if (courseDataFile.FlareReflection != null)
        {
            Console.WriteLine("Dumping CourseData->FlareReflection");
            DumpTextureSet(courseDataFile.ParticleTexture, Path.Combine(dir, "FlareReflection"));
        }

        if (courseDataFile.ReflectionUnk0xC8 != null)
        {
            Console.WriteLine("Dumping CourseData->ReflectionUnk0xC8");
            DumpModelSet(courseDataFile.ReflectionUnk0xC8, Path.Combine(dir, "ReflectionUnk0xC8"));
        }

        if (courseDataFile.FgSky != null)
        {
            Console.WriteLine("Dumping CourseData->FgSky");
            DumpModelSet(courseDataFile.FgSky, Path.Combine(dir, "FgSky"));
        }
    }
}
