/*
██████╗░███████╗██████╗░████████╗██╗░░██╗  ███╗░░░███╗░█████╗░░██████╗██╗░░██╗  ███████╗██████╗░░██████╗░███████╗
██╔══██╗██╔════╝██╔══██╗╚══██╔══╝██║░░██║  ████╗░████║██╔══██╗██╔════╝██║░██╔╝  ██╔════╝██╔══██╗██╔════╝░██╔════╝
██║░░██║█████╗░░██████╔╝░░░██║░░░███████║  ██╔████╔██║███████║╚█████╗░█████═╝░  █████╗░░██║░░██║██║░░██╗░█████╗░░
██║░░██║██╔══╝░░██╔═══╝░░░░██║░░░██╔══██║  ██║╚██╔╝██║██╔══██║░╚═══██╗██╔═██╗░  ██╔══╝░░██║░░██║██║░░╚██╗██╔══╝░░
██████╔╝███████╗██║░░░░░░░░██║░░░██║░░██║  ██║░╚═╝░██║██║░░██║██████╔╝██║░╚██╗  ███████╗██████╔╝╚██████╔╝███████╗
╚═════╝░╚══════╝╚═╝░░░░░░░░╚═╝░░░╚═╝░░╚═╝  ╚═╝░░░░░╚═╝╚═╝░░╚═╝╚═════╝░╚═╝░░╚═╝  ╚══════╝╚═════╝░░╚═════╝░╚══════╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было CGPROGRAM/Built-in RP).
_CameraDepthTexture теперь объявляется через DeclareDepthTexture.hlsl и SampleSceneDepth (нужно
включить Depth Texture в URP Asset — сделано автоматически при миграции). Порядок операций
(разница четырёх сэмплов глубины, затем Linear01Depth от этой разницы) сохранён таким же
странноватым, каким был в оригинале, чтобы не менять визуальный результат.
Работает только при наличии Directional Light в сцене (ограничение оригинального шейдера).
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Depth Mask Edge Detection"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

                float rawDiff =
                        SampleSceneDepth(float2(uv.x - onePixelW, uv.y)) -
                        SampleSceneDepth(float2(uv.x + onePixelW, uv.y)) +
                        SampleSceneDepth(float2(uv.x, uv.y + onePixelH)) -
                        SampleSceneDepth(float2(uv.x, uv.y - onePixelH));

                half4 pixel = Linear01Depth(abs(rawDiff), _ZBufferParams);

                return pixel * _Color;
            }
            ENDHLSL
        }
    }
}
