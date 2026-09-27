using System.Collections.Generic;
using UnityEngine;

/// <summary>Строка таблицы лута: какой предмет, с каким весом и сколько штук за бросок.</summary>
[System.Serializable]
public class LootEntry
{
    [Tooltip("Предмет, который может выпасть.")]
    public ItemData item;

    [Tooltip("Относительный шанс выпадения среди строк таблицы.")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Минимум штук за один бросок.")]
    [Min(1)] public int minCount = 1;

    [Tooltip("Максимум штук за один бросок.")]
    [Min(1)] public int maxCount = 1;
}

/// <summary>
/// Лутбокс, который игрок заказывает в терминале: цена, время доставки, требования (лицензия ветки
/// «Каталог», уровень) и таблица лута. Создаётся через Assets > Create > Terminal > Loot Box,
/// ассеты — в 04_Data/Terminal/.
/// </summary>
[CreateAssetMenu(fileName = "NewLootBox", menuName = "Terminal/Loot Box", order = 40)]
public class LootBoxData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сейва. Заполняется автоматически из имени ассета, после выхода игры не менять.")]
    public string lootBoxId;

    [Header("Отображение")]
    [Tooltip("Название в терминале.")]
    public string title = "Ящик";

    [TextArea(2, 4)]
    [Tooltip("Описание в терминале.")]
    public string description = "Описание ящика";

    /// <summary>Название на языке игры (strings.csv, ключ lootbox.&lt;lootBoxId&gt;.title; нет строки — title).</summary>
    public string DisplayTitle => Loc.GetOr(Loc.DataKey("lootbox", lootBoxId, "title"), title);

    /// <summary>Описание на языке игры (ключ lootbox.&lt;lootBoxId&gt;.desc; нет строки — description).</summary>
    public string DisplayDescription => Loc.GetOr(Loc.DataKey("lootbox", lootBoxId, "desc"), description);

    [Tooltip("Иконка карточки в терминале.")]
    public Sprite icon;

    [Header("Покупка")]
    [Tooltip("Базовая цена (навык «Оптовик» снижает её).")]
    [Min(0)] public int price = 100;

    [Tooltip("Базовое время доставки в игровых часах (навык «Экспресс-доставка» сокращает его). Заказал " +
             "вечером — ночь, пропущенная сном, тоже идёт в зачёт: к утру ящик у точки доставки.")]
    [Min(0.1f)] public float deliveryHours = 3f;

    [Tooltip("Лицензия ветки «Каталог», без которой ящик нельзя заказать. Пусто — без лицензии.")]
    public SkillData requiredLicense;

    [Tooltip("Минимальный уровень игрока.")]
    [Min(1)] public int requiredLevel = 1;

    [Tooltip("Разовая покупка (инструмент: швабра, мойка): после заказа в терминале — «Куплено», второй раз " +
             "не заказать. Выключено — заказывается сколько угодно (патроны, катушки провода).")]
    public bool oneTimePurchase;

    [Header("Содержимое")]
    [Tooltip("Сколько раз бросается таблица лута.")]
    [Min(1)] public int rolls = 3;

    [Tooltip("Таблица лута.")]
    public LootEntry[] loot = System.Array.Empty<LootEntry>();

    /// <summary>Случайное содержимое ящика: rolls бросков по весам таблицы.</summary>
    public List<ItemData> Roll()
    {
        var result = new List<ItemData>();
        float totalWeight = 0f;
        foreach (var entry in loot)
            if (entry != null && entry.item != null) totalWeight += entry.weight;
        if (totalWeight <= 0f) return result;

        for (int i = 0; i < rolls; i++)
        {
            float pick = Random.value * totalWeight;
            foreach (var entry in loot)
            {
                if (entry == null || entry.item == null) continue;
                pick -= entry.weight;
                if (pick > 0f) continue;

                int count = Random.Range(entry.minCount, Mathf.Max(entry.minCount, entry.maxCount) + 1);
                for (int c = 0; c < count; c++) result.Add(entry.item);
                break;
            }
        }
        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(lootBoxId)) return;
        string source = name.StartsWith("LootBox_") ? name.Substring("LootBox_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant()) builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        lootBoxId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
