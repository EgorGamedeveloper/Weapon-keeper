using UnityEngine;

/// <summary>
/// Категория полки/предмета. Создаётся как ассет (Assets > Create > Inventory > Shelf Category).
/// Пример категорий: "Книги", "Оружие", "Посуда" и т.д.
/// Полка принимает только предметы с такой же категорией.
/// </summary>
[CreateAssetMenu(fileName = "NewShelfCategory", menuName = "Inventory/Shelf Category", order = 0)]
public class ShelfCategory : ScriptableObject
{
    [Tooltip("Название категории (для отладки и интерфейса).")]
    public string categoryName;

    [Header("Опыт")]
    [Tooltip("Опыт за каждый предмет этой категории, ВПЕРВЫЕ поставленный на полку " +
             "(для мусорной категории — за выброс в контейнер).")]
    [Min(0)] public int xpPerPlacedItem = 2;
}