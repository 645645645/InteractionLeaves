Shader "_URPForward/huanshu_PBR"
{
    Properties
    {
        [NoScaleOffset]_MainTex("Main Texture", 2D) = "white" {}
//        _NormalStrength("Normal Strength", Range(0,1)) = 0.5
        [Normal][NoScaleOffset]_NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset]_PBR("PBR metallic roughness envMask", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = 0.9
        [NoScaleOffset]_CubeMap("Irradiance Cubemap", CUBE) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "LightMode" = "UniversalForward"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                ReadMask 1
            }
            Name "PBR"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float4 vertex : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _NormalMap_ST;
                // half _NormalStrength;
                half _Smoothness;
            CBUFFER_END


            TEXTURE2D(_MainTex);
            TEXTURE2D(_NormalMap);
            TEXTURE2D(_PBR);
            // TEXTURE2D(_AO);
            TEXTURECUBE(_CubeMap);
            SAMPLER(sampler_CubeMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirWS = -half3(GetWorldSpaceViewDir(output.positionWS));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _NormalMap);
                output.vertex = output.positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target{
                float2 texUV = input.uv.xy;
                half4 albdo = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearRepeat, texUV);
                Light mainLight = GetMainLight();
                half3 lightColor = mainLight.color;
                half3 lightDirWS = mainLight.direction;
                half3 normalTangent = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearRepeat, texUV));
                // normalTangent.xy *= half(_NormalStrength);
                half3x3 TBN = half3x3(
                    input.tangentWS.xyz,
                    cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w,
                    input.normalWS
                );
                half4 pbr = SAMPLE_TEXTURE2D(_PBR, sampler_LinearRepeat, texUV);
                // half4 ao = SAMPLE_TEXTURE2D(_AO, sampler_LinearRepeat, texUV);
                half metallic = pbr.r;
                half roughness = max(pbr.g * (1 - _Smoothness), HALF_MIN);
                half envMask = pbr.b;
                half3 F0 = lerp(0.04, albdo.rgb, metallic);
                half3 diffuseColor = albdo.rgb * (1 - metallic);

                half3 wNormal = mul(normalTangent, TBN);
                half3 viewDirWS = normalize(input.viewDirWS);
                //顶点插值有误差但不多
                // half3 viewDirWS = -normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirWS = normalize(lightDirWS + viewDirWS);
                half3 reflectDirWS = normalize(reflect(viewDirWS, wNormal));
                // half3 reflectDirVS = mul((float3x3)unity_WorldToCamera, reflectDirWS);
                half3 reflectDirVS = TransformWorldToViewDir(reflectDirWS, true);

                half nl = saturate(dot(wNormal, lightDirWS));
                half nh = saturate(dot(wNormal, halfDirWS));
                half vh = saturate(dot(viewDirWS, halfDirWS));
                half nv = saturate(dot(wNormal, viewDirWS));

                // float f = F0 + (1 - F0) * pow(1 - vh, 5);
                // Spherical Gaussian近似来替代power，稍微提高了计算效率，并且差异微不可察
                half3 F = F0 + (1 - F0) * pow(2, (-5.55473 * vh - 6.98316) * vh);
                // float F = F0 + (1 - F0) * pow(2.0, (-5.5547299 * nh - 6.98316) * nh);
                half3 kd = (1 - F0) * diffuseColor;
                // half3 kd = (1 - F) * diffuseColor;

                half roughness2 = pow(roughness, 2);
                half roughness4 = pow(roughness2, 2);

                half D = roughness4 / pow((roughness4 * nh - nh) * nh + 1, 2);
                half L = ((1 - roughness2) * nv + roughness2) * nl;
                half V = ((1 - roughness2) * nv + roughness2) * nv;
                half G = clamp(0.5 / (L + V), 0, 1);
                half3 DFG = D * F * G;
                half3 dirLightResult = nl * (kd + DFG) * lightColor; /* * mainLight.intensity;*/

                float3 clipPos = input.vertex.xyz / input.vertex.w;
                clipPos.xyz = clipPos.xyz * 0.5 + 0.5;


                half3 Irradiance = SAMPLE_TEXTURECUBE_LOD(_CubeMap, sampler_CubeMap, wNormal, 5).xyz;
                half3 diffIBL = kd * Irradiance * envMask;


                /* UE4.27 BRDF.ush
                half2 EnvBRDFApproxLazarov(half Roughness, half NoV)
                {
                    // [ Lazarov 2013, "Getting More Physical in Call of Duty: Black Ops II" ]
                    // Adaptation to fit our G term.
                    const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
                    const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
                    half4 r = Roughness * c0 + c1;
                    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
                    half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
                    return AB;
                }
                */
                half4 bias = half4(1.0, 0.0425, 1.04, -0.04) - roughness.xxxx;
                half2 scale_bias = (min(pow(bias.x, 2), exp2(-9.2799997 * nv)) * bias.x + bias.y) * float2(-1.04, 1.04) + bias.zw;
                half3 envBRDF = F0 * scale_bias.x + scale_bias.y;

                

                half3 prefilteredColor = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDirWS, roughness * 5).xyz;
                

                half3 specularIBL = envBRDF * prefilteredColor * envMask;

                float3 envIBLResult = max(0, diffIBL + specularIBL);

                half3 color = min(10, (dirLightResult * 1 + envIBLResult));


                return half4(color.xyz, 1);
            }
            ENDHLSL
        }
    }
}