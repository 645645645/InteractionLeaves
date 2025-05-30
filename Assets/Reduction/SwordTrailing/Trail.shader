Shader "_URPForward/Trail"
{
    Properties
    {
        [HDR]_MainColor("MainColor(A:ClipThreshold)", Color) = (0.2, 0.6, 1, 0.25)
        [NoScaleOffset]_MainTex("Mask", 2D) = "white" {}
        [NoScaleOffset]_NoiseTex("Noise", 2D) = "white" {}

        [Header(Trail Desturb) ]
        _DesturbParams("整体 时间 噪声 移动", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+900"
        }
        LOD 100

        Pass
        {
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Zero
                ReadMask 1
                WriteMask 0
            }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL; //world运动方向
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : NORMAL;
                float4 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _DesturbParams;
            CBUFFER_END

            #define _OverFactor _DesturbParams.x
            #define _TimeFactor _DesturbParams.y
            #define _NoiseFactor _DesturbParams.z
            #define _MoveFactor _DesturbParams.w


            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D_X(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            
            half3 SampleSceneColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv.xy).rgb;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = float4(input.uv, mad(_SinTime.zz, _TimeFactor, input.uv));
                output.normalVS = TransformWorldToViewDir(input.normalOS) * _MoveFactor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv.xy;
                float mask = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv).g;
                clip(mask - _MainColor.a);

                float2 noiseUV = input.uv.zw;
                // float2 noiseUV = mad(_SinTime.zz, _TimeFactor, uv);
                float2 noise = SAMPLE_TEXTURE2D_X(_NoiseTex, sampler_NoiseTex, noiseUV).rg; //(0.4,0.6)
                float2 offset = noise * _NoiseFactor * input.normalVS.xy * mask * _OverFactor;

                float2 suv = input.positionCS.xy / _ScaledScreenParams.xy + offset;
                half3 screenColor = SampleSceneColor(suv);
                half3 color = screenColor * _MainColor.rgb;

                return half4(color.xyz, 1);
            }
            ENDHLSL
        }
    }
}