using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PDTools.Files.Models.PS2.Commands;

namespace GTPS2ModelTool.Core
{
    public class RenderCommandContext
    {
        // These are all the defaults from ModelSet2::begin (GT4O US: 0x2F7060)
        // Called before render_

        // Alpha Test
        public const bool DEFAULT_ALPHA_TEST = true;
        public bool AlphaTest { get; set; } = DEFAULT_ALPHA_TEST;

        // Alpha Fail
        public const AlphaFailMethod DEFAULT_ALPHA_FAIL = AlphaFailMethod.KEEP;
        public AlphaFailMethod AlphaFail { get; set; } = DEFAULT_ALPHA_FAIL;

        // Destination Alpha Test
        public const bool DEFAULT_DESTINATION_ALPHA_TEST = false;
        public bool DestinationAlphaTest { get; set; } = DEFAULT_DESTINATION_ALPHA_TEST;

        // Destination Alpha Test Func
        public const DestinationAlphaFunction DEFAULT_DESTINATION_ALPHA_FUNC = DestinationAlphaFunction.EQUAL_ONE;
        public DestinationAlphaFunction DestinationAlphaFunc { get; set; } = DEFAULT_DESTINATION_ALPHA_FUNC;

        // Cull Mode
        public const bool DEFAULT_CULL_MODE = true;
        public bool CullMode { get; set; } = DEFAULT_CULL_MODE;

        // Blend func
        public const byte DEFAULT_BLENDFUNC_A = 0;
        public const byte DEFAULT_BLENDFUNC_B = 1;
        public const byte DEFAULT_BLENDFUNC_C = 0;
        public const byte DEFAULT_BLENDFUNC_D = 1;
        public const byte DEFAULT_BLENDFUNC_FIX = 1;
        public byte BlendFunc_A { get; set; } = DEFAULT_BLENDFUNC_A;
        public byte BlendFunc_B { get; set; } = DEFAULT_BLENDFUNC_B;
        public byte BlendFunc_C { get; set; } = DEFAULT_BLENDFUNC_C;
        public byte BlendFunc_D { get; set; } = DEFAULT_BLENDFUNC_D;
        public byte BlendFunc_FIX { get; set; } = DEFAULT_BLENDFUNC_FIX;

        // Depth Bias
        public const float DEFAULT_DEPTH_BIAS = 0.0f;
        public float DepthBias { get; set; } = DEFAULT_DEPTH_BIAS;

        // Depth Mask
        public const bool DEFAULT_DEPTH_MASK = true;
        public bool DepthMask { get; set; } = DEFAULT_DEPTH_MASK;

        // Depth Mask
        public const uint DEFAULT_COLOR_MASK = unchecked(0xFFFFFFFF);
        public uint ColorMask { get; set; } = DEFAULT_COLOR_MASK;

        // Alpha Test
        public const AlphaTestFunc DEFAULT_ALPHA_TEST_FUNC = AlphaTestFunc.GREATER;
        public const byte DEFAULT_ALPHA_TEST_REF = 0x20;
        public AlphaTestFunc AlphaTestFunc { get; set; } = DEFAULT_ALPHA_TEST_FUNC;
        public byte AlphaTestRef { get; set; } = DEFAULT_ALPHA_TEST_REF;

        // GT3
        public const float DEFAULT_GT3_3_R = 0;
        public const float DEFAULT_GT3_3_G = 0;
        public const float DEFAULT_GT3_3_B = 0;
        public const float DEFAULT_GT3_3_A = 0;

        public float UnkGT3_3_R { get; set; } = DEFAULT_GT3_3_R;
        public float UnkGT3_3_G { get; set; } = DEFAULT_GT3_3_G;
        public float UnkGT3_3_B { get; set; } = DEFAULT_GT3_3_B;
        public float UnkGT3_3_A { get; set; } = DEFAULT_GT3_3_A;

        public uint? FogColor { get; set; }

        public bool IsDefaultAlphaTest()
        {
            return AlphaTest == DEFAULT_ALPHA_TEST;
        }

        public bool IsDefaultAlphaFail()
        {
            return AlphaFail == DEFAULT_ALPHA_FAIL;
        }

        public bool IsdefaultDestinationAlphaTest()
        {
            return DestinationAlphaTest == DEFAULT_DESTINATION_ALPHA_TEST;
        }

        public bool IsDefaultDestinationAlphaFunc()
        {
            return DestinationAlphaFunc == DEFAULT_DESTINATION_ALPHA_FUNC;
        }

        public bool IsDefaultCullMode()
        {
            return CullMode == DEFAULT_CULL_MODE;
        }

        public bool IsDefaultAlphaTestFunc()
        {
            return AlphaTestFunc == DEFAULT_ALPHA_TEST_FUNC &&
                AlphaTestRef == DEFAULT_ALPHA_TEST_REF;
        }

        public bool IsDefaultDepthBias()
        {
            return DepthBias == DEFAULT_DEPTH_BIAS;
        }

        public bool IsDefaultDepthMask()
        {
            return DepthMask == DEFAULT_DEPTH_MASK;
        }

        public bool IsDefaultColorMask()
        {
            return ColorMask == DEFAULT_COLOR_MASK;
        }

        public bool IsDefaultBlendFunc()
        {
            return BlendFunc_A == DEFAULT_BLENDFUNC_A &&
                BlendFunc_B == DEFAULT_BLENDFUNC_B &&
                BlendFunc_C == DEFAULT_BLENDFUNC_C &&
                BlendFunc_D == DEFAULT_BLENDFUNC_D &&
                BlendFunc_FIX == DEFAULT_BLENDFUNC_FIX;
        }

        public bool IsDefaultFogColor()
        {
            return FogColor is null;
        }

        public bool IsDefaultGT3_3()
        {
            return UnkGT3_3_R == DEFAULT_GT3_3_R &&
                UnkGT3_3_G == UnkGT3_3_G &&
                UnkGT3_3_B == UnkGT3_3_B &&
                UnkGT3_3_A == UnkGT3_3_A &&
                BlendFunc_FIX == DEFAULT_BLENDFUNC_FIX;
        }
    }
}
