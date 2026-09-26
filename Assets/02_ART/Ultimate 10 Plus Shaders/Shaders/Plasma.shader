/*
    ██████╗░██╗░░░░░░█████╗░░██████╗███╗░░░███╗░█████╗░  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
    ██╔══██╗██║░░░░░██╔══██╗██╔════╝████╗░████║██╔══██╗  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
    ██████╔╝██║░░░░░███████║╚█████╗░██╔████╔██║███████║  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
    ██╔═══╝░██║░░░░░██╔══██║░╚═══██╗██║╚██╔╝██║██╔══██║  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
    ██║░░░░░███████╗██║░░██║██████╔╝██║░╚═╝░██║██║░░██║  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
    ╚═╝░░░░░╚══════╝╚═╝░░╚═╝╚═════╝░╚═╝░░░░░╚═╝╚═╝░░╚═╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было #pragma surface .. alpha / Built-in RP).
Прокрутка UV (_MovementDirection * _Time.y) перенесена как есть в фрагментный шейдер.
Тень не растворяется по альфе (как и у оригинала — addshadow не был указан, alpha-blend шейдеры
в Standard всё равно отбрасывают непрозрачный силуэт).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Plasma"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Normal("Normal map", 2D) = "bump" {}

        _NoiseTex ("Noise", 2D) = "white" {}
        _MovementDirection ("Movement Direction", float) = (0, -1, 0, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal);
            SAMPLER(sampler_Normal);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half2 _MovementDirection;
            CBUFFER_END

            half4 frag(U10PS_Varyings input) : SV_Target
            {
                float2 uvNoise = input.uv + _MovementDirection * _Time.y / 2.0;
                float2 uvMain = input.uv + _MovementDirection * _Time.y;
                float2 uvNormal = input.uv + _MovementDirection * _Time.y / 2.0;

                half alphaPixel = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvNoise).r;
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain) * _Color * alphaPixel;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = pixel.rgb;
                surfaceData.alpha = alphaPixel;
                surfaceData.metallic = 0;
                surfaceData.smoothness = 0;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uvNormal));
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
