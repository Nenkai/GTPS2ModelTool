using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using NLog;

using PDTools.Files.Models.PS2.Commands;
using PDTools.Files.Models.PS2;
using PDTools.Files.Textures.PS2;
using PDTools.Files.Models.PS2.ModelSet;

using GTPS2ModelTool.Core.WavefrontObj;
using GTPS2ModelTool.Core.Config;
using GTPS2ModelTool.Core.Config.Callbacks;
using System.Diagnostics;

namespace GTPS2ModelTool.Core;

/// <summary>
/// Gran Turismo Model Set Builder.
/// </summary>
public class ModelSetBuilder
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Underlaying model set
    private readonly ModelSetBuildVersion _version;
    private readonly ModelSetPS2Base _set;

    // Configuration
    private ModelSetConfig _modelSetConfig;
    private string _configPath;
    private string _configDir;

    private List<ModelSetupPS2Command> _currentCommandList;

    // One per LOD
    private readonly List<TextureSetBuilder> _textureSetBuilders = [];

    // Keeps track of the added textures
    // One per LOD. Should be synced to the texture set builders ^
    private readonly List<List<string>> _texSetTexturesPerLOD = [];

    // Keeps tracks of added materials as we are adding distinct materials only to save on size
    private readonly List<int> _materialHashes = [];

    // Keeps track of each model in general
    private readonly Dictionary<string, ModelBuildInfo> _modelInfos = [];
    private ModelBuildInfo _currentModelInfo;

    private readonly RenderCommandContext _renderCommandContext = new();

    private bool hasFogCol = false;

    private byte _currentLOD;

    public ModelSetBuilder(ModelSetBuildVersion version)
    {
        if (version == ModelSetBuildVersion.ModelSet1)
            _set = new ModelSet1();
        else if (version == ModelSetBuildVersion.ModelSet2)
            _set = new ModelSet2();
        else
            throw new NotImplementedException($"Not implemented version {version}");

        _version = version;
    }

    /// <summary>
    /// Inits/Adds models from a model set config file.
    /// </summary>
    /// <param name="confFile"></param>
    /// <returns></returns>
    public bool InitFromConfig(string confFile)
    {
        if (File.Exists(confFile))
        {
            string confFileText = File.ReadAllText(confFile);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance)
                .Build();

            _modelSetConfig = deserializer.Deserialize<ModelSetConfig>(confFileText);
        }
        else
        {
            Logger.Error($"ModelSet Config file {confFile} does not exist.");
            return false;
        }

        _configPath = confFile;
        _configDir = Path.GetDirectoryName(confFile);

        foreach (var model in _modelSetConfig.Models)
        {
            string modelName = model.Key;
            ModelConfig modelConfig = model.Value;

            Logger.Info("{model:l}|Adding model {modelName} with {lodCount} LODs", modelName, modelName, model.Value.LODs.Count);
            if (!AddModel(model.Key, modelConfig))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a new model to the set.
    /// </summary>
    /// <param name="config">Model config.</param>
    /// <returns></returns>
    public bool AddModel(string name, ModelConfig config)
    {
        ModelPS2Base model = GetNewModel();
        var mainRenderCommand = new Cmd_BBoxRender();
        var lodCommand = new Cmd_LODSelect(); // May or may not be inserted if there's only 1 lod
        lodCommand.Unk2 = 3.0f; // Distance?

        // Current model's min/max boundaries to calculate the bbox
        Vector3[] modelMinMax = new Vector3[2];

        model.Commands.Add(mainRenderCommand);

        _currentCommandList = mainRenderCommand.CommandsOnRender;

        var modelInfo = new ModelBuildInfo();
        _modelInfos.Add(name, modelInfo);
        _currentModelInfo = modelInfo;
        _currentModelInfo.Name = name;

        int numVariations = _modelSetConfig?.NumVariations ?? 1;
        for (int varIndex = 0; varIndex < numVariations; varIndex++)
        {
            // Process each lod
            _currentLOD = 0;

            foreach (var lod in config.LODs)
            {
                string varPath = numVariations == 1 ? lod.Key : Path.Combine($"Var{varIndex}", lod.Key);
                string lodObjPath = GetModelComponentPath(varPath);

                if (!File.Exists(lodObjPath))
                {
                    string pathWithObjExt = lodObjPath + ".obj";
                    if (!File.Exists(pathWithObjExt))
                    {
                        Logger.Fatal("YAML Error: Unable to find obj file {objFile} referenced by lod.", lodObjPath);
                        return false;
                    }
                    else
                        lodObjPath = pathWithObjExt;
                }


                LODConfig lodConfig = lod.Value ?? new LODConfig();
                string dir = Path.GetDirectoryName(lodObjPath);

                var lodModelObj = ModelObject.LoadFromFile(lodObjPath);
                lodModelObj.Meshes = lodModelObj.Meshes.OrderBy(e => e.Key).ToDictionary(e => e.Key, e => e.Value);

                // Empty object?
                if (varIndex > 0) // GT3 Variations work mainly through texture set clut patches
                {
                    if (_version == ModelSetBuildVersion.ModelSet1)
                    {
                        LogInfo("Processing variation #{varIndex}", varIndex);
                        if (!ProcessModelSet1Variation(varIndex, lodModelObj, dir))
                            return false;
                    }
                    else
                        throw new NotSupportedException("Variations for ModelSet2 are not supported yet.");
                }
                else
                {
                    while (_texSetTexturesPerLOD.Count <= _currentLOD)
                        _texSetTexturesPerLOD.Add([]);

                    while (_currentModelInfo.TexturesPerLOD.Count <= _currentLOD)
                        _currentModelInfo.TexturesPerLOD.Add([]);
                    

                    LogInfo("Adding lod {lodName} with {shapeCount} shapes.", lod.Key, lodModelObj.Meshes.Count);

                    // Process materials
                    Dictionary<string, uint> shapeNameToIndex = [];
                    if (lodModelObj.MaterialObject is not null && lodModelObj.MaterialObject.Materials.Count > 0)
                    {
                        LogInfo("Materials: Processing...");
                        for (int objMatIndex = 0; objMatIndex < lodModelObj.MaterialObject.Materials.Count; objMatIndex++)
                        {
                            Material? material = lodModelObj.MaterialObject.Materials[objMatIndex];
                            LogInfo("Material {matIndex}/{matCount}: Adding {matName}...", objMatIndex + 1, lodModelObj.MaterialObject.Materials.Count, material.Name);

                            int pgluMatIndex = AddMaterial(dir, material);

                            // Remap obj material indices to model set mat indices
                            if (objMatIndex != pgluMatIndex)
                                lodModelObj.RemapMaterialIndices(objMatIndex, pgluMatIndex);
                        }
                    }

                    GetOrCreateLODTexSetBuilder(_currentLOD);

                    Vector3[] lodMinMax = ComputeModelMinMax(lodModelObj);
                    CalculateNewBoundings(modelMinMax, lodMinMax);

                    if (_currentLOD == 0)
                    {
                        if (config.LODs.Count > 1)
                        {
                            mainRenderCommand.CommandsOnRender.Add(lodCommand);
                            lodCommand.SetNumberOfLODs(config.LODs.Count);
                        }
                    }

                    _currentCommandList = config.LODs.Count > 1 ? lodCommand.CommandsPerLOD[_currentLOD] : mainRenderCommand.CommandsOnRender;

                    if (_currentLOD > byte.MaxValue)
                        _currentCommandList.Add(new Cmd_pgluSetTexTable_UShort() { TexSetTableIndex = _currentLOD });
                    else
                        _currentCommandList.Add(new Cmd_pgluSetTexTable_Byte() { TexSetTableIndex = _currentLOD });

                    int j = 0;
                    foreach (ModelMesh mesh in lodModelObj.Meshes.Values)
                    {
                        LogInfo("Shape {shapeIndex}/{shapeCount}: Adding new shape {shapeName}...", j + 1, lodModelObj.Meshes.Count, mesh.Name);
                        uint shapeIndex = BuildNewShape(lodModelObj, lodConfig, mesh);

                        if (!IsCallbackShape(lodConfig, mesh))
                            AddModelShapeWithParameters(lodConfig, mesh, shapeIndex);

                        shapeNameToIndex.Add(mesh.Name, shapeIndex);
                        j++;
                    }

                    ProcessCallbacks(lodModelObj, lodConfig, shapeNameToIndex);
                    LogInfo("Added LOD{lod} with {shapeCount} shapes.", _currentLOD, lodModelObj.Meshes.Count);
                }

                _currentLOD++;
            }
        }

        // Restore render parameters
        RestoreDefaultRenderParameters();

        mainRenderCommand.SetBBox(GetBoundaryBoxFromMinMax(modelMinMax[0], modelMinMax[1]));
        mainRenderCommand.CommandsOnRender.Add(new Cmd_pglEnableRendering());

        // Has set fog color? store it at the start, restore it at the end
        if (_renderCommandContext.FogColorWasSet)
        {
            model.Commands.Insert(0, new Cmd_pglStoreFogColor());
            model.Commands.Add(new Cmd_pglCopyFogColor());
        }

        // For car models:
        // - Model 1 = Main body
        // - Model 2 = Shadow

        if (_set is ModelSet1 set1)
        {
            set1.Models.Add(model as ModelSet1Model);
            set1.Boundings.Add(new ModelSet1Bounding() { Value = Vector4.Zero });
        }
        else if (_set is ModelSet2 set2)
        {
            set2.Models.Add(model as ModelSet2Model);
        }

        LogInfo($"Added model with {config.LODs.Count} LODs. Bounds: Min {modelMinMax[0]}, Max {modelMinMax[1]}");
        return true;
    }

    private bool ProcessModelSet1Variation(int varIndex, ModelObject lodModelObj, string variationDir)
    {
        List<string> expectedTexturesThisLOD = _currentModelInfo.TexturesPerLOD[_currentLOD].Keys.ToList();

        // Check that all the textures in this variation are present in the base model variation
        var texturesToMatch = expectedTexturesThisLOD.ToDictionary(k => k, v => false); // k = texture name, v = matched
        foreach (var mat in lodModelObj.MaterialObject.Materials)
        {
            if (string.IsNullOrEmpty(mat.MapDiffuse))
                continue;

            string filePath = Path.Combine(variationDir, mat.MapDiffuse);
            if (!texturesToMatch.ContainsKey(mat.MapDiffuse))
            {
                LogInfo("Variation {varIndex} has an extra unexpected texture {texture}", varIndex, mat.MapDiffuse);
                return false;
            }

            texturesToMatch[mat.MapDiffuse] = true;
        }

        if (texturesToMatch.Count(e => e.Value) != expectedTexturesThisLOD.Count)
        {
            Logger.Error("LOD{lod}|Variation #{varIndex} is missing {textureCount} textures:", _currentLOD, varIndex, texturesToMatch.Count);
            foreach (var textureFileKv in texturesToMatch)
            {
                if (!textureFileKv.Value)
                    Logger.Error("- {name}", textureFileKv.Key);
            }
            return false;
        }

        // Ensure they're in the same order
        if (!texturesToMatch.Keys.SequenceEqual(expectedTexturesThisLOD))
        {
            Logger.Error("LOD{lod}|Variation #{i} has different texture order than base variation.", _currentLOD, varIndex, texturesToMatch.Count);
            return false;
        }

        // Ensure they have the same dimensions
        TextureSetBuilder texSetBuilder = _textureSetBuilders[_currentLOD];

        foreach (string textureName in texturesToMatch.Keys)
        {
            int pgluTextureIndex = _currentModelInfo.TexturesPerLOD[_currentLOD][textureName].PGLUTextureIndex;

            string filePath = Path.Combine(variationDir, textureName);
            ImageInfo imageInfo = Image.Identify(filePath);

            TextureTask textureTask = texSetBuilder.Textures[pgluTextureIndex];

            if (textureTask.Image.Size != imageInfo.Size)
            {
                Logger.Error("LOD{lod}|Variation #{i} has different texture dimensions (texture #{textureId} - {textureName})", _currentLOD,  varIndex, pgluTextureIndex, textureName);
                return false;
            }

            texSetBuilder.AddClutPatch(varIndex, pgluTextureIndex, filePath);
        }

        return true;
    }

    /// <summary>
    /// Builds and returns the model set.
    /// </summary>
    /// <returns></returns>
    public ModelSetPS2Base Build()
    {
        var texSetList = _set.GetTextureSetList();

        for (int lod = 0; lod < _textureSetBuilders.Count; lod++)
        {
            TextureSetBuilder lodTexSetBuilder = _textureSetBuilders[lod];
            Logger.Info("Building LOD{lod} texture set with {textureCount} textures..", lod, lodTexSetBuilder.TextureCount);
            TextureSet1 texSet = lodTexSetBuilder.Build();
            texSetList.Add(texSet);

            if (texSet.TotalBlockSize >= 0x640)
            {
                Logger.Warn("LOD{lod} Texture Set block size 0x{blockSize:X4} >= 0x{maxBlocks:X4} - might cause GS memory overrun & corruption.", lod, texSet.TotalBlockSize, 0x640);
                Logger.Warn("Consider using textures with dimensions power of 2, different pixel formats or smaller textures in general.");
            }

            Logger.Info("Built LOD{lod} texture set. Total block size: 0x{blockSize:l}", lod, texSet.TotalBlockSize.ToString("X4"));
        }

        // At least have one texture set
        if (texSetList.Count == 0)
            texSetList.Add(new TextureSet1());

        // Same for materials
        if (_set.GetMaterialCount() == 0)
            _set.AddMaterial(new PGLUmaterial());

        if (_set is ModelSet1 _modelSet1)
        {
            for (int i = 0; i < texSetList[0].ClutPatchSet.Count; i++)
                _modelSet1.VariationMaterialsTable.Add(_modelSet1.Materials);
        }
        return _set;
    }

    private uint BuildNewShape(ModelObject modelObj, LODConfig lodConfig, ModelMesh mesh)
    {
        bool externalTexture = false;
        if (lodConfig.MeshParameters.TryGetValue(mesh.Name, out MeshConfig meshConfig))
        {
            if (meshConfig.UseExternalTexture)
            {
                LogInfo("Shape '{shapeName}' marked as using external texture.", mesh.Name);
                externalTexture = true;
            }
        }
        else
        {
            if (mesh.Name != "_default_")
                throw new Exception($"Shape {mesh.Name} present in model file, but not in config");
        }

        var shape = PGLUShapeBuilder.BuildShape(mesh, _texSetTexturesPerLOD[_currentLOD], modelObj.MaterialObject?.Materials ?? [], externalTexture);
        LogInfo("Tri-striped mesh into {vifPacket} vif packets - {shape.NumTriangles} triangles, {shape.TotalStripVerts} strip points, unk1: {shape.Unk1}", 
            shape.VIFPackets.Count, shape.NumTriangles, shape.TotalStripVerts, shape.Unk1);

        uint shapeIndex = _set.AddShape(shape);
        return shapeIndex;
    }

    /// <summary>
    /// Adds a material to the model set
    /// </summary>
    /// <param name="config"></param>
    /// <param name="textureSetBuilder"></param>
    /// <param name="dir"></param>
    /// <param name="material"></param>
    /// <returns><see cref="PGLUmaterial"/> index</returns>
    private int AddMaterial(string dir, Material material)
    {
        if (!string.IsNullOrEmpty(material.MapDiffuse) && !_texSetTexturesPerLOD[_currentLOD].Contains(material.MapDiffuse))
        {
            string filePath = Path.Combine(dir, material.MapDiffuse);
            using var img = Image.Load<Rgba32>(filePath);

            LogInfo("Texture: Adding referenced diffuse {name} ({width}x{height})", material.MapDiffuse, img.Width, img.Height);

            if (!BitOperations.IsPow2(img.Width) || !BitOperations.IsPow2(img.Height))
                Logger.Warn($"Image dimensions for '{filePath}' is not power of two ({img.Width}x{img.Height}), will be resized.");

            SCE_GS_PSM format;
            if (_modelSetConfig is not null && _modelSetConfig.Textures.TryGetValue(material.MapDiffuse, out TextureConfig textureConfig))
            {
                format = textureConfig.Format;
                textureConfig.IsTextureMap = true;
                LogInfo($"Texture format for '{material.MapDiffuse}': {format} (U:{textureConfig.WrapModeS}, V:{textureConfig.WrapModeT})");
            }
            else
            {
                textureConfig = new TextureConfig();
                textureConfig.IsTextureMap = true;
                LogInfo($"No texture format config set for '{material.MapDiffuse}', defaulting to format {textureConfig.Format}");
            }

            var currentLodTexSetBuilder = GetOrCreateLODTexSetBuilder(_currentLOD);
            currentLodTexSetBuilder.AddImage(filePath, textureConfig);

            _texSetTexturesPerLOD[_currentLOD].Add(material.MapDiffuse);

            _currentModelInfo.TexturesPerLOD[_currentLOD].Add(material.MapDiffuse, new TextureBuildInfo()
            {
                Name = material.MapDiffuse,
                PGLUTextureIndex = _texSetTexturesPerLOD[_currentLOD].Count - 1,
            });
        }

        PGLUmaterial pgluMaterial = new()
        {
            Ambient = material.Ambient,
            Diffuse = material.Diffuse,
            Specular = material.Specular,
            UnkColor = material.Emissive,
            
        };

        // Avoid adding duplicate materials to save on size
        int matHash = pgluMaterial.GetHashCode();
        int idx = _materialHashes.IndexOf(pgluMaterial.GetHashCode());
        if (idx != -1)
            return idx;

        int matIndex = _set.AddMaterial(pgluMaterial);
        _materialHashes.Add(matHash);

        return matIndex;
    }

    private void ProcessCallbacks(ModelObject lodModelObj, LODConfig lodConfig, Dictionary<string, uint> shapeNameToIndex)
    {
        ProcessTailLampCallback(lodModelObj, lodConfig, shapeNameToIndex);
    }

    private void ProcessTailLampCallback(ModelObject lodModelObj, LODConfig lodConfig, Dictionary<string, uint> shapeNameToIndex)
    {
        if (lodConfig.TailLampCallback is not null)
        {
            TailLampCallback tailLampCallback = lodConfig.TailLampCallback;

            var callbackCmd = new Cmd_CallModelCallback();
            callbackCmd.CommandsPerBranch.Add([]); // 'Off' command list
            callbackCmd.CommandsPerBranch.Add([]); // 'On' command list

            foreach (string shapeName in tailLampCallback.Off)
            {
                if (!lodModelObj.Meshes.TryGetValue(shapeName, out ModelMesh mesh))
                {
                    Logger.Error($"TailLamp callback - off shape '{shapeName}' does not exist in obj file.");
                }
                else
                {
                    LogInfo("Adding shape {shapeName} for Car Tail Lamp (Off) callback", shapeName);
                    uint index = shapeNameToIndex[shapeName];

                    var tmpCommandList = _currentCommandList;
                    _currentCommandList = callbackCmd.CommandsPerBranch[0];

                    AddModelShapeWithParameters(lodConfig, mesh, index);

                    _currentCommandList = tmpCommandList;
                }
            }

            foreach (string shapeName in tailLampCallback.On)
            {
                if (!lodModelObj.Meshes.TryGetValue(shapeName, out ModelMesh mesh))
                {
                    Logger.Error($"TailLamp callback - off shape '{shapeName}' does not exist in obj file.");
                }
                else
                {
                    LogInfo($"Adding shape {shapeName} for Car Tail Lamp (On) callback", shapeName);
                    uint index = shapeNameToIndex[shapeName];

                    var tmpCommandList = _currentCommandList;
                    _currentCommandList = callbackCmd.CommandsPerBranch[1];

                    AddModelShapeWithParameters(lodConfig, mesh, index);

                    _currentCommandList = tmpCommandList;
                }
            }

            _currentCommandList.Add(callbackCmd);
        }
    }

    /// <summary>
    /// Adds a shape to be rendered and its belonging render parameters.
    /// </summary>
    /// <param name="lodConfig"></param>
    /// <param name="mesh"></param>
    /// <param name="shapeIndex"></param>
    private void AddModelShapeWithParameters(LODConfig lodConfig, ModelMesh mesh, uint shapeIndex)
    {
        lodConfig.MeshParameters.TryGetValue(mesh.Name, out MeshConfig meshConfig);
        if (meshConfig is not null && meshConfig.CommandsBefore?.Count > 0)
        {
            var cmds = RenderCommandParser.ParseCommands(meshConfig.CommandsBefore);

            Logger.Info("Adding {meshName} commands...", mesh.Name);

            // Restore any of these parameters if they were set previously set and are not present for this one
            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglAlphaFail))
                RestoreAlphaFail();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglAlphaFunc))
                RestoreAlphaTestFunc();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglDisableAlphaTest))
                RestoreAlphaTest();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglEnableDestinationAlphaTest))
                RestoreDestinationAlphaTest();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglSetDestinationAlphaFunc))
                RestoreDestinationAlphaFunc();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglBlendFunc))
                RestoreBlendFunc();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglDisableDepthMask))
                RestoreDepthMask();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglDepthBias))
                RestoreDepthBias();

            if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglGT3_2_1ui || e.Opcode == ModelSetupPS2Opcode.pglGT3_2_4f))
                RestoreGT3_2();


            foreach (var cmd in cmds)
            {
                Logger.Info($"- {cmd.Opcode}");

                if (_renderCommandContext.ApplyCommand(cmd))
                {
                    // Avoid adding duplicate render commands to save on size
                    _currentCommandList.Add(cmd);
                }
            }
        }
        else
            RestoreDefaultRenderParameters();

        if (shapeIndex > byte.MaxValue)
            _currentCommandList.Add(new Cmd_pgluCallShape_UShort() { ShapeIndex = (ushort)shapeIndex });
        else
            _currentCommandList.Add(new Cmd_pgluCallShapeByte() { ShapeIndex = (byte)shapeIndex });
    }

    private void RestoreDefaultRenderParameters()
    {
        RestoreAlphaTest();
        RestoreAlphaFail();
        RestoreAlphaTestFunc();
        RestoreDepthBias();
        RestoreDestinationAlphaTest();
        RestoreDestinationAlphaFunc();
        RestoreDepthMask();
        RestoreBlendFunc();
        RestoreColorMask();
        RestoreFogColor();
        RestoreGT3_2();
    }

    private void RestoreAlphaTest()
    {
        if (!_renderCommandContext.IsDefaultAlphaTest())
            _currentCommandList.Add(new Cmd_pglEnableAlphaTest());
        _renderCommandContext.AlphaTest = RenderCommandContext.DEFAULT_ALPHA_TEST;
    }

    private void RestoreAlphaFail()
    {
        if (!_renderCommandContext.IsDefaultAlphaFail())
            _currentCommandList.Add(new Cmd_pglAlphaFail(RenderCommandContext.DEFAULT_ALPHA_FAIL));
        _renderCommandContext.AlphaFail = RenderCommandContext.DEFAULT_ALPHA_FAIL;
    }

    private void RestoreAlphaTestFunc()
    {
        if (!_renderCommandContext.IsDefaultAlphaTestFunc())
            _currentCommandList.Add(new Cmd_pglAlphaFunc(RenderCommandContext.DEFAULT_ALPHA_TEST_FUNC, RenderCommandContext.DEFAULT_ALPHA_TEST_REF));
        _renderCommandContext.AlphaTestFunc = RenderCommandContext.DEFAULT_ALPHA_TEST_FUNC;
        _renderCommandContext.AlphaTestRef = RenderCommandContext.DEFAULT_ALPHA_TEST_REF;
    }

    private void RestoreDepthBias()
    {
        if (!_renderCommandContext.IsDefaultDepthBias())
            _currentCommandList.Add(new Cmd_pglDepthBias(RenderCommandContext.DEFAULT_DEPTH_BIAS));
        _renderCommandContext.DepthBias = RenderCommandContext.DEFAULT_DEPTH_BIAS;
    }

    private void RestoreDestinationAlphaTest()
    {
        if (!_renderCommandContext.IsDefaultDestinationAlphaTest())
            _currentCommandList.Add(new Cmd_pglDisableDestinationAlphaTest());
        _renderCommandContext.DestinationAlphaTest = false;
    }

    private void RestoreDestinationAlphaFunc()
    {
        if (!_renderCommandContext.IsDefaultDestinationAlphaFunc())
            _currentCommandList.Add(new Cmd_pglSetDestinationAlphaFunc(RenderCommandContext.DEFAULT_DESTINATION_ALPHA_FUNC));
        _renderCommandContext.DestinationAlphaFunc = RenderCommandContext.DEFAULT_DESTINATION_ALPHA_FUNC;
    }

    private void RestoreDepthMask()
    {
        if (!_renderCommandContext.IsDefaultDepthMask())
            _currentCommandList.Add(new Cmd_pglEnableDepthMask());
        _renderCommandContext.DepthMask = true;
    }

    private void RestoreColorMask()
    {
        if (!_renderCommandContext.IsDefaultColorMask())
            _currentCommandList.Add(new Cmd_pglColorMask(0)); // equates to ~0
        _renderCommandContext.ColorMask = RenderCommandContext.DEFAULT_COLOR_MASK;
    }

    private void RestoreBlendFunc()
    {
        if (!_renderCommandContext.IsDefaultBlendFunc())
            _currentCommandList.Add(new Cmd_pglBlendFunc(RenderCommandContext.DEFAULT_BLENDFUNC_A, 
                RenderCommandContext.DEFAULT_BLENDFUNC_B, 
                RenderCommandContext.DEFAULT_BLENDFUNC_C, 
                RenderCommandContext.DEFAULT_BLENDFUNC_D, 
                RenderCommandContext.DEFAULT_BLENDFUNC_FIX));

        _renderCommandContext.BlendFunc_A = RenderCommandContext.DEFAULT_BLENDFUNC_A;
        _renderCommandContext.BlendFunc_B = RenderCommandContext.DEFAULT_BLENDFUNC_B;
        _renderCommandContext.BlendFunc_C = RenderCommandContext.DEFAULT_BLENDFUNC_C;
        _renderCommandContext.BlendFunc_D = RenderCommandContext.DEFAULT_BLENDFUNC_D;
        _renderCommandContext.BlendFunc_FIX = RenderCommandContext.DEFAULT_BLENDFUNC_FIX;
    }

    private void RestoreFogColor()
    {
        if (!_renderCommandContext.IsDefaultFogColor())
            _currentCommandList.Add(new Cmd_pglCopyFogColor());

        _renderCommandContext.FogColor = null;
    }

    private void RestoreGT3_2()
    {
        if (_version != ModelSetBuildVersion.ModelSet1)
            return;

        if (!_renderCommandContext.IsDefaultGT3_2())
            _currentCommandList.Add(new Cmd_GT3_2_1ui(0));

        _renderCommandContext.UnkGT3_2_R = RenderCommandContext.DEFAULT_GT3_2_R;
        _renderCommandContext.UnkGT3_2_G = RenderCommandContext.DEFAULT_GT3_2_G;
        _renderCommandContext.UnkGT3_2_B = RenderCommandContext.DEFAULT_GT3_2_B;
        _renderCommandContext.UnkGT3_2_A = RenderCommandContext.DEFAULT_GT3_2_A;
    }

    // Utils-ish

    public void LogInfo(string message, params object[] args)
    {
        var arr = new object[args.Length + 2];
        arr[0] = _currentModelInfo.Name;
        arr[1] = _currentLOD;
        for (int i = 0; i < args.Length; i++)
            arr[i+2] = args[i];

        Logger.Info("{modelName:l}|LOD{lod}|" + message, arr);
    }

    private string GetModelComponentPath(string file)
    {
        string componentPath = string.Empty;
        if (!string.IsNullOrEmpty(_configPath))
            componentPath = Path.Combine(_configDir, file);

        if (string.IsNullOrEmpty(componentPath) || !File.Exists(componentPath) && !File.Exists(componentPath + ".obj"))
            componentPath = file;

        return componentPath;
    }

    private TextureSetBuilder GetOrCreateLODTexSetBuilder(byte lod)
    {
        TextureSetBuilder texSetBuilder;
        if (_textureSetBuilders.Count < _currentLOD + 1)
        {
            texSetBuilder = new TextureSetBuilder();
            _textureSetBuilders.Add(texSetBuilder);
        }
        else
        {
            texSetBuilder = _textureSetBuilders[_currentLOD];
        }

        return texSetBuilder;
    }

    private static bool IsCallbackShape(LODConfig lodConfig, ModelMesh mesh)
    {
        if (lodConfig.TailLampCallback is not null)
        {
            TailLampCallback tailLampCallback = lodConfig.TailLampCallback;
            if (tailLampCallback.Off.Contains(mesh.Name))
                return true;

            if (tailLampCallback.On.Contains(mesh.Name))
                return true;
        }

        return false;
    }

    private static void CalculateNewBoundings(Vector3[] modelMinMax, Vector3[] lodMinMax)
    {
        if (lodMinMax[0].X < modelMinMax[0].X)
            modelMinMax[0].X = lodMinMax[0].X;
        if (lodMinMax[0].Y < modelMinMax[0].Y)
            modelMinMax[0].Y = lodMinMax[0].Y;
        if (lodMinMax[0].Z < modelMinMax[0].Z)
            modelMinMax[0].Z = lodMinMax[0].Z;

        if (lodMinMax[1].X > modelMinMax[1].X)
            modelMinMax[1].X = lodMinMax[1].X;
        if (lodMinMax[1].Y > modelMinMax[1].Y)
            modelMinMax[1].Y = lodMinMax[1].Y;
        if (lodMinMax[1].Z > modelMinMax[1].Z)
            modelMinMax[1].Z = lodMinMax[1].Z;
    }

    private ModelPS2Base GetNewModel()
    {
        return _version switch
        {
            ModelSetBuildVersion.ModelSet1 => new ModelSet1Model(),
            ModelSetBuildVersion.ModelSet2 => new ModelSet2Model(),
            _ => throw new NotImplementedException(),
        };
    }

    private static Vector3[] ComputeModelMinMax(ModelObject modelObj)
    {
        // Compute bbox
        float xmin = 0, ymin = 0, zmin = 0, xmax = 0, ymax = 0, zmax = 0;
        foreach (ModelMesh mesh in modelObj.Meshes.Values)
        {
            for (int j = 0; j < mesh.Faces.Count; j++)
            {
                Face face = mesh.Faces[j];
                ProcessVertex(face.Vert1.Position);
                ProcessVertex(face.Vert2.Position);
                ProcessVertex(face.Vert3.Position);

                void ProcessVertex(Vector3 vert)
                {
                    if (vert.X < xmin)
                        xmin = vert.X;

                    if (vert.Y < ymin)
                        ymin = vert.Y;

                    if (vert.Z < zmin)
                        zmin = vert.Z;

                    if (vert.X > xmax)
                        xmax = vert.X;

                    if (vert.Y > ymax)
                        ymax = vert.Y;

                    if (vert.Z > zmax)
                        zmax = vert.Z;
                }
            }

        }

        return
        [
            new(xmin, ymin, zmin),
            new(xmax, ymax, zmax)
        ];
    }

    private static Vector3[] GetBoundaryBoxFromMinMax(Vector3 min, Vector3 max)
    {
        return
        [       
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, max.Z),
        ];
    }
}

public class ModelBuildInfo
{
    public string Name { get; set; }
    public List<Dictionary<string, TextureBuildInfo>> TexturesPerLOD = [];
}

public class TextureBuildInfo
{
    public string Name { get; set; }
    public int PGLUTextureIndex { get; set; }
}

public enum ModelSetBuildVersion
{
    ModelSet1,
    ModelSet2
}
