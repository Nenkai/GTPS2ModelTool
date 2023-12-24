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
using System.Runtime.Intrinsics.X86;
using NLog.Fluent;

namespace GTPS2ModelTool.Core
{
    public class ModelSetBuilder
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        // Underlaying model set
        private ModelSetBuildVersion _version;
        private ModelSetPS2Base _set;

        // Configuration
        private ModelSetConfig _modelSetConfig;
        private string _configPath;
        private string _configDir;

        private List<ModelSetupPS2Command> _currentCommandList;

        // One per LOD
        private List<TextureSetBuilder> _textureSetBuilders = new List<TextureSetBuilder>();

        // Keeps track of the added textures
        // One per LOD
        private List<List<string>> _texturesPerLOD = new List<List<string>>();

        // Keeps tracks of added materials as we are adding distinct materials only to save on size
        private List<int> _materialHashes = new List<int>();

        private RenderCommandContext _renderCommandContext = new RenderCommandContext();

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

                Logger.Info($"Adding model {modelName} with {model.Value.LODs.Count} LODs");
                AddModel(modelConfig);
            }

            return true;
        }

        /// <summary>
        /// Adds a new model to the set.
        /// </summary>
        /// <param name="config">Model config.</param>
        /// <returns></returns>
        public void AddModel(ModelConfig config)
        {
            ModelPS2Base model = GetNewModel();
            var mainRenderCommand = new Cmd_BBoxRender();
            var lodCommand = new Cmd_LODSelect(); // May or may not be inserted if there's only 1 lod
            lodCommand.Unk2 = 3.0f; // Distance?

            // Current model's min/max boundaries to calculate the bbox
            Vector3[] modelMinMax = new Vector3[2];

            model.Commands.Add(mainRenderCommand);

            _currentCommandList = mainRenderCommand.CommandsOnRender;

            // Process each lod
            _currentLOD = 0;

            foreach (var lod in config.LODs)
            {
                string lodObjPath = GetModelComponentPath(lod.Key);

                var lodModelObj = ModelObject.LoadFromFile(lodObjPath);
                Logger.Info($"Adding LOD{_currentLOD}: {lod.Key} with {lodModelObj.Meshes.Count} meshes.");

                LODConfig lodConfig = lod.Value ?? new LODConfig();
                Dictionary<string, int> shapeNameToIndex = new Dictionary<string, int>();

                // Process materials
                string dir = Path.GetDirectoryName(lodObjPath);
                if (lodModelObj.MaterialObject is not null)
                {
                    Logger.Info($"Materials: Processing...");
                    _texturesPerLOD.Add(new List<string>());

                    for (int objMatIndex = 0; objMatIndex < lodModelObj.MaterialObject.Materials.Count; objMatIndex++)
                    {
                        Material? material = lodModelObj.MaterialObject.Materials[objMatIndex];
                        Logger.Info($"Material {objMatIndex + 1}/{lodModelObj.MaterialObject.Materials.Count}: Adding '{material.Name}'...");

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

                _currentCommandList.Add(new Cmd_pgluSetTexTable_UShort() { TexSetTableIndex = _currentLOD });

                int j = 0;
                foreach (ModelMesh mesh in lodModelObj.Meshes.Values)
                {
                    Logger.Info($"Mesh {j + 1}/{lodModelObj.Meshes.Count}: Adding new shape '{mesh.Name}'...");
                    int shapeIndex = BuildNewShape(lodModelObj, lodConfig, mesh);

                    if (!IsCallbackShape(lodConfig, mesh))
                        AddModelShapeWithParameters(lodConfig, mesh, shapeIndex);

                    shapeNameToIndex.Add(mesh.Name, shapeIndex);
                    j++;
                }

                ProcessCallbacks(lodModelObj, lodConfig, shapeNameToIndex);

                Logger.Info($"Added LOD{_currentLOD} with {lodModelObj.Meshes.Count} shapes.");
                _currentLOD++;
            }

            // Restore render parameters
            RestoreDefaultRenderParameters();

            mainRenderCommand.SetBBox(GetBoundaryBoxFromMinMax(modelMinMax[0], modelMinMax[1]));
            mainRenderCommand.CommandsOnRender.Add(new Cmd_pglEnableRendering());

            // Has set fog color? store it at the start, restore it at the end
            if (hasFogCol)
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

            Logger.Info($"Added model with {config.LODs.Count} LODs. Bounds: Min {modelMinMax[0]}, Max {modelMinMax[1]}");
        }

        /// <summary>
        /// Builds and returns the model set.
        /// </summary>
        /// <returns></returns>
        public ModelSetPS2Base Build()
        {
            var texSetList = _set.GetTextureSetList();

            for (int i = 0; i < _textureSetBuilders.Count; i++)
            {
                TextureSetBuilder lodTexSetBuilder = _textureSetBuilders[i];
                Logger.Info($"Building LOD{i} texture set with {lodTexSetBuilder.Textures.Count} textures..");
                TextureSet1 texSet = lodTexSetBuilder.Build();
                texSetList.Add(texSet);


                if (texSet.TotalBlockSize >= 0x640)
                {
                    Logger.Warn($"LOD{i} Texture Set block size 0x{texSet.TotalBlockSize:X4} >= 0x{0x640:X4} - might cause GS memory overrun & corruption.");
                    Logger.Warn($"Consider using textures with dimensions power of 2, different pixel formats or smaller textures in general.");
                }

                Logger.Info($"Built LOD{i} texture set. Total block size: 0x{texSet.TotalBlockSize:X4}");
            }

            // At least have one texture set
            if (texSetList.Count == 0)
                texSetList.Add(new TextureSet1());

            // Same for materials
            if (_set.GetMaterialCount() == 0)
                _set.AddMaterial(new PGLUmaterial());

            return _set;
        }

        private int BuildNewShape(ModelObject modelObj, LODConfig lodConfig, ModelMesh mesh)
        {
            bool externalTexture = false;
            if (lodConfig.MeshParameters.TryGetValue(mesh.Name, out MeshConfig meshConfig))
            {
                if (meshConfig.UseExternalTexture)
                {
                    Logger.Info($"Mesh '{mesh.Name}' marked as using external texture.");
                    externalTexture = true;
                }
            }

            var shape = PGLUShapeBuilder.BuildShape(mesh, _texturesPerLOD[_currentLOD], modelObj.MaterialObject?.Materials ?? new List<Material>(), externalTexture);
            int shapeIndex = _set.AddShape(shape);
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
            if (!string.IsNullOrEmpty(material.MapDiffuse))
            {
                if (!_texturesPerLOD[_currentLOD].Contains(material.MapDiffuse))
                {
                    string filePath = Path.Combine(dir, material.MapDiffuse);
                    var img = Image.Load<Rgba32>(filePath);

                    Logger.Info($"- Texture: Adding referenced diffuse '{material.MapDiffuse}' ({img.Width}x{img.Height})");

                    if (!BitOperations.IsPow2(img.Width) || !BitOperations.IsPow2(img.Height))
                        Logger.Warn($"Image dimensions for '{filePath}' is not power of two ({img.Width}x{img.Height}), will be resized.");

                    SCE_GS_PSM format;
                    if (_modelSetConfig is not null && _modelSetConfig.Textures.TryGetValue(material.MapDiffuse, out TextureConfig textureConfig))
                    {
                        format = textureConfig.Format;
                        textureConfig.IsTextureMap = true;
                        Logger.Info($"Texture format for '{material.MapDiffuse}': {format} (U:{textureConfig.WrapModeS}, V:{textureConfig.WrapModeT})");
                    }
                    else
                    {
                        textureConfig = new TextureConfig();
                        textureConfig.IsTextureMap = true;
                        Logger.Warn($"No texture format config set for '{material.MapDiffuse}', defaulting to format {textureConfig.Format}");
                    }

                    var currentLodTexSetBuilder = GetOrCreateLODTexSetBuilder(_currentLOD);
                    currentLodTexSetBuilder.AddImage(filePath, textureConfig);

                    _texturesPerLOD[_currentLOD].Add(material.MapDiffuse);
                }
            }

            PGLUmaterial pgluMaterial = new PGLUmaterial();
            pgluMaterial.Ambient = material.Ambient;
            pgluMaterial.Diffuse = material.Diffuse;
            pgluMaterial.Specular = material.Specular;

            // Avoid adding duplicate materials to save on siqxze
            int matHash = pgluMaterial.GetHashCode();
            int idx = _materialHashes.IndexOf(pgluMaterial.GetHashCode());
            if (idx != -1)
                return idx;

            int matIndex = _set.AddMaterial(pgluMaterial);
            _materialHashes.Add(matHash);

            return matIndex;
        }

        private void ProcessCallbacks(ModelObject lodModelObj, LODConfig lodConfig, Dictionary<string, int> shapeNameToIndex)
        {
            ProcessTailLampCallback(lodModelObj, lodConfig, shapeNameToIndex);
        }

        private void ProcessTailLampCallback(ModelObject lodModelObj, LODConfig lodConfig, Dictionary<string, int> shapeNameToIndex)
        {
            if (lodConfig.TailLampCallback is not null)
            {
                TailLampCallback tailLampCallback = lodConfig.TailLampCallback;

                var callbackCmd = new Cmd_CallModelCallback();
                callbackCmd.CommandsPerBranch.Add(new List<ModelSetupPS2Command>());
                callbackCmd.CommandsPerBranch.Add(new List<ModelSetupPS2Command>());

                foreach (string shapeName in tailLampCallback.Off)
                {
                    if (!lodModelObj.Meshes.TryGetValue(shapeName, out ModelMesh mesh))
                    {
                        Logger.Error($"TailLamp callback - off shape '{shapeName}' does not exist in obj file.");
                    }
                    else
                    {
                        Logger.Info($"Adding shape '{shapeName}' for Car Tail Lamp (Off) callback");
                        int index = shapeNameToIndex[shapeName];

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
                        Logger.Info($"Adding shape '{shapeName}' for Car Tail Lamp (On) callback");
                        int index = shapeNameToIndex[shapeName];

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
        private void AddModelShapeWithParameters(LODConfig lodConfig, ModelMesh mesh, int shapeIndex)
        {
            lodConfig.MeshParameters.TryGetValue(mesh.Name, out MeshConfig meshConfig);
            if (meshConfig is not null && meshConfig.CommandsBefore?.Count > 0)
            {
                var cmds = RenderCommandParser.ParseCommands(meshConfig.CommandsBefore);

                Logger.Info($"Adding '{mesh.Name}' commands...");

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

                if (!cmds.Any(e => e.Opcode == ModelSetupPS2Opcode.pglGT3_3_1ui || e.Opcode == ModelSetupPS2Opcode.pglGT3_3_4f))
                    RestoreGT3_3();

                foreach (var cmd in cmds)
                {
                    Logger.Info($"- {cmd.Opcode}");

                    // Avoid adding duplicate render commands to save on size
                    if (cmd.Opcode == ModelSetupPS2Opcode.pglAlphaFunc)
                    {
                        var alphaFunc = cmd as Cmd_pglAlphaFunc;
                        if (alphaFunc.TST != _renderCommandContext.AlphaTestFunc ||
                            alphaFunc.REF != _renderCommandContext.AlphaTestRef)
                        {
                            _renderCommandContext.AlphaTestFunc = alphaFunc.TST;
                            _renderCommandContext.AlphaTestRef = alphaFunc.REF;
                        }
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglAlphaFail)
                    {
                        var alphaFail = cmd as Cmd_pglAlphaFail;
                        if (alphaFail.FailMethod != _renderCommandContext.AlphaFail)
                            _renderCommandContext.AlphaFail = alphaFail.FailMethod;
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglDisableAlphaTest)
                    {
                        if (_renderCommandContext.AlphaTest)
                            _renderCommandContext.AlphaTest = false;
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglEnableDestinationAlphaTest)
                    {
                        if (!_renderCommandContext.DestinationAlphaTest)
                            _renderCommandContext.DestinationAlphaTest = true;
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglSetDestinationAlphaFunc)
                    {
                        var dest = cmd as Cmd_pglSetDestinationAlphaFunc;
                        if (dest.Func == _renderCommandContext.DestinationAlphaFunc)
                            continue;

                        _renderCommandContext.DestinationAlphaFunc = dest.Func;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglBlendFunc)
                    {
                        var blend = cmd as Cmd_pglBlendFunc;
                        if (blend.A == _renderCommandContext.BlendFunc_A &&
                            blend.B == _renderCommandContext.BlendFunc_B &&
                            blend.C == _renderCommandContext.BlendFunc_C &&
                            blend.D == _renderCommandContext.BlendFunc_D &&
                            blend.FIX == _renderCommandContext.BlendFunc_FIX)
                            continue;

                        _renderCommandContext.BlendFunc_A = blend.A;
                        _renderCommandContext.BlendFunc_B = blend.B;
                        _renderCommandContext.BlendFunc_C = blend.C;
                        _renderCommandContext.BlendFunc_D = blend.D;
                        _renderCommandContext.BlendFunc_FIX = blend.FIX;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglDisableDepthMask)
                    {
                        if (_renderCommandContext.DepthMask)
                            _renderCommandContext.DepthMask = false;
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglDepthBias)
                    {
                        var bias = cmd as Cmd_pglDepthBias;
                        if (bias.Value != 0.0f)
                            _renderCommandContext.DepthBias = bias.Value;
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglColorMask)
                    {
                        var colorMask = cmd as Cmd_pglColorMask;
                        if ((uint)((int)~colorMask.ColorMask) != _renderCommandContext.ColorMask)
                            _renderCommandContext.ColorMask = (uint)((int)~colorMask.ColorMask);
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglSetFogColor)
                    {
                        var fogCol = cmd as Cmd_pglSetFogColor;
                        if (fogCol.Color != _renderCommandContext.FogColor)
                        {
                            hasFogCol = true;
                            _renderCommandContext.FogColor = fogCol.Color;
                        }
                        else
                            continue;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglCopyFogColor)
                    {
                        _renderCommandContext.FogColor = null;
                    }
                    else if (cmd.Opcode == ModelSetupPS2Opcode.pglGT3_3_1ui || cmd.Opcode == ModelSetupPS2Opcode.pglGT3_3_4f)
                    {
                        if (cmd.Opcode == ModelSetupPS2Opcode.pglGT3_3_4f)
                        {
                            var gt3_3_4f = cmd as Cmd_GT3_3_4f;
                            if (gt3_3_4f.R == _renderCommandContext.UnkGT3_3_R &&
                                gt3_3_4f.G == _renderCommandContext.UnkGT3_3_G &&
                                gt3_3_4f.B == _renderCommandContext.UnkGT3_3_B &&
                                gt3_3_4f.A == _renderCommandContext.UnkGT3_3_A)
                                continue;

                            _renderCommandContext.UnkGT3_3_R = gt3_3_4f.R;
                            _renderCommandContext.UnkGT3_3_G = gt3_3_4f.G;
                            _renderCommandContext.UnkGT3_3_B = gt3_3_4f.B;
                            _renderCommandContext.UnkGT3_3_A = gt3_3_4f.A;
                        }
                        else if (cmd.Opcode == ModelSetupPS2Opcode.pglGT3_3_1ui)
                        {
                            var gt3_3_4f = cmd as Cmd_GT3_3_1ui;
                            if (gt3_3_4f.Color == _renderCommandContext.UnkGT3_3_R &&
                                gt3_3_4f.Color == _renderCommandContext.UnkGT3_3_G &&
                                gt3_3_4f.Color == _renderCommandContext.UnkGT3_3_B &&
                                gt3_3_4f.Color == _renderCommandContext.UnkGT3_3_A)
                                continue;

                            _renderCommandContext.UnkGT3_3_R = gt3_3_4f.Color;
                            _renderCommandContext.UnkGT3_3_G = gt3_3_4f.Color;
                            _renderCommandContext.UnkGT3_3_B = gt3_3_4f.Color;
                            _renderCommandContext.UnkGT3_3_A = 0.0f; // As per original code
                        }
                    }
                    

                    _currentCommandList.Add(cmd);
                }
            }
            else
                RestoreDefaultRenderParameters();

            if (shapeIndex > byte.MaxValue)
                _currentCommandList.Add(new Cmd_pgluCallShapeByte() { ShapeIndex = (byte)shapeIndex });
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
            RestoreGT3_3();
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
            if (!_renderCommandContext.IsdefaultDestinationAlphaTest())
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

        private void RestoreGT3_3()
        {
            if (_version != ModelSetBuildVersion.ModelSet1)
                return;

            if (!_renderCommandContext.IsDefaultGT3_3())
                _currentCommandList.Add(new Cmd_GT3_3_1ui(0));

            _renderCommandContext.UnkGT3_3_R = RenderCommandContext.DEFAULT_GT3_3_R;
            _renderCommandContext.UnkGT3_3_G = RenderCommandContext.DEFAULT_GT3_3_G;
            _renderCommandContext.UnkGT3_3_B = RenderCommandContext.DEFAULT_GT3_3_B;
            _renderCommandContext.UnkGT3_3_A = RenderCommandContext.DEFAULT_GT3_3_A;
        }

        // Utils-ish

        private string GetModelComponentPath(string file)
        {
            string componentPath = string.Empty;
            if (!string.IsNullOrEmpty(_configPath))
                componentPath = Path.Combine(_configDir, file);

            if (string.IsNullOrEmpty(componentPath) || !File.Exists(componentPath))
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
                _texturesPerLOD.Add(new List<string>());
            }
            else
            {
                texSetBuilder = _textureSetBuilders[_currentLOD];
            }

            return texSetBuilder;
        }

        private bool IsCallbackShape(LODConfig lodConfig, ModelMesh mesh)
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

        private void CalculateNewBoundings(Vector3[] modelMinMax, Vector3[] lodMinMax)
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

            return new Vector3[]
            {
                new(xmin, ymin, zmin),
                new(xmax, ymax, zmax)
            };
        }

        private static Vector3[] GetBoundaryBoxFromMinMax(Vector3 min, Vector3 max)
        {
            return new Vector3[]
            {       new Vector3(min.X, min.Y, min.Z),
                    new Vector3(min.X, min.Y, max.Z),
                    new Vector3(min.X, max.Y, min.Z),
                    new Vector3(min.X, max.Y, max.Z),
                    new Vector3(max.X, min.Y, min.Z),
                    new Vector3(max.X, min.Y, max.Z),
                    new Vector3(max.X, max.Y, min.Z),
                    new Vector3(max.X, max.Y, max.Z),
            };
        }
    }

    public enum ModelSetBuildVersion
    {
        ModelSet1,
        ModelSet2
    }
}
