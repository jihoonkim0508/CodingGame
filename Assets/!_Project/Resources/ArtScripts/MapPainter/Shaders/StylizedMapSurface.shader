Shader "CodingGame/Art/Stylized Map Surface"
{
    Properties
    {
        [MainColor] _BaseColor("Color",Color)=(.68,.72,.78,1)
        _LightSoftness("Light Softness",Range(0,1))=.65
        _ShadowStrength("Shadow Strength",Range(0,1))=.35
        _GradientStrength("Body Gradient",Range(0,1))=.18
        _BevelHighlight("Bevel Highlight",Range(0,1))=.12
        _ShadowTint("Shadow Tint",Color)=(.62,.69,.82,1)
        _MapTopY("Map Top",Float)=0
        _MapHeight("Map Height",Float)=1
        [HideInInspector] _BuildGlow("Build Glow",Float)=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _BaseMap("Base",2D)="white"{}
        [HideInInspector] _BumpMap("Normal",2D)="bump"{}
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass
        {
            Name "StylizedForward"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _ShadowTint;
                float _LightSoftness,_ShadowStrength,_GradientStrength,_BevelHighlight,_MapTopY,_MapHeight,_BuildGlow;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=position.positionCS; output.positionWS=position.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS); output.fog=ComputeFogFactor(position.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal=normalize(input.normalWS);
                float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                Light main=GetMainLight(shadowCoord);
                half wrapped=saturate((dot(normal,main.direction)+_LightSoftness)/(1+_LightSoftness));
                half diffuse=lerp(.72,1.08,smoothstep(0,1,wrapped));
                half3 illumination=diffuse*lerp(half3(1,1,1),main.color,.3);
                illumination*=lerp(_ShadowTint.rgb,half3(1,1,1),lerp(1,main.shadowAttenuation,_ShadowStrength));
                InputData inputData=(InputData)0; inputData.positionWS=input.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                uint pixelLightCount=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light extra=GetAdditionalLight(lightIndex,input.positionWS);
                    illumination+=extra.color*extra.distanceAttenuation*saturate(dot(normal,extra.direction))*.3;
                LIGHT_LOOP_END
                float depth=saturate((_MapTopY-input.positionWS.y)/max(.001,_MapHeight));
                half body=1-_GradientStrength*depth*(1-saturate(normal.y));
                half bevel=saturate(normal.y)*saturate(1-normal.y)*4;
                half3 color=_BaseColor.rgb*illumination*body+_BaseColor.rgb*bevel*_BevelHighlight;
                color=lerp(color,half3(.65,.95,1),saturate(_BuildGlow));
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
