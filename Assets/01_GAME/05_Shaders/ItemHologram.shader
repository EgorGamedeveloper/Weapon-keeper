// Голограмма предмета: призрак-подсказка на полке/точке ремонта (заливка + френель-рим) и
// "подтверждение установки" — светящаяся полоса, которая проходит по предмету снизу вверх по мировой
// оси Y (задаётся из кода: GhostPreviewUtility, PlacementScanEffect).
//
// Аддитивный (Blend One One): только добавляет свет, поэтому не темнеет на тёмных текстурах и не
// зависит от материала предмета — текстура предмета не используется вовсе.
Shader "Weapon Keeper/Item Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.2, 1.6, 0.25, 1)

        [Header(Ghost)]
        _FillIntensity ("Fill Intensity", Range(0, 2)) = 0.3
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.5
        _RimPower ("Rim Power", Range(0.5, 8)) = 2

        [Header(Scan Band)]
        _ScanIntensity ("Scan Intensity", Range(0, 8)) = 0
        _ScanHeight ("Scan Height (world Y)", Float) = -10000
        _BandWidth ("Band Half Width (m)", Float) = 0.03
        _TrailLength ("Trail Length (m)", Float) = 0.1
        _TrailIntensity ("Trail Intensity", Range(0, 2)) = 0.12

        [Header(Pulse)]
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 3

        _Intensity ("Overall Intensity", Range(0, 1)) = 1

        // LessEqual (4) — обычная голограмма; Always (8) — видна сквозь стены («видение», PlacementVision).
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest [_ZTest]
            Cull Back
            // Полоса рисуется поверх той же геометрии, что уже нарисовал сам предмет, — сдвиг глубины
            // убирает z-fighting.
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FillIntensity;
                half _RimIntensity;
                half _RimPower;
                half _ScanIntensity;
                float _ScanHeight;
                float _BandWidth;
                float _TrailLength;
                half _TrailIntensity;
                half _PulseAmount;
                half _PulseSpeed;
                half _Intensity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(viewDirWS, normalWS)), _RimPower);

                // Расстояние по высоте от центра полосы: > 0 — выше полосы (ещё не просканировано),
                // < 0 — ниже (уже пройдено, там остаётся быстро гаснущий шлейф).
                float offset = input.positionWS.y - _ScanHeight;
                float bandWidth = max(_BandWidth, 1e-4);
                // Узкое яркое ядро + мягкий ореол вокруг — читается как тонкая светящаяся линия.
                half core = 1.0 - smoothstep(0.0, bandWidth * 0.35, abs(offset));
                half halo = 1.0 - smoothstep(0.0, bandWidth, abs(offset));
                half band = core + halo * 0.5;
                half trail = offset < 0.0 ? exp(offset / max(_TrailLength, 1e-4)) * _TrailIntensity : 0.0;

                half glow = _FillIntensity + rim * _RimIntensity + (band + trail) * _ScanIntensity;
                // Мягкая пульсация (подсказки свободных мест): 1 - amount .. 1.
                half pulse = 1.0 - _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return half4(_Color.rgb * (glow * pulse * _Intensity * _Color.a), 1.0);
            }
            ENDHLSL
        }
    }
}
