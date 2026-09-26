/*
        ░██████╗███╗░░██╗░█████╗░░██╗░░░░░░░██╗  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
        ██╔════╝████╗░██║██╔══██╗░██║░░██╗░░██║  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
        ╚█████╗░██╔██╗██║██║░░██║░╚██╗████╗██╔╝  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
        ░╚═══██╗██║╚████║██║░░██║░░████╔═████║░  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
        ██████╔╝██║░╚███║╚█████╔╝░░╚██╔╝░╚██╔╝░  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
        ╚═════╝░╚═╝░░╚══╝░╚════╝░░░░╚═╝░░░╚═╝░░  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface + #pragma vertex / Built-in RP).
_SnowAmount по-прежнему читается/пишется скриптом U10PS_SnowOverTime.cs через SetFloat.
Упрощение: коэффициент смешивания base/snow (dot(normal, snowDirection)) в оригинале считался
на вершину и интерполировался; здесь считается на пиксель из интерполированной мировой нормали —
даёт более гладкую границу снега, семантика та же.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Snow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}

        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _SnowColor ("Snow Color", Color) = (1,1,1,1)
        _SnowNormal ("Snow Normal Map", 2D) = "bump" {}

        _SnowGlossiness ("Snow Smoothness", Range(0,1)) = 0.5
        _SnowMetallic ("Snow Metallic", Range(0,1)) = 0.0

        _SnowDirection ("Snow Direction", Vector) = (0, 1, 0, 1)
        _SnowAmount ("Snow Amount", Range(0, 1)) = 0.75

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

            #include "Includes/U10PS_URPLitCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);
            TEXTURE2D(_SnowNormal);
            SAMPLER(sampler_SnowNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _SnowColor;
                half _SnowGlossiness;
                half _SnowMetallic;
                half3 _SnowDirection;
                half _SnowAmount;
            CBUFFER_END

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                half dotProduct = saturate(dot(normalize(input.normalWS), normalize(_SnowDirection)));
                dotProduct = (dotProduct < 1.0 - _SnowAmount) ? 0 : dotProduct;

                half4 basePixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half4 pixel = lerp(basePixel, _SnowColor, dotProduct);

                half4 baseNormal = SAMPLE_TEXTURE2D(_Normal, sampler_Normal, input.uv);
                half4 snowNormal = SAMPLE_TEXTURE2D(_SnowNormal, sampler_SnowNormal, input.uv);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = lerp(_Metallic, _SnowMetallic, dotProduct);
                surfaceData.smoothness = lerp(_Glossiness, _SnowGlossiness, dotProduct);
                surfaceData.occlusion = 1;
                surfaceData.normalTS = UnpackNormal(lerp(baseNormal, snowNormal, dotProduct));
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
