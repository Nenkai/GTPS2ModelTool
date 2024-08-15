using PDTools.Files.Models.PS2.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace GTPS2ModelTool.Core;

public class RenderCommandParser
{
    public static List<ModelSetupPS2Command> ParseCommands(List<string> commandStrings)
    {
        List<ModelSetupPS2Command> renderCommands = [];

        foreach (var beforeCmd in commandStrings)
        {
            string[] spl = beforeCmd.Split("(");
            string commandName = spl[0];
            string[] args = null;

            if (!Table.TryGetValue(commandName, out RenderCommandDescription desc))
                throw new InvalidOperationException("Invalid command name");

            RenderCommandArgument[] cmdArgs = new RenderCommandArgument[desc.ArgumentTypes.Length];
            if (desc.ArgumentTypes.Length > 0)
            {
                if (spl.Length <= 1)
                    throw new InvalidOperationException("Expected args");

                args = spl[1].TrimEnd(')')
                        .Split(",")
                        .Select(e => e.Trim())
                        .ToArray();

                if (args.Length != desc.ArgumentTypes.Length)
                    throw new InvalidOperationException("Mismatched amount of arguments provided");

                for (int i = 0; i < desc.ArgumentTypes.Length; i++)
                {
                    var arg = new RenderCommandArgument();
                    ArgType type = desc.ArgumentTypes[i];
                    arg.Type = type;

                    switch (type)
                    {
                        case ArgType.Byte:
                            {
                                if (!byte.TryParse(args[i], out byte value))
                                {
                                    if (!args[i].StartsWith("0x") || !byte.TryParse(args[i].AsSpan(2), NumberStyles.AllowHexSpecifier, null, out value))
                                        throw new RenderCommandParseException("Could not parse byte");
                                }
                                arg.Value = value;
                            }
                            break;
                        case ArgType.Int:
                            {
                                if (!int.TryParse(args[i], out int value))
                                {
                                    if (!args[i].StartsWith("0x") || !int.TryParse(args[i].AsSpan(2), NumberStyles.AllowHexSpecifier, null, out value))
                                        throw new RenderCommandParseException("Could not parse int");
                                }

                                arg.Value = value;
                            }
                            break;
                        case ArgType.UInt:
                            {
                                if (!uint.TryParse(args[i], out uint value))
                                {
                                    if (!args[i].StartsWith("0x") || !uint.TryParse(args[i].AsSpan(2), NumberStyles.AllowHexSpecifier, null, out value))
                                        throw new RenderCommandParseException("Could not parse uint");
                                }

                                arg.Value = value;
                            }
                            break;
                        case ArgType.Float:
                            {
                                if (!float.TryParse(args[i], out float value))
                                    throw new RenderCommandParseException("Could not parse float");
                                arg.Value = value;
                            }
                            break;
                        case ArgType.String:
                            {
                                arg.Value = args[i];
                            }
                            break;
                    }

                    cmdArgs[i] = arg;
                }
            }

            ModelSetupPS2Command cmd;
            if (desc.ParseCallback is not null)
                cmd = desc.ParseCallback(desc, cmdArgs);
            else
                cmd = ModelSetupPS2Command.GetByOpcode(desc.Opcode);

            renderCommands.Add(cmd);
        }

        return renderCommands;
    }

    public static readonly Dictionary<string, RenderCommandDescription> Table = new()
    {
        // Default is GREATER, 0x20
        { "AlphaFunction", new(ModelSetupPS2Opcode.pglAlphaFunc, [
                ArgType.String, ArgType.Byte
            ], ParseAlphaFunction)
        },

        { "AlphaFail", new(ModelSetupPS2Opcode.pglAlphaFail, [
                ArgType.String
            ], ParseAlphaFail)
        },

        { "BlendFunction", new(ModelSetupPS2Opcode.pglBlendFunc, [
                ArgType.Byte, ArgType.Byte, ArgType.Byte, ArgType.Byte, ArgType.Byte
            ], ParseBlendFunction)
        },

        { "ColorMask", new(ModelSetupPS2Opcode.pglColorMask, [
                ArgType.UInt
            ], ParseColorMask)
        },


        /* Enabled by default
        { "EnableCullFace", new(ModelSetupPS2Opcode.pglEnableCullFace, new ArgType[] {
                
            }, null)
        },
        */

        { "DisableAlphaTest", new(ModelSetupPS2Opcode.pglDisableAlphaTest, [], null)
        },

        { "DisableCullFace", new(ModelSetupPS2Opcode.pglDisableCullFace, [], null)
        },

        { "DisableDepthMask", new(ModelSetupPS2Opcode.pglDisableDepthMask, [], null)
        },

        /* Enabled by default
        { "EnableDepthMask", new(ModelSetupPS2Opcode.pglEnableDepthMask, new ArgType[] {

            }, null)
        },
        */
        { "DestinationAlphaFunc", new(ModelSetupPS2Opcode.pglSetDestinationAlphaFunc, [
                ArgType.String
            ], ParseDestinationAlphaFunction)
        },

        { "EnableDestinationAlphaTest", new(ModelSetupPS2Opcode.pglEnableDestinationAlphaTest, [], null)
        },



        { "FogColor", new(ModelSetupPS2Opcode.pglSetFogColor, [
                ArgType.Byte, ArgType.Byte, ArgType.Byte
            ], ParseFogColor)
        },

        { "DepthBias", new(ModelSetupPS2Opcode.pglDepthBias, [
                ArgType.Float
            ], ParseDepthBias)
        },

        /* Disabled by default
        { "DisableDestinationAlphaTest", new(ModelSetupPS2Opcode.pglDisableDestinationAlphaTest, new ArgType[] {

            }, null)
        },
        */

        { "PushMatrix", new(ModelSetupPS2Opcode.pglPushMatrix, [], null)
        },

        { "PopMatrix", new(ModelSetupPS2Opcode.pglPopMatrix, [], null)
        },

        { "MatrixMode", new(ModelSetupPS2Opcode.pglMatrixMode, [
                ArgType.String
            ], ParseMatrixMode)
        },

        { "Rotate", new(ModelSetupPS2Opcode.pglRotate, [
                ArgType.Float, ArgType.Float, ArgType.Float, ArgType.Float,
            ], ParseRotate)
        },

        { "Translate", new(ModelSetupPS2Opcode.pglTranslate, [
                ArgType.Float, ArgType.Float, ArgType.Float,
            ], ParseTranslate)
        },

        { "Scale", new(ModelSetupPS2Opcode.pglScale, [
                ArgType.Float, ArgType.Float, ArgType.Float,
            ], ParseScale)
        },

        { "UnkGT3_2_1ui", new(ModelSetupPS2Opcode.pglGT3_2_1ui, [
                ArgType.Float,
            ], ParseUnkGT3_2_1ui)
        },

        { "UnkGT3_2_4f", new(ModelSetupPS2Opcode.pglGT3_2_4f, [
                ArgType.Float, ArgType.Float, ArgType.Float, ArgType.Float
            ], ParseUnkGT3_2_4f)
        },
    };

    private static Cmd_pglAlphaFunc ParseAlphaFunction(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        AlphaTestFunc tst = args[0].GetEnum<AlphaTestFunc>();
        byte @ref = args[1].GetByte();
        return new Cmd_pglAlphaFunc(tst, @ref);
    }

    private static Cmd_pglBlendFunc ParseBlendFunction(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        byte a = args[0].GetByte();
        byte b = args[1].GetByte();
        byte c = args[2].GetByte();
        byte d = args[3].GetByte();
        byte fix = args[4].GetByte();

        return new Cmd_pglBlendFunc(a, b, c, d, fix);
    }

    private static Cmd_pglSetDestinationAlphaFunc ParseDestinationAlphaFunction(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        DestinationAlphaFunction tst = args[0].GetEnum<DestinationAlphaFunction>();
        return new Cmd_pglSetDestinationAlphaFunc(tst);
    }

    private static Cmd_pglMatrixMode ParseMatrixMode(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        MatrixMode mode = args[0].GetEnum<MatrixMode>();
        return new Cmd_pglMatrixMode(mode);
    }

    private static Cmd_pglAlphaFail ParseAlphaFail(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        AlphaFailMethod method = args[0].GetEnum<AlphaFailMethod>();
        return new Cmd_pglAlphaFail(method);
    }

    private static Cmd_pglRotate ParseRotate(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float angle = args[0].GetFloat();
        float x = args[1].GetFloat();
        float y = args[2].GetFloat();
        float z = args[3].GetFloat();

        return new Cmd_pglRotate(angle, x, y, z);
    }

    private static Cmd_pglTranslate ParseTranslate(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float x = args[0].GetFloat();
        float y = args[1].GetFloat();
        float z = args[2].GetFloat();

        return new Cmd_pglTranslate(x, y, z);
    }

    private static Cmd_pglScale ParseScale(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float x = args[0].GetFloat();
        float y = args[1].GetFloat();
        float z = args[2].GetFloat();

        return new Cmd_pglScale(x, y, z);
    }

    private static Cmd_pglSetFogColor ParseFogColor(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        byte r = args[0].GetByte();
        byte g = args[0].GetByte();
        byte b = args[0].GetByte();

        return new Cmd_pglSetFogColor((uint)(r | g << 8 | b << 16));
    }

    private static Cmd_pglDepthBias ParseDepthBias(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float bias = args[0].GetFloat();

        return new Cmd_pglDepthBias(bias);
    }

    private static Cmd_pglColorMask ParseColorMask(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        uint mask = args[0].GetUInt();

        return new Cmd_pglColorMask(mask);
    }

    private static Cmd_GT3_2_1ui ParseUnkGT3_2_1ui(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float color = args[0].GetFloat();

        return new Cmd_GT3_2_1ui(color);
    }

    private static Cmd_GT3_2_4f ParseUnkGT3_2_4f(RenderCommandDescription desc, RenderCommandArgument[] args)
    {
        float r = args[0].GetFloat();
        float g = args[1].GetFloat();
        float b = args[2].GetFloat();
        float a = args[3].GetFloat();

        return new Cmd_GT3_2_4f(r, g, b, a);
    }
}

public class RenderCommandDescription
{
    public ModelSetupPS2Opcode Opcode { get; set; }
    public ArgType[] ArgumentTypes { get; set; }
    public Func<RenderCommandDescription, RenderCommandArgument[], ModelSetupPS2Command> ParseCallback { get; set; }
    public Func<RenderCommandDescription, RenderCommandArgument[], ModelSetupPS2Command> WriteCallback { get; set; }

    public RenderCommandDescription(ModelSetupPS2Opcode opcode, ArgType[] args, Func<RenderCommandDescription, RenderCommandArgument[], ModelSetupPS2Command> parseCallback)
    {
        Opcode = opcode;
        ArgumentTypes = args;
        ParseCallback = parseCallback;
    }
}

public class RenderCommandArgument
{
    public object Value { get; set; }
    public ArgType Type { get; set; }

    public byte GetByte()
    {
        return (byte)Value;
    }

    public float GetFloat()
    {
        return (float)Value;
    }

    public string GetString()
    {
        return (string)Value;
    }

    public int GetInt()
    {
        return (int)Value;
    }

    public uint GetUInt()
    {
        return (uint)Value;
    }

    public T GetEnum<T>() where T : unmanaged, Enum
    {
        return Enum.Parse<T>((string)Value);
    }
}

public class RenderCommandParseException : Exception
{
    public RenderCommandParseException(string message)
        : base(message)
    {

    }
}

public enum ArgType
{
    Byte,
    Int,
    UInt,
    Float,
    String
}
