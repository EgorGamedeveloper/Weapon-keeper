/*
            ░██████╗░██████╗░░█████╗░░██████╗░██████╗  ░██████╗░██╗░░░░░░░██╗░█████╗░██╗░░░██╗
            ██╔════╝░██╔══██╗██╔══██╗██╔════╝██╔════╝  ██╔════╝░██║░░██╗░░██║██╔══██╗╚██╗░██╔╝
            ██║░░██╗░██████╔╝███████║╚█████╗░╚█████╗░  ╚█████╗░░╚██╗████╗██╔╝███████║░╚████╔╝░
            ██║░░╚██╗██╔══██╗██╔══██║░╚═══██╗░╚═══██╗  ░╚═══██╗░░████╔═████║░██╔══██║░░╚██╔╝░░
            ╚██████╔╝██║░░██║██║░░██║██████╔╝██████╔╝  ██████╔╝░░╚██╔╝░╚██╔╝░██║░░██║░░░██║░░░
            ░╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚═════╝░  ╚═════╝░░░░╚═╝░░░╚═╝░░╚═╝░░╚═╝░░░╚═╝░░░

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface + #pragma vertex / Built-in RP).
Раскачивание ветром подключено через U10PS_VERTEX_HOOK, отбрасываемая тень получает тот же cutout
и ту же раскачку (было addshadow в оригинале).
Упрощение: у оригинала _WindDirection.w (по умолчанию 1) по ошибке прибавлялся к .w позиции вершины,
что на практике портило бы проекцию — здесь используется только _WindDirection.xyz как направление.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Grass Sway"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", float) = 0.25

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.5

        _Cutoff ("Cutoff", Range(0, 1)) = 0.25
        _Speed ("Speed", float) = 0.25
        _WindDirection ("Wind Direction", float) = (1,0,0,1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 200
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex U10PS_LitVertex
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _NormalStrength;
                half _Smoothness;
                half _Metallic;
                half _Cutoff;
                half _Speed;
                half4 _WindDirection;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.xyz += TransformObjectToWorldDir(input.positionOS.xyz).y * _WindDirection.xyz * sin(_Time.y * _Speed)

            #include "Includes/U10PS_URPLitCommon.hlsl"

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(pixel.a - _Cutoff);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uv) * _NormalStrength);
                surfaceData.emission = 0;

                return U10PS_LitFragment(input, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex U10PS_ShadowVertex
            #pragma fragment U10PS_ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half _Speed;
                half4 _WindDirection;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.xyz += TransformObjectToWorldDir(input.positionOS.xyz).y * _WindDirection.xyz * sin(_Time.y * _Speed)
            #define U10PS_SHADOW_ALPHA_CLIP(uv) clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a - _Cutoff)

            #include "Includes/U10PS_URPLitCommon.hlsl"
            ENDHLSL
        }
    }
}
