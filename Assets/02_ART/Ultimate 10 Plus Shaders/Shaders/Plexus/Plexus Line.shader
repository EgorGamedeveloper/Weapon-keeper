/*
        ░██████╗██╗███╗░░░███╗██████╗░██╗░░░░░███████╗  ░██████╗██╗███╗░░██╗███████╗
        ██╔════╝██║████╗░████║██╔══██╗██║░░░░░██╔════╝  ██╔════╝██║████╗░██║██╔════╝
        ╚█████╗░██║██╔████╔██║██████╔╝██║░░░░░█████╗░░  ╚█████╗░██║██╔██╗██║█████╗░░
        ░╚═══██╗██║██║╚██╔╝██║██╔═══╝░██║░░░░░██╔══╝░░  ░╚═══██╗██║██║╚████║██╔══╝░░
        ██████╔╝██║██║░╚═╝░██║██║░░░░░███████╗███████╗  ██████╔╝██║██║░╚███║███████╗
        ╚═════╝░╚═╝╚═╝░░░░░╚═╝╚═╝░░░░░╚══════╝╚══════╝  ╚═════╝░╚═╝╚═╝░░╚══╝╚══════╝

   ░██╗░░░░░░░██╗░█████╗░██╗░░░██╗███████╗  ░██████╗██╗░░██╗░█████╗░██████╗░███████╗██████╗░
   ░██║░░██╗░░██║██╔══██╗██║░░░██║██╔════╝  ██╔════╝██║░░██║██╔══██╗██╔══██╗██╔════╝██╔══██╗
   ░╚██╗████╗██╔╝███████║╚██╗░██╔╝█████╗░░  ╚█████╗░███████║███████║██║░░██║█████╗░░██████╔╝
   ░░████╔═████║░██╔══██║░╚████╔╝░██╔══╝░░  ░╚═══██╗██╔══██║██╔══██║██║░░██║██╔══╝░░██╔══██╗
   ░░╚██╔╝░╚██╔╝░██║░░██║░░╚██╔╝░░███████╗  ██████╔╝██║░░██║██║░░██║██████╔╝███████╗██║░░██║
   ░░░╚═╝░░░╚═╝░░╚═╝░░╚═╝░░░╚═╝░░░╚══════╝  ╚═════╝░╚═╝░░╚═╝╚═╝░░╚═╝╚═════╝░╚══════╝╚═╝░░╚═╝

License:
    The license is ATTRIBUTION 3.0
    More license info here: https://creativecommons.org/licenses/by/3.0/
____________________________________________________________________________________________________________________________________________
Портировано на Universal Render Pipeline (было CGPROGRAM/Built-in RP). Compute-шейдер Plexus.compute
и Plexus.cs изменений не требуют — они не зависят от рендер-пайплайна.
____________________________________________________________________________________________________________________________________________
*/

Shader "Ultimate 10+ Shaders/Plexus Line"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 1)
        [HDR] _Emission1 ("Emission1", Color) = (2.56, 0, 0, 1)
        [HDR] _Emission2 ("Emission2", Color) = (0, 1.95, 2.52, 1)
        _BoxDims ("Box Dimensions", float) = (5, 5, 5, 1) // Управляется Plexus.cs

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100
        Cull [_Cull]

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 col         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Emission1, _Emission2;
                half4 _BoxDims;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.col = half4((clamp(output.positionCS.xyz / _BoxDims.xyz, -1, 1) + 1.0) / 2.0, 1);

                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                return _Color + lerp(_Emission1, _Emission2, input.col);
            }
            ENDHLSL
        }
    }
}
