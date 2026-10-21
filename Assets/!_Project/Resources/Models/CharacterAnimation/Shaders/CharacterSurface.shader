Shader "CodingGame/Art/Character Surface"
{
 Properties
 {
  [MainTexture] _BaseMap("Original Albedo",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  _AccentColor("Accent Light",Color)=(.1,.65,1,1)
  _LightSoftness("Soft Lighting",Range(0,1))=.65
  _ShadowStrength("Cast Shadow Strength",Range(0,1))=.65
  _Smoothness("Surface Smoothness",Range(0,1))=.3
  _SpecularStrength("Soft Highlight",Range(0,1))=.14
  _RimStrength("Edge Light",Range(0,1))=.10
  _AccentGlow("Colored Surface Glow",Range(0,.5))=.035
  _TextureSaturation("Texture Saturation",Range(0,1))=.88
  _Dissolve("Dissolve (0 visible - 1 hidden)",Range(0,1))=0
  _DissolveScale("Dissolve Cell Scale",Range(1,40))=12
  _DissolveEdgeWidth("Dissolve Edge Width",Range(.001,.2))=.045
  [HDR] _DissolveEdgeColor("Dissolve Edge Color",Color)=(3,.28,.04,1)
  _BuildGlow("Build Glow",Range(0,1))=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   // All passes share this layout so skinned meshes also work with the SRP Batcher.
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor, _AccentColor;
    half4 _DissolveEdgeColor;
    float _LightSoftness, _ShadowStrength, _Smoothness, _SpecularStrength;
    float _RimStrength, _AccentGlow, _BuildGlow, _TextureSaturation;
    float _Dissolve, _DissolveScale, _DissolveEdgeWidth;
   CBUFFER_END
   struct Attributes
   {
    float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct Varyings
   {
    float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
    half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; float3 positionOS:TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
   };
   Varyings Vert(Attributes i)
   {
    Varyings o=(Varyings)0;
    UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
    o.positionCS=p.positionCS; o.positionWS=p.positionWS;
    o.normalWS=TransformObjectToWorldNormal(i.normalOS);
    o.positionOS=i.positionOS.xyz;
    o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z);
    return o;
   }
   float NoiseHash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   float DissolveField(float3 p)
   {
    p*=_DissolveScale;float3 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float lower=lerp(lerp(NoiseHash(cell),NoiseHash(cell+float3(1,0,0)),f.x),lerp(NoiseHash(cell+float3(0,1,0)),NoiseHash(cell+float3(1,1,0)),f.x),f.y);
    float upper=lerp(lerp(NoiseHash(cell+float3(0,0,1)),NoiseHash(cell+float3(1,0,1)),f.x),lerp(NoiseHash(cell+float3(0,1,1)),NoiseHash(cell+float3(1,1,1)),f.x),f.y);
    return .03+.94*lerp(lower,upper,f.z);
   }
   float DissolveClip(float3 p)
   {
    if(_Dissolve<=0)return 1;
    float remaining=DissolveField(p)-_Dissolve;clip(remaining);return remaining;
   }
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float remaining=DissolveClip(i.positionOS);
    half3 n=normalize(i.normalWS), view=GetWorldSpaceNormalizeViewDir(i.positionWS);
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    albedo=lerp(dot(albedo,half3(.2126,.7152,.0722)).xxx,albedo,_TextureSaturation);
    float4 shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
     shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
    #endif
    Light key=GetMainLight(shadowCoord);
    half diffuse=smoothstep(0,1,saturate((dot(n,key.direction)+_LightSoftness)/(1+_LightSoftness)));
    half3 ambient=max(SampleSH(n),half3(.16,.18,.22));
    half3 illumination=ambient*.4+lerp(half3(.35,.39,.47),half3(.92,.90,.86),diffuse)*key.color;
    illumination*=lerp(half3(.55,.64,.76),half3(1,1,1),lerp(1,key.shadowAttenuation,_ShadowStrength));
    half3 h=SafeNormalize(key.direction+view);
    half highlight=pow(saturate(dot(n,h)),lerp(12,72,_Smoothness))*_SpecularStrength*key.shadowAttenuation;
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS; inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    uint pixelLightCount=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
     Light light=GetAdditionalLight(lightIndex,i.positionWS);
     illumination+=light.color*light.distanceAttenuation*light.shadowAttenuation*saturate(dot(n,light.direction))*.65;
    LIGHT_LOOP_END
    half saturation=max(albedo.r,max(albedo.g,albedo.b))-min(albedo.r,min(albedo.g,albedo.b));
    half rim=pow(1-saturate(dot(n,view)),3)*_RimStrength;
    half3 color=albedo*illumination+highlight*key.color+rim*lerp(half3(.6,.7,.85),_AccentColor.rgb,.3);
    color+=albedo*smoothstep(.08,.35,saturation)*_AccentGlow;
    color+=_DissolveEdgeColor.rgb*(1-smoothstep(0,_DissolveEdgeWidth,remaining))*step(.0001,_Dissolve);
    color=lerp(color,half3(.65,.95,1),saturate(_BuildGlow));
    return half4(MixFog(color,i.fog),1);
   }
   float3 _LightDirection, _LightPosition;
   Varyings ShadowVert(Attributes i)
   {
    Varyings o=Vert(i); float3 lightDirection=_LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     lightDirection=normalize(_LightPosition-o.positionWS);
    #endif
    o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,normalize(o.normalWS),lightDirection));
    #if UNITY_REVERSED_Z
     o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #else
     o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return o;
   }
   half4 DepthFrag(Varyings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);DissolveClip(i.positionOS);return 0;}
   half4 NormalFrag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);DissolveClip(i.positionOS);
    half3 n=normalize(i.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
     float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
    #else
     return half4(n,0);
    #endif
   }
  ENDHLSL
  Pass
  {
   Name "CharacterForward" Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fog
   ENDHLSL
  }
  Pass
  {
   Name "CharacterShadow" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   ENDHLSL
  }
  Pass
  {
   Name "CharacterDepth" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   ENDHLSL
  }
  Pass
  {
   Name "CharacterNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment NormalFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   ENDHLSL
  }
 }
 FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
