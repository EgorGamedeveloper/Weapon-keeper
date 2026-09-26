// Общий каркас прямого прохода URP Lit (PBR, металл/шероховатость) для ручного порта
// шейдеров пакета Ultimate 10+ Shaders со Surface Shader (Built-in RP) на URP.
//
// Каждый .shader сам объявляет свои текстуры/свойства и функцию U10PS_Surf(), которая
// заполняет SurfaceData по своей логике (аналог surf() из оригинала), а этот файл берёт
// на себя вершинный проход, тени, GI и вызов физической модели освещения URP —
// то, что раньше генерировал компилятор Surface Shader.
//
// Смещение вершин (ветер/волна/лава и т.п.) подключается через переопределение
// U10PS_VERTEX_HOOK(input) ДО #include этого файла.

#ifndef U10PS_URP_LIT_COMMON_INCLUDED
#define U10PS_URP_LIT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#ifndef U10PS_VERTEX_HOOK
#define U10PS_VERTEX_HOOK(input)
#endif

struct U10PS_Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct U10PS_Varyings
{
    float2 uv          : TEXCOORD0;
    float3 positionWS  : TEXCOORD1;
    float3 normalWS    : TEXCOORD2;
    float4 tangentWS   : TEXCOORD3;
    float3 viewDirWS   : TEXCOORD4;
    float4 shadowCoord : TEXCOORD5;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
    float4 positionCS  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

U10PS_Varyings U10PS_LitVertex(U10PS_Attributes input)
{
    U10PS_Varyings output = (U10PS_Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    U10PS_VERTEX_HOOK(input);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionWS = positionInputs.positionWS;
    output.positionCS = positionInputs.positionCS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
    output.shadowCoord = GetShadowCoord(positionInputs);
    output.uv = input.uv;

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS, output.vertexSH);

    return output;
}

half4 U10PS_LitFragment(U10PS_Varyings input, SurfaceData surfaceData)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surfaceData.normalTS, tangentToWorld));
    inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
    inputData.shadowCoord = input.shadowCoord;
    inputData.fogCoord = 0;
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    return UniversalFragmentPBR(inputData, surfaceData);
}

// --- ShadowCaster ---
// Минимальный проход теней, повторяющий тот, что Surface Shader генерировал автоматически.
// U10PS_VERTEX_HOOK(input) применяется и здесь, поэтому смещённая (ветром/волной/лавой) геометрия
// отбрасывает соответствующим образом смещённую тень.
// Опционально: #define U10PS_SHADOW_ALPHA_CLIP(uv) до #include этого файла для cutout-теней
// (Dissolve, Grass Sway) — по умолчанию тень непрозрачная, как у SurfaceOutputStandard без cutout.
#ifndef U10PS_SHADOW_ALPHA_CLIP
#define U10PS_SHADOW_ALPHA_CLIP(uv)
#endif

float3 _LightDirection;
float3 _LightPosition;

struct U10PS_ShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct U10PS_ShadowVaryings
{
    float2 uv         : TEXCOORD0;
    float4 positionCS : SV_POSITION;
};

U10PS_ShadowVaryings U10PS_ShadowVertex(U10PS_ShadowAttributes input)
{
    U10PS_ShadowVaryings output = (U10PS_ShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);

    U10PS_VERTEX_HOOK(input);

    output.uv = input.uv;

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    output.positionCS = positionCS;
    return output;
}

half4 U10PS_ShadowFragment(U10PS_ShadowVaryings input) : SV_Target
{
    U10PS_SHADOW_ALPHA_CLIP(input.uv);
    return 0;
}

#endif // U10PS_URP_LIT_COMMON_INCLUDED
