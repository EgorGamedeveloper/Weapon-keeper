/// <summary>
/// Общий контракт точки установки предмета: полка (ShelfSlot) или точка ремонта (RepairPoint).
/// Позволяет PlayerItemInteraction резолвить любую такую точку одним GetComponent,
/// без ветвления по конкретным типам.
/// </summary>
public interface IPlaceableSlot
{
    /// <summary>Можно ли сюда поставить предмет этого типа.</summary>
    bool CanAccept(ItemData item);

    /// <summary>Показать полупрозрачный "призрак" предмета — подсказка игроку перед установкой.</summary>
    void ShowGhost(ItemData item);

    /// <summary>Скрыть призрак.</summary>
    void HideGhost();

    /// <summary>Установить существующий физический предмет в эту точку.</summary>
    void PlaceItem(WorldItem item);
}
