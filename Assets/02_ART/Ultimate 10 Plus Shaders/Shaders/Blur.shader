/*
           ██████╗░██╗░░░░░██╗░░░██╗██████╗░  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
           ██╔══██╗██║░░░░░██║░░░██║██╔══██╗  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
           ██████╦╝██║░░░░░██║░░░██║██████╔╝  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
           ██╔══██╗██║░░░░░██║░░░██║██╔══██╗  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
           ██████╦╝███████╗╚██████╔╝██║░░██║  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
           ╚═════╝░╚══════╝░╚═════╝░╚═╝░░╚═╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было CGPROGRAM/Built-in RP, GrabPass).
URP не поддерживает GrabPass — вместо него используется _CameraOpaqueTexture (Opaque Texture),
которую нужно включить в активном URP Asset (сделано автоматически при миграции).
Важно: в отличие от GrabPass, opaque-текстура захватывается один раз после прохода непрозрачных
объектов, а не заново перед каждым прозрачным объектом — если несколько таких материалов
стоят друг за другом, они не будут "цеплять" блюр друг друга так же, как это делал GrabPass.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Blur"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BlurAmount ("Blur Amount", Range(0, 0.03)) = 0.0128
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        Cull Back
        ZTest Always

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
                half _BlurAmount;
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

                half4 pixel = 0;

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 1.5 * _BlurAmount, uv.y + 0.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 1.5 * _BlurAmount, uv.y - 0.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 1.5 * _BlurAmount, uv.y - 1.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 1.5 * _BlurAmount, uv.y - 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y + 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y + 1.5 * _BlurAmount));

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y + 0.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y - 0.5 * _BlurAmount));

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y - 1.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x + 0.5 * _BlurAmount, uv.y - 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y + 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y + 1.5 * _BlurAmount));

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y + 0.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y - 0.5 * _BlurAmount));

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y - 1.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 0.5 * _BlurAmount, uv.y - 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 1.5 * _BlurAmount, uv.y + 2.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 1.5 * _BlurAmount, uv.y + 1.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 1.5 * _BlurAmount, uv.y + 0.5 * _BlurAmount));
                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x - 1.5 * _BlurAmount, uv.y - 0.5 * _BlurAmount));

                pixel += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, half2(uv.x, uv.y));

                return (pixel / 20.0) * _Color;
            }
            ENDHLSL
        }
    }
}
