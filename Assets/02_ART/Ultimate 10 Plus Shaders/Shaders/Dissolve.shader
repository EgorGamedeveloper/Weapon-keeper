/*
    ██████╗░██╗░██████╗░██████╗░█████╗░██╗░░░░░██╗░░░██╗███████╗  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
    ██╔══██╗██║██╔════╝██╔════╝██╔══██╗██║░░░░░██║░░░██║██╔════╝  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
    ██║░░██║██║╚█████╗░╚█████╗░██║░░██║██║░░░░░╚██╗░██╔╝█████╗░░  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
    ██║░░██║██║░╚═══██╗░╚═══██╗██║░░██║██║░░░░░░╚████╔╝░██╔══╝░░  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
    ██████╔╝██║██████╔╝██████╔╝╚█████╔╝███████╗░░╚██╔╝░░███████╗  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
    ╚═════╝░╚═╝╚═════╝░╚═════╝░░╚════╝░╚══════╝░░░╚═╝░░░╚══════╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface / Built-in RP).
_Cutoff по-прежнему читается/пишется скриптом U10PS_DissolveOverTime.cs через SetFloat — имя
и семантика свойства не менялись. ShadowCaster учитывает clip по _NoiseTex/_Cutoff, поэтому тень
растворяется вместе с объектом (было addshadow у оригинала).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Dissolve"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "white" {}

        _Cutoff ("Cut off", Range(0, 1)) = 0.25
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.05
        [HDR] _EdgeColor ("Edge Color", Color) = (1,1,1,1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Geometry" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
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

            #include "Includes/U10PS_URPLitCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                half4 _Color;
                half4 _EdgeColor;
                half _Cutoff;
                half _EdgeWidth;
            CBUFFER_END

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv).r;

                clip(noise >= _Cutoff ? 1 : -1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = 0;
                surfaceData.smoothness = 0;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = noise >= (_Cutoff * (_EdgeWidth + 1.0)) ? half3(0, 0, 0) : _EdgeColor.rgb;

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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half _Cutoff;
            CBUFFER_END

            #define U10PS_SHADOW_ALPHA_CLIP(uv) clip(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r >= _Cutoff ? 1 : -1)

            #include "Includes/U10PS_URPLitCommon.hlsl"
            ENDHLSL
        }
    }
}
