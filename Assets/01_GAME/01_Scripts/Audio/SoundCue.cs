using UnityEngine;

/// <summary>
/// Один игровой звук («шаг по бетону», «установка оружия на полку», «клик кнопки»): набор вариантов,
/// разброс громкости и высоты, 2D/3D и канал громкости из настроек. Играется через SoundPlayer:
/// <c>SoundPlayer.Play(cue, position)</c> или <c>SoundPlayer.Play2D(cue)</c> — оба спокойно принимают null,
/// так что незаданный звук просто молчит.
///
/// Ассеты лежат в 04_Data/Audio/Cues. Меню Tools/Weapon Keeper/Audio/Create Sound Assets пересобирает
/// стандартный набор из клипов Kenney (02_ART/Audio/Kenney), подобранные вручную значения затираются.
/// </summary>
[CreateAssetMenu(fileName = "Sound_New", menuName = "Audio/Sound Cue", order = 0)]
public class SoundCue : ScriptableObject
{
    [Header("Клипы")]
    [Tooltip("Варианты звука: каждый раз играется случайный, но не тот же, что в прошлый раз.")]
    public AudioClip[] clips;

    [Header("Громкость и высота")]
    [Tooltip("Базовая громкость (дальше умножается на громкость канала из настроек).")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Случайное снижение громкости, доля: 0.15 — от −15% до полной.")]
    [Range(0f, 1f)] public float volumeVariation = 0.1f;

    [Tooltip("Случайная высота (pitch): мин и макс. Небольшой разброс убирает «пулемётный» эффект одинаковых звуков.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Пространство")]
    [Tooltip("Канал громкости из настроек игры. Звуки канала UI играют и на паузе.")]
    public AudioChannel channel = AudioChannel.SFX;

    [Tooltip("3D-звук: громкость и панорама зависят от того, где он прозвучал. Выключено — 2D " +
             "(интерфейс и звуки самого игрока: шаги, прыжок).")]
    public bool spatial = true;

    [Tooltip("Ближе этой дистанции 3D-звук играет на полной громкости, м.")]
    [Min(0.01f)] public float minDistance = 1f;

    [Tooltip("Дальше этой дистанции 3D-звук больше не затихает, м.")]
    [Min(0.1f)] public float maxDistance = 20f;

    [Header("Частота")]
    [Tooltip("Минимальный интервал между воспроизведениями этого звука, секунды: пачка одновременных " +
             "столкновений (рассыпанная стопка) не превращается в треск.")]
    [Min(0f)] public float minInterval = 0.03f;

    // Не сериализуются: состояние текущего запуска. realtimeSinceStartup, а не Time.time — ассет живёт
    // дольше одного Play Mode, а Time.time в новом запуске снова начинается с нуля.
    [System.NonSerialized] private int lastClipIndex = -1;
    [System.NonSerialized] private float lastPlayTime = -1000f;

    /// <summary>Выбрать клип для воспроизведения. false — звук сейчас играть не нужно (нет клипов или
    /// не прошёл minInterval с прошлого раза).</summary>
    public bool TryPickClip(out AudioClip clip)
    {
        clip = null;
        if (clips == null || clips.Length == 0) return false;

        float now = Time.realtimeSinceStartup;
        if (now - lastPlayTime < minInterval) return false;

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastClipIndex) index = (index + 1) % clips.Length;

        clip = clips[index];
        if (clip == null) return false;

        lastClipIndex = index;
        lastPlayTime = now;
        return true;
    }

    /// <summary>Громкость одного воспроизведения с учётом случайного разброса (без канала настроек).</summary>
    public float PickVolume() => volume * (1f - Random.value * volumeVariation);

    /// <summary>Высота одного воспроизведения.</summary>
    public float PickPitch() => Random.Range(pitchRange.x, pitchRange.y);
}
