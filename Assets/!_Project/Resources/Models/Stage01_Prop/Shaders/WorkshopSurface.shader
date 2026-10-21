Shader "CodingGame/Art/Workshop Surface"
{
 Properties
 {
  [MainColor] _BaseColor("Color",Color)=(.8,.65,.4,1)
  _GrainStrength("Wood Grain",Range(0,1))=0
  _LightSoftness("Softness",Range(0,1))=.7
  _ShadowStrength("Shadow",Range(0,1))=.65
  _GradientStrength("Body Shade",Range(0,1))=.16
  _BevelHighlight("Edge Light",Range(0,1))=.12
  _MapTopY("Top",Float)=0
  _MapHeight("Height",Float)=1
  _BuildGlow("Build Glow",Float)=0
  [HideInInspector] _BaseMap("Base",2D)="white"{}
  [HideInInspector] _BumpMap("Normal",2D)="bump"{}
  [HideInInspector] _Cutoff("Cutoff",Float)=.5
  [HideInInspector] _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  HLSLINCLUDE







   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   float _GrainStrength,_LightSoftness,_ShadowStrength,_GradientStrength,_BevelHighlight,_MapTopY,_MapHeight,_BuildGlow;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;};
   V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(V i):SV_Target
   {
    half3 n=normalize(i.n);float4 sc=TransformWorldToShadowCoord(i.w);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
     sc=ComputeScreenPos(TransformWorldToHClip(i.w));
    #endif
    Light l=GetMainLight(sc);
    half d=saturate((dot(n,l.direction)+_LightSoftness)/(1+_LightSoftness));
    half3 illum=lerp(half3(.57,.57,.61),half3(1.16,1.10,1.01),smoothstep(0,1,d));
    illum*=lerp(half3(.64,.72,.82),half3(1,1,1),lerp(1,l.shadowAttenuation,_ShadowStrength));
    illum*=lerp(half3(1,1,1),l.color,.25);
    InputData inputData=(InputData)0;inputData.positionWS=i.w;inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
    uint pixelLightCount=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
     Light e=GetAdditionalLight(lightIndex,i.w);illum+=e.color*e.distanceAttenuation*saturate(dot(n,e.direction))*.25;
    LIGHT_LOOP_END
    float wav=sin(i.w.x*.7)*.3+sin(i.w.x*2.1+i.w.z*.4)*.07;
    float grain=sin(i.w.z*35+wav*9)*.025+sin(i.w.z*6+wav)*.018;
    half3 c=_BaseColor.rgb*(1+grain*_GrainStrength)*illum;
    half edge=saturate(n.y)*saturate(1-n.y)*4;
    c+=_BaseColor.rgb*edge*_BevelHighlight;
    c*=1-_GradientStrength*saturate((_MapTopY-i.w.y)/max(.01,_MapHeight))*(1-saturate(n.y));
    c=lerp(c,half3(.65,.95,1),saturate(_BuildGlow));return half4(MixFog(c,i.fog),1);
   }
   float3 _LightDirection;
   float3 _LightPosition;
   V ShadowVert(A i)
   {
    V o=Vert(i);float3 direction=_LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     direction=normalize(_LightPosition-o.w);
    #endif
    o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),direction));
    #if UNITY_REVERSED_Z
     o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE);
    #else
     o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return o;
   }
   half4 DepthFrag(V i):SV_Target{return 0;}
   half4 NormalFrag(V i):SV_Target{return half4(normalize(i.n),0);}
  ENDHLSL
  Pass
  {
   Name "WorkshopForward"
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fog
   ENDHLSL
  }
  Pass
  {
   Name "WorkshopShadow"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   ENDHLSL
  }
  Pass
  {
   Name "WorkshopDepth"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   ENDHLSL
  }
  Pass
  {
   Name "WorkshopNormals"
   Tags {"LightMode"="DepthNormalsOnly"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment NormalFrag
   ENDHLSL
  }
 }
}
