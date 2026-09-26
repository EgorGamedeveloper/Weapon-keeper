using System;
using UnityEngine;

/// <summary>
/// Воспроизведение SoundCue из пула AudioSource. Статический класс — осознанное исключение из правила
/// «без синглтонов»: звук нужен из десятка несвязанных мест (предметы, полки, пятна, интерфейс), а
/// AudioSource.PlayClipAtPoint, который был раньше, на каждый звук создаёт и уничтожает объект, не умеет
/// в 2D, разброс высоты и настройки 3D.
///
/// Пул живёт на своём объекте «SoundPlayer» (DontDestroyOnLoad), создаётся при первом звуке. Когда все
/// голоса заняты, забирается самый давно начавшийся. Громкость канала (эффекты/интерфейс) применяет
/// AudioChannelVolume на каждом голосе: базовая громкость ставится при каждом звуке, а смена громкости
/// в настройках сразу касается и уже звучащих голосов. Общую громкость применяет AudioListener.volume.
/// </summary>
public static class SoundPlayer
{
    private const int VoiceCount = 24;

    private class Voice
    {
        public AudioSource source;
        public AudioChannelVolume channelVolume;
        public float startTime;
    }

    private static Voice[] voices;
    private static GameObject root;

    /// <summary>Звук запущен — для отладки и проверок (счётчики в тестах Play Mode).</summary>
    public static event Action<SoundCue, Vector3> Played;

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        voices = null;
        root = null;
        Played = null;
    }

    /// <summary>Сыграть звук в точке мира (для cue с выключенным spatial — как 2D).</summary>
    /// <param name="volumeScale">Множитель громкости этого раза (например, от силы удара), 0..1.</param>
    public static void Play(SoundCue cue, Vector3 position, float volumeScale = 1f)
    {
        PlayInternal(cue, position, cue != null && cue.spatial, volumeScale);
    }

    /// <summary>Сыграть звук без положения в мире (интерфейс, звуки самого игрока).</summary>
    public static void Play2D(SoundCue cue, float volumeScale = 1f)
    {
        PlayInternal(cue, Vector3.zero, false, volumeScale);
    }

    private static void PlayInternal(SoundCue cue, Vector3 position, bool spatial, float volumeScale)
    {
        if (cue == null || volumeScale <= 0f || !Application.isPlaying) return;
        if (!cue.TryPickClip(out AudioClip clip)) return;

        Voice voice = AcquireVoice();
        AudioSource source = voice.source;
        source.Stop();
        source.transform.position = position;
        source.clip = clip;
        source.pitch = cue.PickPitch();
        source.spatialBlend = spatial ? 1f : 0f;
        source.minDistance = cue.minDistance;
        source.maxDistance = Mathf.Max(cue.minDistance + 0.1f, cue.maxDistance);

        voice.channelVolume.SetChannel(cue.channel);
        // SetChannel включает ignoreListenerPause только для UI и не выключает — голос мог прежде
        // играть звук интерфейса, а эффекты на паузе должны замолкать.
        source.ignoreListenerPause = cue.channel == AudioChannel.UI;
        voice.channelVolume.SetBaseVolume(cue.PickVolume() * Mathf.Clamp01(volumeScale));

        source.Play();
        voice.startTime = Time.realtimeSinceStartup;
        Played?.Invoke(cue, position);
    }

    private static Voice AcquireVoice()
    {
        EnsurePool();

        Voice oldest = voices[0];
        foreach (Voice voice in voices)
        {
            if (!voice.source.isPlaying) return voice;
            if (voice.startTime < oldest.startTime) oldest = voice;
        }
        return oldest;
    }

    private static void EnsurePool()
    {
        if (root != null && voices != null) return;

        root = new GameObject("SoundPlayer");
        UnityEngine.Object.DontDestroyOnLoad(root);

        voices = new Voice[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var voiceObject = new GameObject("Voice " + i);
            voiceObject.transform.SetParent(root.transform, false);

            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;

            // Свой AudioChannelVolume есть сразу — SettingsService не станет помечать голос каналом SFX сам.
            var channelVolume = voiceObject.AddComponent<AudioChannelVolume>();
            voices[i] = new Voice { source = source, channelVolume = channelVolume, startTime = -1f };
        }
    }
}
