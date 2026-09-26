/*
        ░██████╗░██████╗░░█████╗░░██╗░░░░░░░██╗  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
        ██╔════╝██╔██╗██║██╔══██╗░██║░░██╗░░██║  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
        ╚█████╗░██╔██╗██║██║░░██║░╚██╗████╗██╔╝  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
        ░╚═══██╗██║╚████║██║░░██║░░████╔═████║░  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
        ██████╔╝██║░╚███║╚█████╔╝░░╚██╔╝░╚██╔╝░  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
        ╚═════╝░╚═╝░░╚══╝░╚════╝░░░░╚═╝░░░╚═╝░░  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface .. alpha / Built-in RP).
Оригинал использовал UNITY_SETUP_BRDF_INPUT — внутренний define Standard-шейдера Built-in RP,
которого в URP просто нет; освещение переписано через SurfaceData/UniversalFragmentPBR
(см. Includes/U10PS_URPLitCommon.hlsl). "Lighting Off" в оригинальных тегах SubShader на
Surface Shader не влиял (это no-op для программных проходов) — здесь опущен, поведение то же.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Ocean"
{
    Properties
    {
        _Color ("Color", Color) = (0.0,0.25,0.35,0.0)

        _Normal1 ("Normal Map (1)", 2D) = "white" {}
        _NormalStrength1 ("Normal Strength (1)", Range(0, 2)) = 0.17
        _FlowDirection1("Flow Direction (1)", float) = (0.05, 0, 0, 1)

        _Normal2 ("Normal Map (2)", 2D) = "white" {}
        _NormalStrength2 ("Normal Strength (2)", Range(0, 2)) = 0.8
        _FlowDirection2("Flow Direction (2)", float) = (0, 0.05, 0, 1)

        _Glossiness ("Smoothness", Range(0,1)) = 0.6
        _Metallic ("Metallic", Range(0,1)) = 0.2

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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

            #include "Includes/U10PS_URPLitCommon.hlsl"

            TEXTURE2D(_Normal1);
            SAMPLER(sampler_Normal1);
            TEXTURE2D(_Normal2);
            SAMPLER(sampler_Normal2);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _NormalStrength1;
                half2 _FlowDirection1;
                half _NormalStrength2;
                half2 _FlowDirection2;
                half _Glossiness;
                half _Metallic;
            CBUFFER_END

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                float2 uv1 = input.uv + _Time.y * _FlowDirection1;
                float2 uv2 = input.uv + _Time.y * _FlowDirection2;

                half4 normalBlend = SAMPLE_TEXTURE2D(_Normal1, sampler_Normal1, uv1) * _NormalStrength1
                                   + SAMPLE_TEXTURE2D(_Normal2, sampler_Normal2, uv2) * _NormalStrength2;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _Color.rgb;
                surfaceData.alpha = _Color.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Glossiness;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = UnpackNormal(normalBlend);
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

            #include "Includes/U10PS_URPLitCommon.hlsl"
            ENDHLSL
        }
    }
}
