/*
███████╗██████╗░░██████╗░███████╗  ██████╗░███████╗████████╗███████╗░█████╗░████████╗██╗░█████╗░███╗░░██╗
██╔════╝██╔══██╗██╔════╝░██╔════╝  ██╔══██╗██╔════╝╚══██╔══╝██╔════╝██╔══██╗╚══██╔══╝██║██╔══██╗████╗░██║
█████╗░░██║░░██║██║░░██╗░█████╗░░  ██║░░██║█████╗░░░░░██║░░░█████╗░░██║░░╚═╝░░░██║░░░██║██║░░██║██╔██╗██║
██╔══╝░░██║░░██║██║░░╚██╗██╔══╝░░  ██║░░██║██╔══╝░░░░░██║░░░██╔══╝░░██║░░██╗░░░██║░░░██║██║░░██║██║╚████║
███████╗██████╔╝╚██████╔╝███████╗  ██████╔╝███████╗░░░██║░░░███████╗╚█████╔╝░░░██║░░░██║╚█████╔╝██║░╚███║
╚══════╝╚═════╝░░╚═════╝░╚══════╝  ╚═════╝░╚══════╝░░░╚═╝░░░╚══════╝░╚════╝░░░░╚═╝░░░╚═╝░╚════╝░╚═╝░░╚══╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было CGPROGRAM/Built-in RP, GrabPass).
GrabPass заменён на _CameraOpaqueTexture (нужно включить Opaque Texture в URP Asset — сделано
автоматически при миграции). См. те же оговорки про отличие от GrabPass, что в Blur.shader.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Edge Detection" /* Алгоритм детекции границ, реализованный в этом шейдере, называется "Sobel Edge Detection" */
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        Cull Back

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / input.screenPos.w;

                float onePixelW = 1.0 / _ScaledScreenParams.x;
                float onePixelH = 1.0 / _ScaledScreenParams.y;

                half4 pixel = abs(
                        SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - onePixelW, uv.y)) -
                        SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + onePixelW, uv.y)) +
                        SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x, uv.y + onePixelH)) -
                        SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x, uv.y - onePixelH))
                    );

                return pixel * _Color;
            }
            ENDHLSL
        }
    }
}
