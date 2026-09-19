using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Связывает слот экипировки (EquipmentInventory) со стрельбой Easy Weapons (WeaponSystem).
/// Когда активным предметом экипировки становится ItemData с заданным weaponPrefab —
/// спавнит этот префаб (один раз на предмет, дальше переиспользует) и делает его активным
/// оружием WeaponSystem. Экипировано может быть только одно оружие одновременно —
/// это гарантирует PlayerInventoryModeController, мост просто отражает текущее состояние.
/// Оружие видно и может стрелять только в режиме экипировки (modeController.IsWeaponEquipped()) —
/// переключение на tidy-up (клавиша 1) прячет его точно так же, как WeaponHolster.
/// При появлении оружия плавно доводит его из точки удержания tidy-up (HeldItemTransform)
/// в его боевую позицию (локальный ноль относительно weaponMountPoint) — без DOTween,
/// вручную в Update, по тому же принципу, что и PlayerItemInteraction.UpdatePickupAnimation.
/// Пока прицел наведён на предмет/полку (PlayerItemInteraction.IsAimingAtInteractable) — стрельба
/// полностью блокируется через Weapon.fireBlocked, клик в этом случае подбирает/ставит предмет.
/// Порядок выполнения — после PlayerItemInteraction (тот считает луч этого кадра раньше),
/// но до Weapon (по умолчанию 0), чтобы fireBlocked долетал до проверки в том же кадре.
/// </summary>
[DefaultExecutionOrder(-100)]
public class EquipmentWeaponBridge : MonoBehaviour
{
    [Header("Ссылки")]
    public EquipmentInventory equipmentInventory;
    public WeaponSystem weaponSystem;

    [Tooltip("Режим tidy-up/экипировки — оружие активно и видимо только в режиме экипировки.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Опционально: если задан, мост сам прячет/достаёт оружие через него при снятии/экипировке/смене режима.")]
    public WeaponHolster weaponHolster;

    [Tooltip("Точка, под которую спавнятся префабы оружия Easy Weapons (обычно тот же объект, где WeaponSystem). Боевая позиция оружия — локальный ноль относительно этой точки.")]
    public Transform weaponMountPoint;

    [Tooltip("Держатель предмета tidy-up — источник стартовой точки анимации появления оружия (его HeldItemTransform).")]
    public EquippedItemHolder tidyUpHolder;

    [Tooltip("Пока прицел наведён на предмет или ячейку полки (IsAimingAtInteractable) — стрельба блокируется, клик должен подбирать/ставить предмет.")]
    public PlayerItemInteraction playerItemInteraction;

    [Header("Анимация появления оружия")]
    [Tooltip("Скорость интерполяции из руки tidy-up в боевую позицию (аналогично PlayerItemInteraction.pickupAnimationSpeed).")]
    public float drawAnimationSpeed = 10f;

    private readonly Dictionary<WorldItem, GameObject> spawnedWeapons = new Dictionary<WorldItem, GameObject>();
    private readonly Dictionary<GameObject, Weapon> weaponComponents = new Dictionary<GameObject, Weapon>();

    private GameObject activeWeaponGO;
    private GameObject drawingWeapon;
    private Vector3 drawVelocity;

    private void OnEnable()
    {
        if (equipmentInventory != null) equipmentInventory.OnChanged += HandleChanged;
        if (modeController != null) modeController.OnModeChanged += HandleModeChanged;
        HandleChanged();
    }

    private void OnDisable()
    {
        if (equipmentInventory != null) equipmentInventory.OnChanged -= HandleChanged;
        if (modeController != null) modeController.OnModeChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(PlayerInventoryModeController.InventoryMode mode) => HandleChanged();

    private void Update()
    {
        // Пока прицел наведён на предмет/полку — стрелять нельзя, клик должен подбирать/ставить
        // предмет (это уже делает PlayerItemInteraction.HandleClick независимо от режима).
        if (activeWeaponGO != null && weaponComponents.TryGetValue(activeWeaponGO, out Weapon activeWeapon) && activeWeapon != null)
            activeWeapon.fireBlocked = playerItemInteraction != null && playerItemInteraction.IsAimingAtInteractable;

        if (drawingWeapon == null) return;

        // Боевая позиция всегда локальный ноль относительно weaponMountPoint — её задаёт сам
        // родительский объект (weaponMountPoint уже стоит там, где должно быть оружие в руке).
        Transform t = drawingWeapon.transform;

        t.localPosition = Vector3.SmoothDamp(
            t.localPosition,
            Vector3.zero,
            ref drawVelocity,
            1f / Mathf.Max(0.01f, drawAnimationSpeed),
            Mathf.Infinity,
            Time.deltaTime);
        t.localRotation = Quaternion.Slerp(
            t.localRotation,
            Quaternion.identity,
            1f - Mathf.Exp(-drawAnimationSpeed * Time.deltaTime));

        if (Vector3.Distance(t.localPosition, Vector3.zero) < 0.005f)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            drawingWeapon = null;
        }
    }

    private void HandleChanged()
    {
        if (equipmentInventory == null || weaponSystem == null || weaponMountPoint == null) return;

        // Отфильтровываем предметы-оружие из слота экипировки, сохраняя порядок.
        List<WorldItem> weaponItems = new List<WorldItem>();
        foreach (var item in equipmentInventory.items)
            if (item != null && item.itemData != null && item.itemData.IsWeapon)
                weaponItems.Add(item);

        // Спавним недостающие (один раз на предмет, дальше переиспользуем инстанс). Боевая
        // позиция всегда локальный ноль/identity — она уже обеспечена weaponMountPoint.
        foreach (var item in weaponItems)
        {
            if (spawnedWeapons.ContainsKey(item)) continue;
            GameObject instance = Instantiate(item.itemData.weaponPrefab, weaponMountPoint);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);
            spawnedWeapons[item] = instance;
            weaponComponents[instance] = instance.GetComponent<Weapon>();
        }

        // Гасим вообще весь кэш: WeaponSystem.SetActiveWeapon ниже включит только нужный ствол,
        // но смотрит лишь на актуальный weapons[] — оружие, которое только что сняли и убрали
        // из массива, сама по себе не погаснет, если не выключить его явно здесь.
        foreach (var go in spawnedWeapons.Values)
            if (go != null) go.SetActive(false);

        weaponSystem.weapons = new GameObject[weaponItems.Count];
        for (int i = 0; i < weaponItems.Count; i++)
            weaponSystem.weapons[i] = spawnedWeapons[weaponItems[i]];

        // Оружие видно и может стрелять, только пока мы реально в режиме экипировки —
        // переключение на tidy-up (1) прячет его точно так же, как снятие через Q.
        bool armed = modeController == null || modeController.IsWeaponEquipped();
        WorldItem active = equipmentInventory.ActiveItem;
        int activeIndex = active != null ? weaponItems.IndexOf(active) : -1;
        GameObject newActiveGO = armed && activeIndex >= 0 ? weaponSystem.weapons[activeIndex] : null;

        if (newActiveGO != activeWeaponGO)
        {
            // Прежнее (если анимация не успела доехать) — доводим мгновенно, не бросаем на середине.
            if (drawingWeapon != null)
            {
                drawingWeapon.transform.localPosition = Vector3.zero;
                drawingWeapon.transform.localRotation = Quaternion.identity;
            }

            if (newActiveGO != null && tidyUpHolder != null && tidyUpHolder.HeldItemTransform != null)
            {
                // Старт анимации — там, где предмет визуально был в руке tidy-up перед экипировкой.
                Transform start = tidyUpHolder.HeldItemTransform;
                newActiveGO.transform.position = start.position;
                newActiveGO.transform.rotation = start.rotation;
                drawingWeapon = newActiveGO;
                drawVelocity = Vector3.zero;
            }
            else
            {
                drawingWeapon = null;
            }

            activeWeaponGO = newActiveGO;
        }

        if (armed && activeIndex >= 0)
        {
            if (weaponHolster != null && weaponHolster.IsHolstered)
                weaponHolster.Unholster();
            weaponSystem.enabled = true;
            weaponSystem.SetActiveWeapon(activeIndex);
        }
        else
        {
            // Явно выключаем WeaponSystem (не только прячем стволы) — иначе его собственный
            // Update() (клавиши 1-9 / колесо) может сам включить оружие обратно поверх нашей логики.
            if (weaponHolster != null) weaponHolster.Holster();
            else weaponSystem.enabled = false;
        }
    }
}
