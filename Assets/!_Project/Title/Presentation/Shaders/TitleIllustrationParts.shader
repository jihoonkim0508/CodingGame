Shader "CodingGame/Title/Illustration Parts"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlagRect ("Flag UV bounds", Vector) = (0,0,0,0)
        _PinRight ("Pinned right edge", Float) = 0
        _WavePhase ("Wave phase", Float) = 0
        _WaveHeight ("Wave height in UV", Float) = .0048
        _CutRect ("Separate part UV bounds", Vector) = (0,0,0,0)
        _CutEnabled ("Cut separate part", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 position:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _FlagRect, _CutRect, _ClipRect;
            float _PinRight, _WavePhase, _WaveHeight, _CutEnabled;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o;
            }
            float inside(float2 uv, float4 bounds)
            {
                return step(bounds.x,uv.x)*step(bounds.y,uv.y)*step(uv.x,bounds.z)*step(uv.y,bounds.w);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float flag=inside(uv,_FlagRect)*step(.001,_FlagRect.z-_FlagRect.x);
                float2 local=(uv-_FlagRect.xy)/max(_FlagRect.zw-_FlagRect.xy,.0001);
                float freeEdge=saturate(lerp(local.x,1-local.x,_PinRight));
                float edgeFade=smoothstep(0,.07,local.y)*(1-smoothstep(.93,1,local.y));
                float wave=sin(_WavePhase-freeEdge*3.6+local.y*.8);
                uv.y+=flag*wave*_WaveHeight*pow(freeEdge,1.3)*edgeFade;
                fixed4 col=tex2D(_MainTex,uv)*i.color;
                col.rgb*=1+flag*.055*cos(_WavePhase-freeEdge*3.6)*freeEdge;
                col.a*=1-saturate(_CutEnabled)*inside(i.uv,_CutRect);
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.position.xy,_ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
