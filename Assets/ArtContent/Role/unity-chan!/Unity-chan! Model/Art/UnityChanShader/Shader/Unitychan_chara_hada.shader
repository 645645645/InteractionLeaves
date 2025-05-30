Shader "UnityChan/Skin"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
        _EdgeThickness ("Outline Thickness", Float) = 1

        _MainTex ("Diffuse", 2D) = "white" {}
        _FalloffSampler ("Falloff Control", 2D) = "white" {}
        _RimLightSampler ("RimLight Control", 2D) = "white" {}
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
            Cull Back
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "CharaSkin.cg"
            ENDHLSL
        }

        Pass
        {
            Cull Front
            ZTest Less
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "CharaOutline.cg"
            ENDHLSL
        }

    }

    FallBack "Transparent/Cutout/Diffuse"
}