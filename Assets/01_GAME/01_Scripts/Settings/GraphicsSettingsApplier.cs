using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Применение настроек графики: экран (разрешение, режим окна), пресет качества, VSync и лимит FPS,
/// параметры URP-ассета (масштаб рендеринга, MSAA, дальность теней) и камер сцены (тип и качество
/// сглаживания, тени, поле зрения). Вызывает SettingsService — при применении настроек и заново при
/// каждой загрузке сцены (камеры и поле зрения живут в сценах).
///
/// ВАЖНО про редактор: URP-ассет и QualitySettings в редакторе — это настоящие файлы проекта, и изменения
/// из Play Mode в них остались бы (URP-ассет записался бы с изменёнными значениями при следующем
/// сохранении проекта). Поэтому перед первым изменением исходные значения запоминаются и возвращаются
/// на выходе из Play Mode (см. блок UNITY_EDITOR внизу). Разрешение и режим окна в редакторе не
/// применяются вовсе — Game view им не подчиняется.
/// </summary>
public static class GraphicsSettingsApplier
{
    /// <summary>Разрешения монитора без повторов по ширине×высоте (частоты игнорируются — берётся
    /// максимальная при применении), от большего к меньшему. Нативное есть в списке всегда.</summary>
    public static List<Vector2Int> GetResolutionOptions()
    {
        var result = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!result.Contains(size)) result.Add(size);
        }

        Vector2Int native = GetNativeResolution();
        if (!result.Contains(native)) result.Add(native);

        result.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
        return result;
    }

    /// <summary>Нативное разрешение основного монитора.</summary>
    public static Vector2Int GetNativeResolution()
    {
        int width = Display.main.systemWidth;
        int height = Display.main.systemHeight;
        if (width <= 0 || height <= 0)
        {
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
        }
        return new Vector2Int(width, height);
    }

    /// <summary>Разрешение и режим окна. Экран не трогается, если уже в нужном состоянии, — иначе
    /// каждое «Применить» заставляло бы монитор моргать.</summary>
    public static void ApplyDisplay(GameSettingsData settings)
    {
        if (Application.isEditor) return;

        Vector2Int size = settings.resolutionWidth > 0 && settings.resolutionHeight > 0
            ? new Vector2Int(settings.resolutionWidth, settings.resolutionHeight)
            : GetNativeResolution();

        if (Screen.width == size.x && Screen.height == size.y && Screen.fullScreenMode == settings.windowMode) return;

        Screen.SetResolution(size.x, size.y, settings.windowMode, FindHighestRefreshRate(size));
    }

    /// <summary>Пресет качества, VSync и лимит FPS. Пресет переключается первым: он сам несёт свои
    /// vSyncCount и URP-ассет, поэтому остальное применяется поверх.</summary>
    public static void ApplyQuality(GameSettingsData settings, int projectDefaultLevel)
    {
        RememberEditorState(null);

        int level = settings.qualityLevel >= 0 ? settings.qualityLevel : projectDefaultLevel;
        if (level >= 0 && level < QualitySettings.names.Length && level != QualitySettings.GetQualityLevel())
        {
            QualitySettings.SetQualityLevel(level, true);
            RememberEditorState(null);
        }

        QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
        // С VSync лимит не нужен (-1 — умолчание платформы); без него 0 в настройках — «без ограничения».
        Application.targetFrameRate = settings.vSync || settings.frameRateLimit <= 0 ? -1 : settings.frameRateLimit;
    }

    /// <summary>Параметры текущего URP-ассета (он может быть свой у каждого пресета качества, поэтому
    /// применять — после ApplyQuality). TAA и MSAA вместе не включаются.</summary>
    public static void ApplyPipeline(GameSettingsData settings)
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return;

        RememberEditorState(pipeline);
        pipeline.renderScale = settings.renderScale;
        pipeline.msaaSampleCount = settings.EffectiveMsaaSamples;
        pipeline.shadowDistance = settings.shadowDistance;
    }

    /// <summary>Сглаживание, тени и MSAA — всем камерам сцены; поле зрения — главной камере, если в
    /// сцене есть игрок (камеру меню не трогаем).</summary>
    public static void ApplyCameras(GameSettingsData settings)
    {
        bool hasPlayer = Object.FindFirstObjectByType<PlayerCharacterController>() != null;
        AntialiasingMode mode = ToUrpMode(settings.postAntialiasing);
        var quality = (AntialiasingQuality)Mathf.Clamp(settings.antialiasingQuality, 0, 2);
        TemporalAAQuality taaQuality = ToTaaQuality(settings.antialiasingQuality);

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                data.antialiasing = mode;
                data.antialiasingQuality = quality;
                data.taaSettings.quality = taaQuality;
                data.renderShadows = settings.shadows;
            }

            camera.allowMSAA = settings.EffectiveMsaaSamples > 1;
            if (hasPlayer && camera.CompareTag("MainCamera")) camera.fieldOfView = settings.fieldOfView;
        }
    }

    private static AntialiasingMode ToUrpMode(PostAntialiasingMode mode)
    {
        switch (mode)
        {
            case PostAntialiasingMode.FXAA: return AntialiasingMode.FastApproximateAntialiasing;
            case PostAntialiasingMode.SMAA: return AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            case PostAntialiasingMode.TAA: return AntialiasingMode.TemporalAntiAliasing;
            default: return AntialiasingMode.None;
        }
    }

    private static TemporalAAQuality ToTaaQuality(int quality)
    {
        switch (quality)
        {
            case 0: return TemporalAAQuality.Low;
            case 1: return TemporalAAQuality.Medium;
            default: return TemporalAAQuality.High;
        }
    }

    /// <summary>Максимальная частота для этого разрешения (важна для эксклюзивного полноэкранного режима).</summary>
    private static RefreshRate FindHighestRefreshRate(Vector2Int size)
    {
        RefreshRate best = Screen.currentResolution.refreshRateRatio;
        bool found = false;
        foreach (Resolution resolution in Screen.resolutions)
        {
            if (resolution.width != size.x || resolution.height != size.y) continue;
            if (found && resolution.refreshRateRatio.value <= best.value) continue;
            best = resolution.refreshRateRatio;
            found = true;
        }
        return best;
    }

    // ───────────────────────── Редактор: откат изменений ассетов ─────────────────────────

#if UNITY_EDITOR
    private struct PipelineState
    {
        public float renderScale;
        public int msaaSampleCount;
        public float shadowDistance;
    }

    private static bool editorStateSaved;
    private static int editorQualityLevel;
    private static readonly Dictionary<int, int> editorVSyncByLevel = new Dictionary<int, int>();
    private static readonly Dictionary<UniversalRenderPipelineAsset, PipelineState> editorPipelines =
        new Dictionary<UniversalRenderPipelineAsset, PipelineState>();

    private static void RememberEditorState(UniversalRenderPipelineAsset pipeline)
    {
        if (!editorStateSaved)
        {
            editorStateSaved = true;
            editorQualityLevel = QualitySettings.GetQualityLevel();
            UnityEditor.EditorApplication.playModeStateChanged += RestoreEditorState;
        }

        int level = QualitySettings.GetQualityLevel();
        if (!editorVSyncByLevel.ContainsKey(level)) editorVSyncByLevel[level] = QualitySettings.vSyncCount;

        if (pipeline != null && !editorPipelines.ContainsKey(pipeline))
            editorPipelines[pipeline] = new PipelineState
            {
                renderScale = pipeline.renderScale,
                msaaSampleCount = pipeline.msaaSampleCount,
                shadowDistance = pipeline.shadowDistance
            };
    }

    private static void RestoreEditorState(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
        UnityEditor.EditorApplication.playModeStateChanged -= RestoreEditorState;

        foreach (KeyValuePair<UniversalRenderPipelineAsset, PipelineState> pair in editorPipelines)
        {
            if (pair.Key == null) continue;
            pair.Key.renderScale = pair.Value.renderScale;
            pair.Key.msaaSampleCount = pair.Value.msaaSampleCount;
            pair.Key.shadowDistance = pair.Value.shadowDistance;
        }

        foreach (KeyValuePair<int, int> pair in editorVSyncByLevel)
        {
            QualitySettings.SetQualityLevel(pair.Key, false);
            QualitySettings.vSyncCount = pair.Value;
        }
        QualitySettings.SetQualityLevel(editorQualityLevel, false);

        editorPipelines.Clear();
        editorVSyncByLevel.Clear();
        editorStateSaved = false;
    }
#else
    private static void RememberEditorState(UniversalRenderPipelineAsset pipeline) { }
#endif
}
