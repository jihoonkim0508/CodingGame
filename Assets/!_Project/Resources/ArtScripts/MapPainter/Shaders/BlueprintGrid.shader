Shader "CodingGame/Art/Blueprint Grid"
{
    Properties
    {
        _PaperColor("Blueprint Blue",Color)=(.035,.24,.58,1)
        _LineColor("Grid Color",Color)=(1,1,1,1)
        _GridSpacing("Grid Spacing",Float)=1
        _LineWidth("Line Width Pixels",Float)=1
        _MinorOpacity("Minor Lines",Range(0,1))=.18
        _MajorOpacity("Major Lines",Range(0,1))=.38
        _MajorEvery("Major Every",Float)=5
        _GridOrigin("Grid Origin",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Name "Blueprint"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _PaperColor,_LineColor;
                float4 _GridOrigin;
                float _GridSpacing,_LineWidth,_MinorOpacity,_MajorOpacity,_MajorEvery;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;};
            Varyings Vert(Attributes input)
            {
                Varyings output; VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS; output.positionWS=p.positionWS; return output;
            }
            float Grid(float2 coordinate)
            {
                float2 derivative=max(fwidth(coordinate),float2(.00001,.00001));
                float2 distance=abs(frac(coordinate+.5)-.5)/derivative;
                float coverage=1-smoothstep(max(0,_LineWidth*.5-.5),_LineWidth*.5+.5,min(distance.x,distance.y));
                // Suppress subpixel patterns when the camera pulls far away.
                return coverage*saturate(1-max(derivative.x,derivative.y));
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 coordinate=(input.positionWS.xz-_GridOrigin.xy)/max(.01,_GridSpacing);
                float minor=Grid(coordinate)*_MinorOpacity;
                float major=Grid(coordinate/max(1,_MajorEvery))*_MajorOpacity;
                return half4(lerp(_PaperColor.rgb,_LineColor.rgb,max(minor,major)),1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
