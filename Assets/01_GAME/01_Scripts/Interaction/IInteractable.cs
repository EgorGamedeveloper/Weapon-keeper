/// <summary>
/// Объект, который игрок использует кликом, не ставя в него предмет: терминал, кнопки лифта,
/// разбор коробки отменённого заказа. Введён по той же причине, что и IPlaceableSlot: таких
/// объектов сразу несколько, и без общего контракта PlayerItemInteraction оброс бы веткой
/// if/else на каждый новый тип.
/// </summary>
public interface IInteractable
{
    /// <summary>Заголовок подсказки при наведении («Терминал», «Лифт»).</summary>
    string InteractTitle { get; }

    /// <summary>Текст подсказки: что сделает клик или почему сейчас нельзя («Нет питания…»).</summary>
    string InteractHint { get; }

    /// <summary>false — подсказка показывается, но клик ничего не делает. Если объект заодно
    /// WorldItem (коробка), клик уходит в обычный подбор.</summary>
    bool CanInteract { get; }

    /// <summary>Использовать объект.</summary>
    void Interact();
}
