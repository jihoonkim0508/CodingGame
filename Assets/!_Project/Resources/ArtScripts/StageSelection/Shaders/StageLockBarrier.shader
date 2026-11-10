Shader "CodingGame/Stage Lock Barrier"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,.64,.19,1)
        _Pulse ("Pulse", Float) = 1
        _Reject ("Rejected click", Float) = 0
        _Release ("Unlock dissolve", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Pulse, _Reject, _Release;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv = input.uv;
                // The barrier dissolves upward as the circuit reaches the lock.
                clip(uv.y - _Release * 1.1 + .01);
                float scan = pow(saturate(1 - abs(uv.y - frac(_Time.y * .16)) * 16), 3);
                float uprights = pow(saturate(1 - abs(frac(uv.x * 8) - .5) * 2), 32);
                float stripeWave=sin((uv.x*50-uv.y*6-_Time.y*.12)*6.283185);
                float stripeWidth=max(fwidth(stripeWave),.04);
                float stripes = smoothstep(-stripeWidth,stripeWidth,stripeWave);
                float cautionBand = 1 - smoothstep(.025, .055, abs(uv.y - .15));
                float alpha = .025 + scan * .11 + uprights * .2 + stripes * cautionBand * .35;
                // Soften the transparent wall's silhouette independently from its visible rim.
                alpha *= 1-smoothstep(.92,1,uv.y);
                alpha *= _Pulse + _Reject * 1.6;
                float openingEdge = (1 - smoothstep(.01,.08,abs(uv.y-_Release*1.1))) * step(.01,_Release);
                half3 color = lerp(_BaseColor.rgb, half3(1,.45,.18), _Reject);
                color = lerp(color,half3(1,.82,.48),saturate(_Release*3));
                return half4(color + openingEdge*.7, saturate((alpha+openingEdge*.6)*(1-_Release)));
            }
            ENDHLSL
        }
    }
}
