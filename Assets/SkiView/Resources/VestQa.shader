Shader "Hidden/SkiView/VestQa"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.55, 0.52, 1)
        _Mode ("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "VestQaForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Mode;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 tangentWS : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                if (_Mode < 0.5) return half4(_Color.rgb, 1);
                if (_Mode < 1.5)
                {
                    half3 n = normalize(input.normalWS);
                    return half4(n * 0.5 + 0.5, 1);
                }
                half3 t = normalize(input.tangentWS);
                return half4(t * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
