using UnityEngine;

/// <summary>
/// Временный эффект на игроке: действие стимулятора («Кофе», «Энергетик», «Шприц»), «Отходняк» после
/// шприца, утренние «Выспался» и «Разбитость». Создаётся через Assets > Create > Survival > Status Effect,
/// ассеты живут в 04_Data/Survival/Effects/. Какие эффекты сейчас действуют и сколько им осталось, ведёт
/// PlayerStatusEffects; сам ассет только описывает, что эффект делает.
/// </summary>
[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Survival/Status Effect", order = 41)]
public class StatusEffectData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сейва. Заполняется автоматически из имени ассета (Effect_Coffee → coffee). " +
             "После выхода игры менять НЕЛЬЗЯ — сейвы потеряют действующий эффект.")]
    public string effectId;

    [Header("Отображение")]
    [Tooltip("Название эффекта (подсказка у иконки в HUD).")]
    public string title = "Новый эффект";

    [Tooltip("Иконка в ряду эффектов рядом с полосой выносливости.")]
    public Sprite icon;

    [Tooltip("Эффект вредный («Отходняк», «Разбитость») — иконка красится предупреждающим цветом.")]
    public bool isNegative;

    /// <summary>Название на языке игры (strings.csv, ключ effect.&lt;effectId&gt;.title; нет строки — title).</summary>
    public string DisplayTitle => Loc.GetOr(Loc.DataKey("effect", effectId, "title"), title);

    [Header("Длительность")]
    [Tooltip("Сколько игровых часов действует. Утренние эффекты длительность задаёт сон (до SleepSettings.morningEffectsUntilHour).")]
    [Min(0.05f)] public float durationHours = 2f;

    [Tooltip("Стимулятор: на него действуют толерантность (каждый следующий за день слабее) и навык " +
             "«Кофеман» (SkillStat.StimulantDuration, StimulantCrash).")]
    public bool isStimulant;

    [Header("Действие")]
    [Tooltip("Сколько усталости временно перекрывает: на столько бар длиннее, пока эффект идёт. −1 — вся усталость.")]
    public float fatigueMask;

    [Tooltip("Множитель скорости восстановления бара (1 — без изменений, 1.5 — в полтора раза быстрее).")]
    [Min(0f)] public float regenMultiplier = 1f;

    [Tooltip("Множитель расхода бара на бег, прыжки и удары (1 — без изменений, 0.5 — вдвое меньше).")]
    [Min(0f)] public float staminaCostMultiplier = 1f;

    [Header("Окончание")]
    [Tooltip("Откат: столько настоящей усталости добавляется, когда эффект кончается. Её снимает только сон.")]
    [Min(0f)] public float crashFatigue;

    [Tooltip("Что начинается, когда этот эффект кончился (например «Отходняк» после шприца). Пусто — ничего. " +
             "Если игрок лёг спать раньше, следующий эффект не начинается — он его «проспал».")]
    public StatusEffectData followUpEffect;

    /// <summary>Эффект перекрывает всю усталость (шприц).</summary>
    public bool MasksAllFatigue => fatigueMask < 0f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (followUpEffect == this)
            Debug.LogWarning($"[StatusEffectData] Эффект '{name}' указан следующим за самим собой — он никогда не кончится.", this);

        if (!string.IsNullOrEmpty(effectId)) return;

        // Автозаполнение один раз, из имени ассета: Effect_Coffee -> coffee. Дальше id живёт отдельно
        // от имени — как ItemData.itemId.
        string source = name.StartsWith("Effect_") ? name.Substring("Effect_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        effectId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
