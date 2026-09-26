using UnityEngine;

/// <summary>
/// Громкость канала (музыка, эффекты, интерфейс, окружение) для AudioSource на этом объекте: базовая
/// громкость источника из инспектора умножается на значение канала из настроек. Общую громкость
/// применяет AudioListener.volume, поэтому здесь она не учитывается.
///
/// Это замена AudioMixer, которого в проекте нет (а .mixer из кода не создать). Позже стоит перейти
/// на AudioMixer с группами по каналам — тогда компонент станет не нужен.
///
/// Источники без компонента SettingsService сам помечает каналом SFX (оружие Easy Weapons и
/// AudioSource.PlayClipAtPoint создают свои AudioSource в рантайме). Музыку, окружение и звуки
/// интерфейса нужно пометить вручную — меню Tools/Weapon Keeper/Settings/Assign Audio Channels
/// расставит SFX всем остальным источникам сцены.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class AudioChannelVolume : MonoBehaviour
{
    [Header("Канал")]
    [Tooltip("Канал громкости из настроек игры. Звуки интерфейса (UI) играют и на паузе.")]
    public AudioChannel channel = AudioChannel.SFX;

    private AudioSource[] sources;
    private float[] baseVolumes;
    private SettingsService service;

    private void Awake()
    {
        // Все AudioSource объекта: у оружия бывает несколько источников на одном объекте.
        sources = GetComponents<AudioSource>();
        baseVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++) baseVolumes[i] = sources[i].volume;
        ApplyChannelFlags();
    }

    private void OnEnable()
    {
        service = SettingsService.Instance;
        if (service == null) return;
        service.OnSettingsApplied += Apply;
        Apply(service.Current);
    }

    private void OnDisable()
    {
        if (service != null) service.OnSettingsApplied -= Apply;
        service = null;
    }

    /// <summary>Сменить канал из кода (например, источнику музыки, созданному в рантайме).</summary>
    public void SetChannel(AudioChannel newChannel)
    {
        channel = newChannel;
        ApplyChannelFlags();
        if (service != null) Apply(service.Current);
    }

    /// <summary>Сменить базовую громкость из кода — для источника, который играет разные звуки с разной
    /// громкостью (голоса пула SoundPlayer). Громкость канала применяется сразу.</summary>
    public void SetBaseVolume(float volume)
    {
        if (sources == null) return;
        for (int i = 0; i < baseVolumes.Length; i++) baseVolumes[i] = volume;

        SettingsService current = service != null ? service : SettingsService.Instance;
        if (current != null && current.Current != null) Apply(current.Current);
        else
            foreach (AudioSource source in sources)
                if (source != null) source.volume = volume;
    }

    private void Apply(GameSettingsData settings)
    {
        if (settings == null || sources == null) return;
        float volume = settings.GetChannelVolume(channel);
        for (int i = 0; i < sources.Length; i++)
            if (sources[i] != null) sources[i].volume = baseVolumes[i] * volume;
    }

    private void ApplyChannelFlags()
    {
        // Меню паузы ставит AudioListener.pause — звуки интерфейса при этом должны звучать.
        // Остальным флаг не сбрасываем: его могли включить в инспекторе намеренно.
        if (sources == null || channel != AudioChannel.UI) return;
        foreach (AudioSource source in sources)
            if (source != null) source.ignoreListenerPause = true;
    }

    /// <summary>Пометить каналом SFX все активные AudioSource сцен, у которых канала ещё нет.
    /// Возвращает число новых компонентов.</summary>
    public static int BindUnassigned()
    {
        int added = 0;
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
        {
            if (source.GetComponent<AudioChannelVolume>() != null) continue;
            source.gameObject.AddComponent<AudioChannelVolume>();
            added++;
        }
        return added;
    }
}
