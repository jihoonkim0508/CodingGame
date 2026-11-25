// A see-through shell that glows at its rim, with bright lines scrolling up it: the shield bubble and the reflector's
// reflect-phase shimmer. Alpha blended (added light vanished on the cream road and desk wood).
Shader "CodingGame/MonsterRim"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.4, 0.8, 1, 0.12)
        _RimColor ("Rim color", Color) = (0.7, 0.95, 1, 0.9)
        _RimPower ("Rim power", Float) = 2.5
        _Lines ("Lines per unit", Float) = 12
        _LineSpeed ("Line speed", Float) = 2
        _LineStrength ("Line strength", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _RimColor;
                float _RimPower, _Lines, _LineSpeed, _LineStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(1 - saturate(dot(normalize(input.normalWS), view)), _RimPower);
                float lines = _LineStrength * saturate(sin(input.positionWS.y * _Lines - _Time.y * _LineSpeed) * 4 - 3);
                half4 color = _BaseColor;
                color.rgb = lerp(color.rgb, _RimColor.rgb, saturate(rim + lines));
                color.a = saturate(color.a + rim * _RimColor.a + lines);
                return color;
            }
            ENDHLSL
        }
    }
}
