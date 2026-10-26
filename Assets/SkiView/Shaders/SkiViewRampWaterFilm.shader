// SKI VIEW ramp water film (pass 14): a thin sheet of water running down the waxed jump-ramp surface (and, with _EdgeFade,
// the foam/contact band where the ramp meets the lake). Two scrolling rivulet layers give a moving film thickness; its slope
// bends the sky reflection and the sun highlight, so the wet surface glints and visibly runs. Premultiplied: the film darkens
// the red a touch where it is thick and adds reflections on top.
Shader "SkiView/RampWaterFilm"
{
    Properties
    {
        _FilmTex ("Film (R thickness, G glints)", 2D) = "black" {}
        _Scroll ("Scroll A (xy), B (zw) per second", Vector) = (0.02, -0.55, -0.015, -0.9)
        _TileB ("Layer B tiling", Float) = 1.7
        _Opacity ("Film opacity", Float) = 0.35
        _Reflect ("Reflection", Float) = 1.0
        _SunSpec ("Sun highlight", Float) = 5.0
        _Bump ("Film slope strength", Float) = 1.6
        _EdgeFade ("Fade with uv1.y (waterline band)", Float) = 0
        _FoamCol ("Foam colour (waterline band)", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Film"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _FilmTex_ST; float4 _Scroll; float _TileB, _Opacity, _Reflect, _SunSpec, _Bump, _EdgeFade; float4 _FoamCol;
            CBUFFER_END
            TEXTURE2D(_FilmTex); SAMPLER(sampler_FilmTex);
            float _SVTime;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 t : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct V { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 ws : TEXCOORD2; float3 n : TEXCOORD3; float3 t : TEXCOORD4; float3 b : TEXCOORD5; float fog : TEXCOORD6; };
            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz); VertexNormalInputs nn = GetVertexNormalInputs(i.n, i.t);
                o.cs = p.positionCS; o.ws = p.positionWS; o.n = nn.normalWS; o.t = nn.tangentWS; o.b = nn.bitangentWS;
                o.uv = i.uv; o.uv1 = i.uv1; o.fog = ComputeFogFactor(o.cs.z); return o;
            }
            float H(float2 uv)
            {
                float2 a = uv * _FilmTex_ST.xy + _Scroll.xy * _SVTime, b = uv * _FilmTex_ST.xy * _TileB + _Scroll.zw * _SVTime;
                return SAMPLE_TEXTURE2D(_FilmTex, sampler_FilmTex, a).r * 0.65 + SAMPLE_TEXTURE2D(_FilmTex, sampler_FilmTex, b).r * 0.5;
            }
            half4 frag(V i) : SV_Target
            {
                float e = 1.0 / 256.0;
                float h0 = H(i.uv), hx = H(i.uv + float2(e, 0)), hy = H(i.uv + float2(0, e));
                float2 glu = i.uv * _FilmTex_ST.xy * 0.8 + _Scroll.zw * _SVTime * 1.1;
                half glint = SAMPLE_TEXTURE2D(_FilmTex, sampler_FilmTex, glu).g;
                float3 N = normalize(i.n + (normalize(i.t) * (h0 - hx) + normalize(i.b) * (h0 - hy)) * _Bump * 4.0);
                float3 Vd = normalize(_WorldSpaceCameraPos - i.ws);
                Light L = GetMainLight();
                float3 Hh = normalize(L.direction + Vd);
                half nv = saturate(dot(N, Vd));
                half fres = 0.02 + 0.98 * pow(1.0 - nv, 5.0);
                half3 refl = GlossyEnvironmentReflection(reflect(-Vd, N), 0.04h, 1.0h) * _Reflect;
                half thick = saturate(h0);
                half spec = pow(saturate(dot(N, Hh)), 380.0) * _SunSpec * (0.25 + glint * 0.9) * thick;
                half fade = lerp(1.0, pow(saturate(1.0 - i.uv1.y), 1.6), _EdgeFade);
                half a = saturate(_Opacity * thick * thick) * fade;   // thin water barely darkens; thicker rivulets read
                half3 c = (refl * fres * (0.15 + 0.85 * thick) + spec * L.color) * fade + _FoamCol.rgb * _FoamCol.a * thick * fade;
                a = saturate(a + _FoamCol.a * thick * fade * 0.8);
                c = MixFog(c, i.fog);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
