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
}