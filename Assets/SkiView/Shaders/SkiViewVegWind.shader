// Pass 19: cheap foliage shader for Christopher's vegetation pack. Main light (+ shadows) + SH ambient + fog, alpha clip, two-sided
// leaves with the normal flipped on back faces. Wind from the pack's vertex colours (README "Wind"): R = sway weight, G = leaf flutter,
// B = phase. sway = windDir * strength * R * sin(t*1.1 + B*2pi + worldX*0.05); flutter = normal * flutterStrength * G * sin(t*7 + B*2pi + worldY*0.7).
// Global _SVWind (xy = dir, z = sway metres at R=1, w = flutter metres) is set by LakeDetail; the phone tier sets SV_WIND_OFF.
Shader "SkiView/VegWind"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha clip", Range(0,1)) = 0.5
        _WindScale ("Wind scale (0 = rigid)", Range(0,2)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half _Cutoff; half _WindScale;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float4 _SVWind;   // global
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        float3 Wind(float3 posWS, float3 nWS, half4 c)
        {
        #if defined(SV_WIND_OFF)
            return posWS;
        #else
            float t = _Time.y, ph = c.b * 6.2831853;
            float3 dir = float3(_SVWind.x, 0, _SVWind.y);
            float sway = _SVWind.z * _WindScale * c.r * sin(t * 1.1 + ph + posWS.x * 0.05);
            float fl = _SVWind.w * _WindScale * c.g * sin(t * 7.0 + ph + posWS.y * 0.7);
            return posWS + dir * sway + nWS * fl;
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            AlphaToMask On   // T2: with _SV_A2C = 0 the output alpha is 1 (hard clip as before); 1 = coverage from a screen-space sharpened alpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ SV_WIND_OFF
            #pragma multi_compile _ LOD_FADE_CROSSFADE   // T2: dithered LOD cross-fade (only prefabs with fadeMode CrossFade use it)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            float _SV_A2C;   // global, set per lake by LakeDetail (T2: Tall Pine, Heron Marsh)
            half4 _SV_FoliageRim;   // global, lake P1 (LakeDetail.foliageRim)
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 nWS : TEXCOORD1; float3 posWS : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(Attributes i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                float3 p = Wind(TransformObjectToWorld(i.positionOS.xyz), nWS, i.color);
                o.posWS = p; o.nWS = nWS; o.pos = TransformWorldToHClip(p); o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.pos);
                #endif
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half cov = 1;
                if (_SV_A2C > 0.5) { cov = saturate((c.a - _Cutoff) / max(fwidth(c.a), 1e-4) + 0.5); clip(cov - 0.004); }
                else clip(c.a - _Cutoff);
                float3 n = normalize(i.nWS) * (front ? 1 : -1);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                half ndl = saturate(dot(n, L.direction)) * 0.8 + 0.2 * saturate(dot(-n, L.direction));   // a little light through the leaves
                half3 col = c.rgb * (SampleSH(n) + L.color * ndl * L.shadowAttenuation);
                // lake P1: warm fresnel rim on the sun-facing, sunlit side (no extra lights; a few ALU)
                half fr = 1 - saturate(dot(n, normalize(GetWorldSpaceViewDir(i.posWS)))); fr *= fr * fr;
                col += c.rgb * _SV_FoliageRim.rgb * (_SV_FoliageRim.a * 2 * fr * saturate(dot(n, L.direction)) * L.shadowAttenuation);
                return half4(MixFog(col, i.fog), cov);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SV_WIND_OFF
            #pragma multi_compile _ LOD_FADE_CROSSFADE   // T2: dithered LOD cross-fade (only prefabs with fadeMode CrossFade use it)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            float3 _LightDirection;
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(Attributes i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                float3 p = Wind(TransformObjectToWorld(i.positionOS.xyz), nWS, i.color);
                float4 h = TransformWorldToHClip(ApplyShadowBias(p, nWS, _LightDirection));
            #if UNITY_REVERSED_Z
                h.z = min(h.z, UNITY_NEAR_CLIP_VALUE);
            #else
                h.z = max(h.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.pos = h; o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o;
            }
            half4 frag(V i) : SV_Target {
            #ifdef LOD_FADE_CROSSFADE
            LODFadeCrossFade(i.pos);
            #endif
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SV_WIND_OFF
            #pragma multi_compile _ LOD_FADE_CROSSFADE   // T2: dithered LOD cross-fade (only prefabs with fadeMode CrossFade use it)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(Attributes i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                o.pos = TransformWorldToHClip(Wind(TransformObjectToWorld(i.positionOS.xyz), nWS, i.color)); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o;
            }
            half4 frag(V i) : SV_Target {
            #ifdef LOD_FADE_CROSSFADE
            LODFadeCrossFade(i.pos);
            #endif
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff); return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
