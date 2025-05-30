Shader "_URPForward/Leaves"
{
    Properties
    {
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
        _ZBias("ZOffset", Range(-50, 10)) = 0.1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            Cull Off
//            Offset [_ZBias], -10
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 normalOS : NORMAL;
                half2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _ZBias;
                half _ConcatNormalBasic;
            CBUFFER_END

            inline float2 UnitVectorToOctahedron(float3 N)
            {
                float2 Oct = N.xy;
                if (N.z < 0)
                {
                    Oct = (1 - abs(N.yx)) * float2(N.x >= 0 ? 1 : -1, N.y >= 0 ? 1 : -1);
                }
                return Oct;
            }

            inline float3 OctahedronToUnitVector(float2 Oct)
            {
                float3 N = float3(Oct, 1 - dot(1, abs(Oct)));
                if (N.z < 0)
                {
                    N.xy = (1 - abs(N.yx)) * (N.xy >= 0 ? float2(1, 1) : float2(-1, -1));
                }
                return normalize(N);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = OctahedronToUnitVector(input.normalOS.xy);
                float3 posWS = input.positionOS.xyz;
                // float3 posVS = TransformWorldToView(posWS);
                // posVS.z += _ViewZBasic;
                // output.positionCS = TransformWViewToHClip(posVS);
                float4 clipPos = TransformWorldToHClip(posWS);
                
                #if UNITY_REVERSED_Z
                    clipPos.z += _ZBias;
                #else
                    clipPos.z -= _ZBias;
                #endif
                
                output.positionCS = clipPos;
                output.uv = input.uv;
                output.normalWS = normalWS;
                return output;
            }

            half4 frag(Varyings input, bool frontFacing : SV_IsFrontFace) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);
                clip(base.a - 0.9);
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half3 lightDirWS = mainLight.direction;
                half3 normalWS = normalize(input.normalWS);
                normalWS *= frontFacing ? 1 : -1;
                half nl = dot(normalWS, lightDirWS);
                half halfLambert = mad(nl, 0.5, 0.5);
                half4 col = half4(lightColor * base.rgb * halfLambert.xxx, 1);
                // col.rgb = normalWS;
                return col;
            }
            ENDHLSL
        }
    }
}