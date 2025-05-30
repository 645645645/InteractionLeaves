Shader "UnityChan/Blush - Transparent"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)

        _MainTex ("Diffuse", 2D) = "white" {}
        _FalloffSampler ("Falloff Control", 2D) = "white" {}
        _RimLightSampler ("RimLight Control", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    ENDHLSL

    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha, One One
        ZWrite Off
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry+3"
            "IgnoreProjector"="True"
            "RenderType"="Overlay"
            "LightMode"="UniversalForward"
        }

        Pass
        {
            Cull Back
            ZTest LEqual
            HLSLPROGRAM
            #include "CharaSkin.cg"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }

    FallBack "Transparent/Cutout/Diffuse"
}