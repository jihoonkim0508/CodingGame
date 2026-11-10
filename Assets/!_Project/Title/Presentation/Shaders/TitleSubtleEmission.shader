Shader "CodingGame/Title/Subtle Emission"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Strength ("Additional light", Range(0,.2)) = .06
        _AccentMode ("0 Cyan / 1 Red", Float) = 0
        _ScanPosition ("Code row scan", Float) = 0
        _ScanStrength ("Code row highlight", Float) = 0
        _AreaA ("UV center and radius A", Vector) = (.155,.687,.05,.1)
        _AreaB ("UV center and radius B", Vector) = (.15,.85,.035,.04)
        _AreaC ("UV center and radius C", Vector) = (.23,.81,.016,.045)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 position:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect, _AreaA, _AreaB, _AreaC;
            float _Strength, _AccentMode, _ScanPosition, _ScanStrength;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }
            float area(float2 uv, float4 ellipse)
            {
                return 1 - smoothstep(.55, 1, length((uv - ellipse.xy) / max(ellipse.zw, .001)));
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                float cyan = saturate((min(source.g, source.b) - source.r - .08) * 4) * smoothstep(.25,.8,max(source.g,source.b));
                float red = saturate((source.r - max(source.g,source.b) - .1) * 4) * smoothstep(.3,.9,source.r);
                float mask = max(area(i.uv,_AreaA),max(area(i.uv,_AreaB),area(i.uv,_AreaC)));
                float scanY=lerp(_AreaA.y+.065,_AreaA.y-.065,saturate(_ScanPosition));
                float scanBand=1-smoothstep(.007,.018,abs(i.uv.y-scanY));
                float scan=scanBand*area(i.uv,_AreaA)*_ScanStrength*(1-saturate(_AccentMode));
                fixed4 result = fixed4(source.rgb * i.color.rgb, source.a * i.color.a * mask * lerp(cyan,red,saturate(_AccentMode)) * (_Strength+scan));
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.position.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - .001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
