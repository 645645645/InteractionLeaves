Shader "_URPForward/WorldPlane"
{
    Properties
    {
        _MainColor("Main Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset]_MainTex ("Texture", 2D) = "white" {}
        [Normal][NoScaleOffset]_NormalMap("Normal Map", 2D) = "bump" {}
        _Tiling("Tiling", Range(0,10)) = 0.9
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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                half2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 posWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
            };

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);

            CBUFFER_START(UnityPerMaterial)
            half4 _MainColor;
            float _Tiling;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS.xyz);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = input.uv;
                output.normalWS = normalWS;
                output.posWS = posWS;
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                return output;
            }

            half4 frag(Varyings input, bool frontFacing : SV_IsFrontFace) : SV_Target
            {
                float2 worldUV = input.posWS.xz * 0.1f * _Tiling;
                half4 base = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearRepeat, worldUV);
                half3 normalTangent = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearRepeat, worldUV));
                half3x3 TBN = half3x3(
                    input.tangentWS.xyz,
                    cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w,
                    input.normalWS
                );
                half3 wNormal = mul(normalTangent, TBN);
                
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half3 lightDirWS = mainLight.direction;
                half3 normalWS = normalize(wNormal);
                normalWS *= frontFacing ? 1 : -1;
                half nl = dot(normalWS, lightDirWS);
                half halfLambert = mad(nl, 0.5, 0.5);
                half4 col = half4(lightColor * base.rgb * _MainColor.rgb * halfLambert.xxx, 1);
                // col.rgb = normalWS;
                return col;
            }
            ENDHLSL
        }
    }
}