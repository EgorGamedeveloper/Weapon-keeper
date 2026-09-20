using UnityEngine;

/// <summary>
/// ScriptableObject-шаблон задания игрока. Создаётся через Assets > Create > Quests > Quest Data.
/// Один ассет = одно задание ("Почисти пятна крови", "Расставь патроны по полкам" и т.п.).
/// Рантайм-прогресс по конкретному запуску квеста хранится отдельно — см. QuestManager.QuestProgress.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "Quests/Quest Data", order = 20)]
public class QuestData : ScriptableObject
{
    /// <summary>Какая механика отслеживается. Уборка обломков — это ShelveItems с targetCategory,
    /// указывающим на мусорную категорию полки, отдельного типа под неё не заводим.</summary>
    public enum QuestType { CleanStains, RepairPoints, ShelveItems, DeliverItem }

    [Header("Идентификация")]
    [Tooltip("Стабильный id квеста. Заполняется автоматически из имени ассета.")]
    public string questId;

    [Header("Отображение")]
    public string title = "Новое задание";

    [TextArea(2, 4)]
    public string description = "Описание задания";

    [Header("Условие завершения")]
    public QuestType type = QuestType.ShelveItems;

    [Tooltip("Сколько единиц/точек нужно выполнить для завершения квеста.")]
    [Min(1)] public int targetCount = 1;

    [Header("Для ShelveItems — категория полки (в т.ч. мусорная для уборки обломков)")]
    public ShelfCategory targetCategory;

    [Header("Для DeliverItem")]
    [Tooltip("Какой предмет нужно донести. Пусто — подходит любой предмет.")]
    public ItemData targetItem;

    [Tooltip("Id зоны доставки (DeliveryZone.zoneId). Пусто — подходит любая зона.")]
    public string targetZoneId;

    [Header("Точечная цель (опционально)")]
    [Tooltip("Id конкретного объекта сцены (PersistentId) для CleanStains/RepairPoints/ShelveItems. " +
             "Пусто — подходит любой объект, удовлетворяющий условию выше.")]
    public string targetPersistentId;

    [Header("Награда")]
    [Tooltip("Опыт, начисляемый игроку при завершении квеста.")]
    [Min(0)] public int xpReward = 10;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(questId)) return;

        // Автозаполнение один раз, из имени ассета: Quest_ClearDebris -> cleardebris.
        // Дальше id живёт отдельно от имени, как ItemData.itemId.
        string source = name.StartsWith("Quest_") ? name.Substring("Quest_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        questId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
