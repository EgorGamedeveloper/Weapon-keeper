/*
               ███████╗░█████╗░██████╗░░█████╗░███████╗  ███████╗██╗███████╗██╗░░░░░██████╗░
               ██╔════╝██╔══██╗██╔══██╗██╔══██╗██╔════╝  ██╔════╝██║██╔════╝██║░░░░░██╔══██╗
               █████╗░░██║░░██║██████╔╝██║░░╚═╝█████╗░░  █████╗░░██║█████╗░░██║░░░░░██║░░██║
               ██╔══╝░░██║░░██║██╔══██╗██║░░██╗██╔══╝░░  ██╔══╝░░██║██╔══╝░░██║░░░░░██║░░██║
               ██║░░░░░╚█████╔╝██║░░██║╚█████╔╝███████╗  ██║░░░░░██║███████╗███████╗██████╔╝
               ╚═╝░░░░░░╚════╝░╚═╝░░╚═╝░╚════╝░╚══════╝  ╚═╝░░░░░╚═╝╚══════╝╚══════╝╚═════╝░

                           ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
                           ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
                           ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
                           ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
                           ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
                           ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

                █▀▀▄ █──█ 　 ▀▀█▀▀ █──█ █▀▀ 　 ░█▀▀▄ █▀▀ ▀█─█▀ █▀▀ █── █▀▀█ █▀▀█ █▀▀ █▀▀█
                █▀▀▄ █▄▄█ 　 ─░█── █▀▀█ █▀▀ 　 ░█─░█ █▀▀ ─█▄█─ █▀▀ █── █──█ █──█ █▀▀ █▄▄▀
                ▀▀▀─ ▄▄▄█ 　 ─░█── ▀──▀ ▀▀▀ 　 ░█▄▄▀ ▀▀▀ ──▀── ▀▀▀ ▀▀▀ ▀▀▀▀ █▀▀▀ ▀▀▀ ▀─▀▀
____________________________________________________________________________________________________________________________________________

        ▄▀█ █▀ █▀ █▀▀ ▀█▀ ▀   █░█ █░░ ▀█▀ █ █▀▄▀█ ▄▀█ ▀█▀ █▀▀   ▄█ █▀█ ▄█▄   █▀ █░█ ▄▀█ █▀▄ █▀▀ █▀█ █▀
        █▀█ ▄█ ▄█ ██▄ ░█░ ▄   █▄█ █▄▄ ░█░ █ █░▀░█ █▀█ ░█░ ██▄   ░█ █▄█ ░▀░   ▄█ █▀█ █▀█ █▄▀ ██▄ █▀▄ ▄█
____________________________________________________________________________________________________________________________________________
License:
    The license is ATTRIBUTION 3.0

    More license info here:
        https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было CGPROGRAM/Built-in RP).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Force Field"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)

        _FresnelPower("Fresnel Power", Range(0, 10)) = 3
        _ScrollDirection ("Scroll Direction", float) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "IgnoreProjector"="True" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float rim         : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _FresnelPower;
                half2 _ScrollDirection;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv += _ScrollDirection * _Time.y;

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.rim = 1.0 - saturate(dot(viewDirWS, normalWS));

                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * pow(_FresnelPower, input.rim);
                pixel = lerp(0, pixel, input.rim);

                return clamp(pixel, 0, _Color);
            }
            ENDHLSL
        }
    }
}
