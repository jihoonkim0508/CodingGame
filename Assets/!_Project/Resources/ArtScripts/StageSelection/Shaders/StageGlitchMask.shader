// StageTextGlitch's mask: a glitched model drawn flat in _GlitchMaskValue (coverage, which model / 8, its strength).
Shader "Hidden/CodingGame/Stage Glitch Mask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _GlitchMaskValue;
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag() : SV_Target { return _GlitchMaskValue; }
            ENDHLSL
        }
    }
}
