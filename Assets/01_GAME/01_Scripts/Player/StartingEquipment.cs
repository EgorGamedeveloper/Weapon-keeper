using UnityEngine;

/// <summary>
/// Кладёт "постоянные" инструменты (лом, позже швабра) в EquipmentInventory при старте сцены,
/// минуя обычный подбор — они не лежат в мире и доступны игроку с самого начала. Q их не снимает
/// (UnequipActiveWeapon реагирует только на IsWeapon), так что запись остаётся в списке всегда.
/// </summary>
public class StartingEquipment : MonoBehaviour
{
    public EquipmentInventory equipmentInventory;

    [Tooltip("equipmentStorage создаётся динамически в PlayerInventoryModeController.Awake() — берём готовую ссылку оттуда " +
             "(Awake гарантированно отрабатывает раньше Start у всех компонентов сцены), а не заводим свою копию поля.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Префабы с компонентом WorldItem, которые нужно сразу положить в экипировку (сейчас — только Crowbar.prefab).")]
    public GameObject[] startingTools;

    private void Start()
    {
        if (equipmentInventory == null || modeController == null) return;

        foreach (var prefab in startingTools)
        {
            if (prefab == null) continue;
            GameObject instance = Instantiate(prefab);
            WorldItem item = instance.GetComponent<WorldItem>();
            if (item == null) continue;

            item.SetCarriedHidden(modeController.equipmentStorage);
            equipmentInventory.Add(item);
        }
    }
}
