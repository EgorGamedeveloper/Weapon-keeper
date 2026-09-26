/// <summary>
/// Общий контракт точки установки предмета: ячейка полки (ShelfSlot), точка ремонта (RepairPoint)
/// или место в кладке (RepairSlot).
/// Позволяет PlayerItemInteraction резолвить любую такую точку одним GetComponent,
/// без ветвления по конкретным типам.
/// </summary>
public interface IPlaceableSlot
{
    /// <summary>Можно ли сюда поставить предмет этого типа.</summary>
    bool CanAccept(ItemData item);

    /// <summary>Показать голограмму-"призрак" предмета — подсказку игроку. Режим: под прицелом (ярко),
    /// подсказка свободного места поблизости (тускло) или «видение» (сквозь стены, гаснет вместе
    /// со способностью, см. PlacementVision).</summary>
    void ShowGhost(ItemData item, GhostMode mode);

    /// <summary>Скрыть призрак.</summary>
    void HideGhost();

    /// <summary>Установить существующий физический предмет в эту точку.</summary>
    void PlaceItem(WorldItem item);
}
