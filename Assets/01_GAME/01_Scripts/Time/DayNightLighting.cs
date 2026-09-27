using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Свет по времени суток. Солнце встаёт утром, поднимается к полудню и садится к вечеру; ночью его место
/// занимает слабый холодный «лунный» свет — те же Directional Light, развёрнутые на другую сторону неба.
/// Направленных источников может быть несколько (основной и заполняющий): каждый ведётся от своего
/// исходного направления, яркости и цвета.
/// Окружающий свет, отражения, туман и экспозиция неба темнеют вместе с ним. Свет в здании
/// (LightsActivator) не трогается: ночью светло только там, где починена проводка.
///
/// Исходные значения сцены (яркость и цвет солнца, окружающий свет, туман, экспозиция неба) запоминаются
/// в Awake и считаются «полднем» — кривые ниже задают множители к ним, поэтому настройка сцены для дня
/// остаётся как есть. Материал неба подменяется копией, чтобы Play Mode не менял сам ассет.
/// </summary>
public class DayNightLighting : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Игровые часы.")]
    public GameClock clock;

    [Tooltip("Солнечный свет — Directional Light сцены. Пусто — все включённые направленные источники сцены.")]
    public Light[] sunLights = System.Array.Empty<Light>();

    [Header("Солнце")]
    [Tooltip("Во сколько солнце на горизонте утром.")]
    [Range(0f, 12f)] public float sunriseHour = 6f;

    [Tooltip("Во сколько солнце на горизонте вечером.")]
    [Range(12f, 24f)] public float sunsetHour = 20f;

    [Tooltip("Наибольшая высота солнца в полдень, градусы.")]
    [Range(5f, 90f)] public float maxSunElevation = 60f;

    [Tooltip("На сколько градусов солнце проходит по азимуту от восхода до заката (вокруг исходного направления сцены).")]
    [Range(0f, 180f)] public float sunAzimuthSweep = 120f;

    [Tooltip("Цвет солнца от восхода (слева) до заката (справа). Умножается на цвет солнца сцены.")]
    public Gradient sunColor = DefaultSunColor();

    [Tooltip("Яркость солнца от восхода (0) до заката (1) — множитель к яркости солнца сцены.")]
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0.15f), new Keyframe(0.15f, 0.8f), new Keyframe(0.5f, 1f),
        new Keyframe(0.85f, 0.8f), new Keyframe(1f, 0.15f));

    [Tooltip("Сколько часов длятся сумерки — плавный переход между солнцем и луной вокруг восхода и заката.")]
    [Range(0.1f, 4f)] public float twilightHours = 1.5f;

    [Header("Ночь")]
    [Tooltip("Цвет лунного света.")]
    public Color moonColor = new Color(0.55f, 0.65f, 1f);

    [Tooltip("Яркость лунного света — доля яркости солнца сцены. Не 0: в темноте всё равно должно быть видно, куда идёшь.")]
    [Range(0f, 1f)] public float moonIntensity = 0.2f;

    [Tooltip("Высота луны, градусы.")]
    [Range(5f, 90f)] public float moonElevation = 40f;

    [Header("Окружение")]
    [Tooltip("Окружающий свет и отражения ночью — доля дневных.")]
    [Range(0f, 1f)] public float nightAmbient = 0.25f;

    [Tooltip("Экспозиция неба ночью — доля дневной (если у материала неба есть _Exposure).")]
    [Range(0f, 1f)] public float nightSkyExposure = 0.05f;

    [Tooltip("Цвет тумана ночью (если туман включён).")]
    public Color nightFogColor = new Color(0.03f, 0.04f, 0.07f);

    [Tooltip("Как часто пересчитывать окружающий свет от неба, игровые часы (операция не бесплатная).")]
    [Min(0.05f)] public float environmentUpdateHours = 0.25f;

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    private bool captured;
    private float[] baseSunIntensity = System.Array.Empty<float>();
    private Color[] baseSunColor = System.Array.Empty<Color>();
    private float[] baseYaw = System.Array.Empty<float>();
    private float baseAmbientIntensity;
    private float baseReflectionIntensity;
    private Color baseAmbientLight;
    private Color baseAmbientSky;
    private Color baseAmbientEquator;
    private Color baseAmbientGround;
    private Color baseFogColor;
    private Material originalSkybox;
    private Material runtimeSkybox;
    private float baseSkyExposure = -1f;
    private double lastEnvironmentUpdate = double.NegativeInfinity;

    private void Awake()
    {
        if (sunLights == null || sunLights.Length == 0)
        {
            var found = new System.Collections.Generic.List<Light>();
            foreach (var light in FindObjectsByType<Light>())
                if (light.type == LightType.Directional && light.isActiveAndEnabled) found.Add(light);
            sunLights = found.ToArray();
        }
        Capture();
    }

    private void OnDestroy()
    {
        if (!captured) return;

        // Вернуть сцене исходный вид (важно для редактора: небо — ассет, остальное — настройки сцены).
        for (int i = 0; i < sunLights.Length; i++)
        {
            if (sunLights[i] == null) continue;
            sunLights[i].intensity = baseSunIntensity[i];
            sunLights[i].color = baseSunColor[i];
        }
        if (runtimeSkybox != null)
        {
            RenderSettings.skybox = originalSkybox;
            Destroy(runtimeSkybox);
        }
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.reflectionIntensity = baseReflectionIntensity;
        RenderSettings.ambientLight = baseAmbientLight;
        RenderSettings.ambientSkyColor = baseAmbientSky;
        RenderSettings.ambientEquatorColor = baseAmbientEquator;
        RenderSettings.ambientGroundColor = baseAmbientGround;
        RenderSettings.fogColor = baseFogColor;
    }

    private void Capture()
    {
        baseSunIntensity = new float[sunLights.Length];
        baseSunColor = new Color[sunLights.Length];
        baseYaw = new float[sunLights.Length];
        for (int i = 0; i < sunLights.Length; i++)
        {
            if (sunLights[i] == null) continue;
            baseSunIntensity[i] = sunLights[i].intensity;
            baseSunColor[i] = sunLights[i].color;
            baseYaw[i] = sunLights[i].transform.eulerAngles.y;
        }

        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseReflectionIntensity = RenderSettings.reflectionIntensity;
        baseAmbientLight = RenderSettings.ambientLight;
        baseAmbientSky = RenderSettings.ambientSkyColor;
        baseAmbientEquator = RenderSettings.ambientEquatorColor;
        baseAmbientGround = RenderSettings.ambientGroundColor;
        baseFogColor = RenderSettings.fogColor;

        originalSkybox = RenderSettings.skybox;
        if (originalSkybox != null && originalSkybox.HasProperty(ExposureId))
        {
            runtimeSkybox = new Material(originalSkybox) { name = originalSkybox.name + " (день/ночь)" };
            baseSkyExposure = runtimeSkybox.GetFloat(ExposureId);
            RenderSettings.skybox = runtimeSkybox;
        }

        captured = true;
    }

    private void LateUpdate()
    {
        if (clock == null || !captured) return;
        Apply(clock.Hour);

        // Окружающий свет от неба пересчитывается не каждый кадр — это дорого, а меняется он медленно.
        if (RenderSettings.ambientMode == AmbientMode.Skybox
            && System.Math.Abs(clock.TotalHours - lastEnvironmentUpdate) >= environmentUpdateHours)
        {
            lastEnvironmentUpdate = clock.TotalHours;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void Apply(float hour)
    {
        float day = DayFactor(hour);

        float t = Mathf.InverseLerp(sunriseHour, sunsetHour, hour);
        float sunElevation = Mathf.Sin(t * Mathf.PI) * maxSunElevation;
        for (int i = 0; i < sunLights.Length; i++)
        {
            Light sun = sunLights[i];
            if (sun == null) continue;

            Quaternion sunRotation = Quaternion.Euler(sunElevation, baseYaw[i] + (t - 0.5f) * sunAzimuthSweep, 0f);
            Quaternion moonRotation = Quaternion.Euler(moonElevation, baseYaw[i] + 180f, 0f);
            sun.transform.rotation = Quaternion.Slerp(moonRotation, sunRotation, day);

            Color daylight = baseSunColor[i] * sunColor.Evaluate(t);
            sun.color = Color.Lerp(moonColor, daylight, day);
            sun.intensity = Mathf.Lerp(baseSunIntensity[i] * moonIntensity, baseSunIntensity[i] * sunIntensity.Evaluate(t), day);
        }

        float ambient = Mathf.Lerp(nightAmbient, 1f, day);
        RenderSettings.ambientIntensity = baseAmbientIntensity * ambient;
        RenderSettings.reflectionIntensity = baseReflectionIntensity * ambient;
        RenderSettings.ambientLight = baseAmbientLight * ambient;
        RenderSettings.ambientSkyColor = baseAmbientSky * ambient;
        RenderSettings.ambientEquatorColor = baseAmbientEquator * ambient;
        RenderSettings.ambientGroundColor = baseAmbientGround * ambient;

        if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(nightFogColor, baseFogColor, day);
        if (runtimeSkybox != null) runtimeSkybox.SetFloat(ExposureId, baseSkyExposure * Mathf.Lerp(nightSkyExposure, 1f, day));
    }

    /// <summary>0 — ночь, 1 — день, между ними — сумерки вокруг восхода и заката.</summary>
    private float DayFactor(float hour)
    {
        float half = twilightHours * 0.5f;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunriseHour - half, sunriseHour + half, hour));
        float set = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunsetHour + half, sunsetHour - half, hour));
        return Mathf.Min(rise, set);
    }

    private static Gradient DefaultSunColor()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.6f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.2f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(new Color(1f, 0.9f, 0.75f), 0.8f),
                new GradientColorKey(new Color(1f, 0.5f, 0.3f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
