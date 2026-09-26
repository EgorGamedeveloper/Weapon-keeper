using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Кат-сцена в игровой сцене — то, что запускает нода «Кат-сцена» сюжетного графа (по storyId).
/// Сама сцена собирается в Unity обычным Timeline (PlayableDirector): камера, анимации, звук. Без Timeline
/// показ длится durationIfNoTimeline секунд — хватает для простого «камера смотрит на пролом».
///
/// Показом управляет StoryCutscenePlayer (очередь, управление игроком, HUD, пропуск); этот компонент
/// только включает и выключает своё: камеру, реквизит и Timeline. Чтобы актёры остались в финальной позе,
/// поставьте у PlayableDirector Wrap Mode = Hold; с None по окончании Timeline отпускает анимации.
/// Создать заготовку: Tools → Weapon Keeper → Story → Create Cutscene.
/// </summary>
[DisallowMultipleComponent]
public class StoryCutscene : MonoBehaviour
{
    [Header("Сюжет")]
    [Tooltip("Id для графа: нода «Кат-сцена» выбирает сцену по нему (после Export Scene Catalog). Латиница, уникальный в сцене.")]
    public string storyId;

    [Header("Показ")]
    [Tooltip("Timeline кат-сцены. Пусто — показ длится «Длительность без Timeline».")]
    public PlayableDirector director;

    [Tooltip("Камера кат-сцены: включается на время показа поверх камеры игрока (Depth выше). Без AudioListener. " +
             "Пусто — кат-сцена идёт через камеру игрока.")]
    public Camera cutsceneCamera;

    [Tooltip("Сколько секунд идёт показ, если Timeline не задан.")]
    [Min(0f)] public float durationIfNoTimeline = 3f;

    [Tooltip("Объекты только на время кат-сцены (актёры, реквизит): включаются в начале и выключаются в конце.")]
    public GameObject[] showDuringCutscene = Array.Empty<GameObject>();

    /// <summary>Показ начался / закончился (skipped — пропущен игроком).</summary>
    public event Action OnStarted;
    public event Action<bool> OnFinished;

    /// <summary>Идёт показ.</summary>
    public bool IsPlaying { get; private set; }

    private float elapsed;

    private void Awake()
    {
        if (director != null) director.playOnAwake = false;
        SetVisuals(false);
    }

    /// <summary>Начать показ (зовёт StoryCutscenePlayer).</summary>
    public void Begin()
    {
        IsPlaying = true;
        elapsed = 0f;
        SetVisuals(true);
        if (director != null)
        {
            director.time = 0d;
            director.Play();
        }
        OnStarted?.Invoke();
    }

    /// <summary>Кадр показа (игровое время). true — показ закончился.</summary>
    public bool Tick(float deltaTime)
    {
        if (!IsPlaying) return true;
        elapsed += deltaTime;
        if (director == null || director.playableAsset == null) return elapsed >= durationIfNoTimeline;
        // Остановился сам (Wrap Mode None) или дошёл до конца (Hold/Loop держат Playing — меряем по длительности).
        return director.state != PlayState.Playing || elapsed >= (float)director.duration;
    }

    /// <summary>Закончить показ. skipped — игрок пропустил: Timeline перематывается в конец, чтобы сцена
    /// осталась в финальном состоянии.</summary>
    public void End(bool skipped)
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        if (director != null && director.playableAsset != null)
        {
            if (skipped)
            {
                director.time = director.duration;
                director.Evaluate();
            }
            if (skipped || director.extrapolationMode == DirectorWrapMode.Loop) director.Stop();
        }
        SetVisuals(false);
        OnFinished?.Invoke(skipped);
    }

    private void SetVisuals(bool on)
    {
        if (cutsceneCamera != null) cutsceneCamera.enabled = on;
        foreach (GameObject item in showDuringCutscene)
            if (item != null) item.SetActive(on);
    }
}
