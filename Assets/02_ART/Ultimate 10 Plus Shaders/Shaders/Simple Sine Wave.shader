/*
        ░██████╗██╗███╗░░░███╗██████╗░██╗░░░░░███████╗  ░██████╗██╗███╗░░██╗███████╗
        ██╔════╝██║████╗░████║██╔══██╗██║░░░░░██╔════╝  ██╔════╝██║████╗░██║██╔════╝
        ╚█████╗░██║██╔████╔██║██████╔╝██║░░░░░█████╗░░  ╚█████╗░██║██╔██╗██║█████╗░░
        ░╚═══██╗██║██║╚██╔╝██║██╔═══╝░██║░░░░░██╔══╝░░  ░╚═══██╗██║██║╚████║██╔══╝░░
        ██████╔╝██║██║░╚═╝░██║██║░░░░░███████╗███████╗  ██████╔╝██║██║░╚███║███████╗
        ╚═════╝░╚═╝╚═╝░░░░░╚═╝╚═╝░░░░░╚══════╝╚══════╝  ╚═════╝░╚═╝╚═╝░░╚══╝╚══════╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface + #pragma vertex / Built-in RP).
Вершинная синус-волна подключена через U10PS_VERTEX_HOOK — применяется и в ShadowCaster,
поэтому тень тоже "волнуется" вместе с геометрией (как и было с addshadow в оригинале).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Simple Sine Wave"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}
        _Speed ("Speed", float) = 1.25
        _Amplitude ("Amplitude", float) = 1.0

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.5

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 150
        Cull [_Cull]
        ZWrite On

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
                half _Smoothness;
                half _Metallic;
                half _Speed;
                half _Amplitude;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.y = sin((_Time.y + input.positionOS.x) * _Speed) * _Amplitude

            #include "Includes/U10PS_URPLitCommon.hlsl"

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uv));
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

            CBUFFER_START(UnityPerMaterial)
                half _Speed;
                half _Amplitude;
            CBUFFER_END

            #define U10PS_VERTEX_HOOK(input) input.positionOS.y = sin((_Time.y + input.positionOS.x) * _Speed) * _Amplitude

            #include "Includes/U10PS_URPLitCommon.hlsl"
            ENDHLSL
        }
    }
}
