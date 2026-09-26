/*
            ██╗░░░░░░█████╗░██╗░░░██╗░█████╗░  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
            ██║░░░░░██╔══██╗██║░░░██║██╔══██╗  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
            ██║░░░░░███████║╚██╗░██╔╝███████║  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
            ██║░░░░░██╔══██║░╚████╔╝░██╔══██║  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
            ███████╗██║░░██║░░╚██╔╝░░██║░░██║  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
            ╚══════╝╚═╝░░╚═╝░░░╚═╝░░░╚═╝░░╚═╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface + #pragma vertex / Built-in RP).
Смещение по heightmap в вершинном шейдере подключено через U10PS_VERTEX_HOOK, используя, как и
в оригинале, второй канал UV (TEXCOORD1) со сдвигом по времени/направлению потока — Height Map
читается через SAMPLE_TEXTURE2D_LOD (аналог tex2Dlod, обязателен в вершинном шейдере).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Lava3D"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _HeightMap ("Height Map (Black and White)", 2D) = "bump" {}
        _FlowDirection ("Flow Direction", Vector) = (1, 0, 0, 0)
        _Speed ("Speed", float) = 0.25
        _Amplitude ("Amplitude", float) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 150
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
            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _FlowDirection;
                half _Speed;
                half _Amplitude;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.y = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, input.lightmapUV + _FlowDirection.xy * fmod(_Time.y, 1200) * _Speed, 0).r * _Amplitude

            #include "Includes/U10PS_URPLitCommon.hlsl"

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                float2 uv = input.uv + _FlowDirection.xy * fmod(_Time.y, 1200) * _Speed;
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = 0;
                surfaceData.smoothness = 0;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = half3(0, 0, 1);
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

            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _FlowDirection;
                half _Speed;
                half _Amplitude;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.y = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, input.lightmapUV + _FlowDirection.xy * fmod(_Time.y, 1200) * _Speed, 0).r * _Amplitude

            #include "Includes/U10PS_URPLitCommon.hlsl"
            ENDHLSL
        }
    }
}
