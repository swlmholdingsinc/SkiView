Shader "Hidden/SkiView/CharacterCaptureDiag"
{
    Properties { _Mode ("0 normals 1 tangents 2 backface", Float) = 0 }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "CharacterCaptureDiag"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
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
            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                if (_Mode < 0.5)
                    return half4(normalize(input.normalWS) * 0.5 + 0.5, 1);
                if (_Mode < 1.5)
                    return half4(normalize(input.tangentWS) * 0.5 + 0.5, 1);
                return front ? half4(0.15, 0.75, 0.25, 1) : half4(0.85, 0.15, 0.12, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
