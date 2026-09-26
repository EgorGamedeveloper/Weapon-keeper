using UnityEngine;

/// <summary>Строка заказа: какой предмет и сколько штук.</summary>
[System.Serializable]
public class OrderLine
{
    [Tooltip("Требуемый предмет.")]
    public ItemData item;

    [Tooltip("Сколько штук.")]
    [Min(1)] public int count = 1;
}

/// <summary>
/// Шаблон заказа на отправку: кто заказал, что положить в коробку, награда и требования.
/// Терминал выдаёт такие заказы во «входящие». Создаётся через Assets > Create > Terminal > Shipping Order,
/// ассеты — в 04_Data/Terminal/.
/// </summary>
[CreateAssetMenu(fileName = "NewShippingOrder", menuName = "Terminal/Shipping Order", order = 41)]
public class ShippingOrderData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сейва. Заполняется автоматически из имени ассета, после выхода игры не менять.")]
    public string orderId;

    [Header("Отображение")]
    [Tooltip("Кто заказал — показывается в терминале.")]
    public string customer = "Заказчик";

    [TextArea(2, 3)]
    [Tooltip("Текст письма заказчика.")]
    public string message = "Нужен товар";

    [Header("Состав")]
    [Tooltip("Что нужно уложить в коробку.")]
    public OrderLine[] lines = System.Array.Empty<OrderLine>();

    [Header("Награда")]
    [Tooltip("Деньги за выполнение (навык «Торговец» увеличивает).")]
    [Min(0)] public int moneyReward = 150;

    [Tooltip("Опыт за выполнение.")]
    [Min(0)] public int xpReward = 20;

    [Header("Требования")]
    [Tooltip("Лицензия ветки «Каталог», без которой заказ не приходит. Пусто — без лицензии.")]
    public SkillData requiredLicense;

    [Tooltip("Минимальный уровень игрока.")]
    [Min(1)] public int requiredLevel = 1;

    /// <summary>Сколько штук нужно всего.</summary>
    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (var line in lines) if (line != null && line.item != null) total += line.count;
            return total;
        }
    }

    /// <summary>Сколько штук этого предмета требует заказ.</summary>
    public int RequiredCount(ItemData item)
    {
        int total = 0;
        foreach (var line in lines) if (line != null && line.item == item) total += line.count;
        return total;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(orderId)) return;
        string source = name.StartsWith("Order_") ? name.Substring("Order_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant()) builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        orderId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
