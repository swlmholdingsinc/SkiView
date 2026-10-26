// SKI VIEW spray particle (pass 14): soft, translucent water spray. Texture alpha x vertex colour, lit by the sky (SH) and
// the sun, with strong forward scattering when the spray is between the camera and the sun (backlit spray glows), and
// soft-particle fading against the scene depth so sheets and mist never show a hard edge where they meet the water.
Shader "SkiView/SprayParticle"
{
    Properties
    {
        _BaseMap ("Texture (A = density)", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.93, 0.96, 1, 1)
        _Ambient ("Sky light", Float) = 1.0
        _Sun ("Sun light", Float) = 0.45
        _Scatter ("Forward scatter (backlit)", Float) = 2.2
        _ScatterPow ("Scatter sharpness", Float) = 6
        _SoftDist ("Soft particle distance (m)", Float) = 0.35
        _AlphaMul ("Alpha", Float) = 1
        _WaterFade ("Fade near the water surface (m)", Float) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "Spray"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _Tint; float _Ambient, _Sun, _Scatter, _ScatterPow, _SoftDist, _AlphaMul, _WaterFade;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 cs : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; float3 ws : TEXCOORD1; float4 sp : TEXCOORD2; float fog : TEXCOORD3; };
            V vert(A i)
            {
                V o; o.ws = TransformObjectToWorld(i.pos.xyz); o.cs = TransformWorldToHClip(o.ws);
                o.col = i.col; o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.sp = ComputeScreenPos(o.cs); o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = t.a * i.col.a * _AlphaMul;
                // soft particles
                float2 suv = i.sp.xy / max(i.sp.w, 1e-5);
                float raw = SampleSceneDepth(suv);
                float sceneZ = LinearEyeDepth(raw, _ZBufferParams), partZ = i.sp.w;
                a *= saturate((sceneZ - partZ) / max(_SoftDist, 1e-3));
                a *= saturate(i.ws.y / max(_WaterFade, 1e-3));   // the lake surface is not in the depth texture: fade into it instead of a hard cut
                Light L = GetMainLight();
                float3 v = normalize(i.ws - _WorldSpaceCameraPos);
                half fwd = pow(saturate(dot(v, L.direction)), _ScatterPow);
                half3 sky = max(SampleSH(half3(0, 1, 0)), 0.05);
                half3 c = _Tint.rgb * t.rgb * i.col.rgb * (sky * _Ambient + L.color * (_Sun + _Scatter * fwd));
                c = MixFog(c, i.fog);
                return half4(c, saturate(a));
            }
            ENDHLSL
        }
    }
}
