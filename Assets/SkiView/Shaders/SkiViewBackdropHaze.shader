// Pass 16: distant foothill / mountain backdrop layers. Unlit + simple sun shading, then blended to the haze colour by the
// per-vertex haze (vertex colour alpha). Ignores scene fog on purpose: exp2 fog would erase every layer past ~1.5 km, while
// layered ridges that each step closer to the haze colour are what gives atmospheric depth.
Shader "SkiView/BackdropHaze"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Haze colour", Color) = (0.74, 0.77, 0.81, 1)
        _SunDir ("Sun direction (world, to the sun)", Vector) = (0, 1, 0, 0)
        _Light ("Light level", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+20" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half4 _HazeColor; float4 _SunDir; half _Light;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; half shade : TEXCOORD1; };
            V vert(A i)
            {
                V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.col = i.col;
                float3 n = TransformObjectToWorldNormal(i.n);
                o.shade = 0.70 + 0.30 * saturate(dot(n, normalize(_SunDir.xyz)) * 0.5 + 0.5);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb * i.col.rgb * i.shade * _Light;
                return half4(lerp(c, _HazeColor.rgb, saturate(i.col.a)), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            float4 dv(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half df() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
