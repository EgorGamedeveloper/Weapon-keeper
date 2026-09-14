using UnityEngine;

/// <summary>
/// Категория полки/предмета. Создаётся как ассет (Assets > Create > Inventory > Shelf Category).
/// Пример категорий: "Книги", "Оружие", "Посуда" и т.д.
/// Полка принимает только предметы с такой же категорией.
/// </summary>
[CreateAssetMenu(fileName = "NewShelfCategory", menuName = "Inventory/Shelf Category", order = 0)]
public class ShelfCategory : ScriptableObject
{
    public string categoryName;
}