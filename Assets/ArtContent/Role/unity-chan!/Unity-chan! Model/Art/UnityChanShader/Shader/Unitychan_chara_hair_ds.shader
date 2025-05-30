Shader "UnityChan/Hair - Double-sided"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
        _SpecularPower ("Specular Power", Float) = 20
        _EdgeThickness ("Outline Thickness", Float) = 1

        _MainTex ("Diffuse", 2D) = "white" {}
        _FalloffSampler ("Falloff Control", 2D) = "white" {}
        _RimLightSampler ("RimLight Control", 2D) = "white" {}
        _SpecularReflectionSampler ("Specular / Reflection Mask", 2D) = "white" {}
        _EnvMapSampler ("Environment Map", 2D) = "" {}
        _NormalMapSampler ("Normal Map", 2D) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "LightMode"="UniversalForward"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        ENDHLSL

        Pass
        {
            Cull Off
            ZTest LEqual
            HLSLPROGRAM
            #include "CharaMain.cg"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #define ENABLE_NORMAL_MAP
            ENDHLSL
        }

        Pass
        {
            Cull Front
            ZTest Less
            HLSLPROGRAM
            #include "CharaOutline.cg"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

    }

}