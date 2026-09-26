using UnityEngine;

/// <summary>
/// Кладёт "постоянные" инструменты (лом, позже швабра) в EquipmentInventory при старте сцены,
/// минуя обычный подбор — они не лежат в мире и доступны игроку с самого начала. Q их не снимает
/// (UnequipActiveWeapon реагирует только на IsWeapon), так что запись остаётся в списке всегда.
/// </summary>
public class StartingEquipment : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Инвентарь экипировки, куда кладутся стартовые инструменты.")]
    public EquipmentInventory equipmentInventory;

    [Tooltip("equipmentStorage создаётся динамически в PlayerInventoryModeController.Awake() — берём готовую ссылку оттуда " +
             "(Awake гарантированно отрабатывает раньше Start у всех компонентов сцены), а не заводим свою копию поля.")]
    public PlayerInventoryModeController modeController;

    [Header("Инструменты")]
    [Tooltip("Префабы с компонентом WorldItem, которые нужно сразу положить в экипировку (сейчас — только Crowbar.prefab).")]
    public GameObject[] startingTools;

    private void Start()
    {
        if (equipmentInventory == null || modeController == null) return;

        // При загрузке сейва экипировка (включая лом) восстанавливается SaveLoadService раньше
        // этого Start (см. DefaultExecutionOrder(-1000)), поэтому выдаём только то, чего в экипировке
        // ещё нет. Раньше здесь проверялось "нажали Продолжить" — и если сейв не загрузился, игрок
        // оставался без лома, а автосейв закреплял это навсегда.
        foreach (var prefab in startingTools)
        {
            if (prefab == null || AlreadyEquipped(prefab)) continue;
            GameObject instance = Instantiate(prefab);
            WorldItem item = instance.GetComponent<WorldItem>();
            if (item == null) continue;

            item.SetCarriedHidden(modeController.equipmentStorage);
            equipmentInventory.Add(item);
        }
    }

    private bool AlreadyEquipped(GameObject prefab)
    {
        WorldItem prefabItem = prefab.GetComponent<WorldItem>();
        if (prefabItem == null || prefabItem.itemData == null) return false;

        foreach (var item in equipmentInventory.items)
            if (item != null && item.itemData == prefabItem.itemData) return true;
        return false;
    }
}
