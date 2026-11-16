// The map's code error drawn like glitched text (시안_N2_글리치_버스트): a full-screen pass after the opaque objects, before
// the glass domes. It reads StageTextGlitch's mask and the camera's opaque texture. Outside a glitched model, cyan and
// magenta copies of its outline either side (white where they overlap). Inside an island's burst slice, the island shoved
// sideways, the gap it leaves showing the sky. On top, glitch bars. Everything else is left as it is.
Shader "Hidden/CodingGame/Stage Text Glitch"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-499" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_GlitchMask); SAMPLER(sampler_GlitchMask);
            // Viewport units (0 to 1, y up). Rects: each model's screen box (x min, y min, x max, y max). Slices: y from, y to,
            // shift in x, model. Bars: box; colours alongside.
            float4 _Rects[8], _Slices[32], _Bars[32], _BarColors[32];
            float4 _Sky;
            float _GhostX, _GhostAlpha, _SliceCount, _BarCount;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy * 2, .5, 1); // the quad is the whole screen, wherever it is
                return output;
            }
            float4 Mask(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_GlitchMask, sampler_GlitchMask, uv, 0); }
            bool On(float4 mask, float model) { return mask.r > .5 && abs(mask.g * 8 - model) < .5; }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                for (int b = 0; b < (int)_BarCount; b++)
                    if (all(uv >= _Bars[b].xy) && all(uv <= _Bars[b].zw)) return half4(_BarColors[b].rgb, 1);
                float4 here = Mask(uv);
                for (int s = 0; s < (int)_SliceCount; s++)
                {
                    float4 slice = _Slices[s]; float4 rect = _Rects[(int)slice.w];
                    if (uv.y < slice.x || uv.y >= slice.y || uv.x < rect.x || uv.x > rect.z) continue;
                    float2 from = uv - float2(slice.z, 0);
                    if (from.x >= rect.x && from.x <= rect.z && On(Mask(from), slice.w)) return half4(SampleSceneColor(from), 1);
                    if (On(here, slice.w)) return half4(_Sky.rgb, 1); // lifted out
                    break;
                }
                if (here.r > .5) return 0;
                float4 left = Mask(uv + float2(_GhostX, 0)), right = Mask(uv - float2(_GhostX, 0));
                float cyan = left.r > .5 ? left.b * _GhostAlpha : 0, magenta = right.r > .5 ? right.b * _GhostAlpha : 0;
                float white = cyan * magenta * .6 / max(_GhostAlpha * _GhostAlpha, .0001);
                // cyan over the scene, magenta over that, white over that, as one premultiplied colour
                float3 color = float3(0, 1, 1) * cyan * (1 - magenta) * (1 - white) + float3(1, 0, 1) * magenta * (1 - white) + white;
                return half4(color, 1 - (1 - cyan) * (1 - magenta) * (1 - white));
            }
            ENDHLSL
        }
    }
}
