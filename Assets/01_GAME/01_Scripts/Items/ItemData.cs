using UnityEngine;

/// <summary>
/// ScriptableObject с описанием предмета.
/// Создаётся через Assets > Create > Inventory > Item Data.
/// Один ассет = один "тип" предмета (например "Кружка", "Книга", "Ключ").
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Data", order = 0)]
public class ItemData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сохранений (например ammobox_556). Заполняется автоматически из имени ассета. " +
             "После выхода игры менять НЕЛЬЗЯ — сейвы игроков перестанут находить предмет.")]
    public string itemId;

    [Header("Основная информация")]
    [Tooltip("Название предмета для интерфейса.")]
    public string itemName = "Новый предмет";

    [TextArea(3, 6)]
    [Tooltip("Описание для панели информации о предмете.")]
    public string description = "Описание предмета";

    /// <summary>Название на языке игры (strings.csv, ключ item.&lt;itemId&gt;.name; нет строки — itemName).</summary>
    public string DisplayName => Loc.GetOr(Loc.DataKey("item", itemId, "name"), itemName);

    /// <summary>Описание на языке игры (ключ item.&lt;itemId&gt;.desc; нет строки — description).</summary>
    public string DisplayDescription => Loc.GetOr(Loc.DataKey("item", itemId, "desc"), description);

    [Tooltip("Иконка для инвентаря.")]
    public Sprite icon;

    [Header("Инвентарь tidy-up")]
    [Tooltip("Если включено, все единицы выбранного типа видны в руке стопкой. Отключите для оружия и крупных предметов.")]
    public bool showAsVisualStack = false;

    [Tooltip("Предмет можно перенести из tidy-up в слот экипировки клавишей Q.")]
    public bool canEquip = false;

    [Tooltip("Расстояние между предметами в визуальной стопке в руке.")]
    [Min(0f)] public float heldStackSpacing = 0.08f;

    [Header("Визуал предмета")]
    [Tooltip("Префаб визуальной модели предмета. Используется и в мире, и на полке, и в руке игрока.")]
    public GameObject worldPrefab;

    [Tooltip("Локальное смещение модели в руке игрока (точка EquippedItemHolder.handPoint).")]
    public Vector3 handPositionOffset;

    [Tooltip("Локальный поворот модели в руке игрока.")]
    public Vector3 handRotationOffset;

    [Header("Совместимость с полками")]
    [Tooltip("На какую категорию полки можно поставить этот предмет.")]
    public ShelfCategory shelfType;

    [Header("Оружие (Easy Weapons)")]
    [Tooltip("Если задано — при экипировке предмет становится настоящим оружием Easy Weapons (стреляет). Ссылка на префаб с компонентом Weapon.")]
    public GameObject weaponPrefab;

    /// <summary>Признак того, что предмет — оружие (можно экипировать и оно стреляет через Easy Weapons).</summary>
    public bool IsWeapon => weaponPrefab != null;

    [Header("Инструмент (разбор Breakable)")]
    [Tooltip("Инструмент для разбора Breakable-объектов (например, лом). Не оружие — weaponPrefab не участвует.")]
    public bool canBreakObjects = false;

    [Header("Эффекты установки")]
    [Tooltip("Звук при установке предмета на место (полка и т.п.). Не задан — тихо, без ошибки.")]
    public AudioClip placementSound;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(itemId)) return;

        // Автозаполнение один раз, из имени ассета: Item_AmmoBox_556 -> ammobox_556.
        // Дальше id живёт отдельно от имени — переименование ассета его не трогает,
        // иначе у игроков поехали бы сейвы.
        string source = name.StartsWith("Item_") ? name.Substring("Item_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        itemId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
