using UnityEngine;

/// <summary>
/// ScriptableObject с описанием предмета.
/// Создаётся через Assets > Create > Inventory > Item Data.
/// Один ассет = один "тип" предмета (например "Кружка", "Книга", "Ключ").
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Data", order = 0)]
public class ItemData : ScriptableObject
{
    [Header("Основная информация")]
    public string itemName = "Новый предмет";

    [TextArea(3, 6)]
    public string description = "Описание предмета";

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
}
