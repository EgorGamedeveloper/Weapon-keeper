using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// «Видение» предмета в руке — будущая способность игрока (пока включается клавишей). На время
/// действия (duration) подсвечиваются, в том числе сквозь стены:
/// - голограммы всех мест на сцене, куда можно поставить предмет из руки (их показывает
///   PlayerItemInteraction.UpdatePlacementHints в режиме GhostMode.Vision по списку SceneSlots);
/// - все предметы того же типа, лежащие в мире или стоящие на полках (рисует этот компонент).
/// В последние fadeDuration секунд подсветка плавно гаснет (Intensity 1 → 0). Места рядом с игроком
/// видение не трогает — у них обычная подсказка обычной яркости.
///
/// Повторное нажатие во время действия игнорируется; перезарядка (cooldown) — задел под способность.
/// Индикатора на экране пока нет: состояние — IsActive/Remaining/Intensity и события.
/// </summary>
public class PlacementVision : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Tidy-up инвентарь: подсвечивается то, что подходит к его активному предмету.")]
    public InventorySystem inventory;

    [Tooltip("Режим инвентаря: с оружием в руках видение ничего не подсвечивает.")]
    public PlayerInventoryModeController modeController;

    [Header("Конфиг")]
    [Tooltip("Если задан — клавиша, длительность, затухание и перезарядка берутся из GameConfig при старте.")]
    public GameConfig config;

    [Header("Способность")]
    [Tooltip("Клавиша активации.")]
    public KeyCode activationKey = KeyCode.V;

    [Tooltip("Сколько секунд действует видение.")]
    [Min(0.1f)] public float duration = 15f;

    [Tooltip("За сколько секунд до конца подсветка начинает плавно гаснуть.")]
    [Min(0f)] public float fadeDuration = 5f;

    [Tooltip("Перезарядка после окончания, секунды. 0 — можно включить сразу снова.")]
    [Min(0f)] public float cooldown;

    /// <summary>Видение сейчас действует.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Сколько секунд видения осталось.</summary>
    public float Remaining { get; private set; }

    /// <summary>Яркость подсветки 0..1: 1, а в последние fadeDuration секунд — линейно до 0.</summary>
    public float Intensity { get; private set; }

    /// <summary>Сколько секунд перезарядки осталось (0 — можно включать).</summary>
    public float CooldownRemaining { get; private set; }

    /// <summary>Все места установки сцены (собираются при активации) — их перебирает
    /// PlayerItemInteraction, чтобы показать голограммы дальних мест.</summary>
    public IReadOnlyList<IPlaceableSlot> SceneSlots => sceneSlots;

    /// <summary>Видение включилось — хук для будущего HUD/звука.</summary>
    public event Action OnActivated;

    /// <summary>Видение закончилось.</summary>
    public event Action OnExpired;

    // Новые предметы (лут из ящика) и смена состояния появляются редко — пересобирать список раз в
    // секунду достаточно.
    private const float ItemRebuildInterval = 1f;

    private readonly List<IPlaceableSlot> sceneSlots = new List<IPlaceableSlot>();
    private readonly List<WorldItem> matchingItems = new List<WorldItem>();
    private ItemData matchingFor;
    private float rebuildTimer;

    private void Awake()
    {
        if (config == null) return;

        activationKey = config.input.visionKey;
        duration = config.vision.duration;
        fadeDuration = config.vision.fadeDuration;
        cooldown = config.vision.cooldown;
    }

    private void OnDisable()
    {
        if (IsActive) Expire();
    }

    private void Update()
    {
        if (Input.GetKeyDown(activationKey)) Activate();

        if (!IsActive)
        {
            if (CooldownRemaining > 0f) CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            return;
        }

        Remaining -= Time.deltaTime;
        if (Remaining <= 0f)
        {
            Expire();
            return;
        }

        Intensity = fadeDuration > 0f && Remaining < fadeDuration ? Remaining / fadeDuration : 1f;
        GhostPreviewUtility.SetVisionIntensity(Intensity);
        DrawMatchingItems();
    }

    /// <summary>Включить видение. Во время действия и перезарядки ничего не делает (возвращает false).</summary>
    public bool Activate()
    {
        if (IsActive || CooldownRemaining > 0f) return false;

        IsActive = true;
        Remaining = duration;
        Intensity = 1f;
        GhostPreviewUtility.SetVisionIntensity(1f);

        sceneSlots.Clear();
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            if (behaviour is IPlaceableSlot slot) sceneSlots.Add(slot);

        matchingFor = null;
        OnActivated?.Invoke();
        return true;
    }

    private void Expire()
    {
        IsActive = false;
        Remaining = 0f;
        Intensity = 0f;
        GhostPreviewUtility.SetVisionIntensity(0f);
        sceneSlots.Clear();
        matchingItems.Clear();
        matchingFor = null;
        CooldownRemaining = cooldown;
        OnExpired?.Invoke();
    }

    /// <summary>Предмет, под который работает видение: активный предмет tidy-up, если в руках не оружие.</summary>
    private ItemData ActiveItem
    {
        get
        {
            if (inventory == null) return null;
            if (modeController != null && modeController.IsWeaponEquipped()) return null;
            return inventory.GetActiveItem();
        }
    }

    /// <summary>Однотипные предметы в мире и на полках — тем же материалом видения, что и дальние
    /// голограммы мест (сквозь стены, гаснет вместе с видением). Предметы в руке/инвентаре не рисуются.</summary>
    private void DrawMatchingItems()
    {
        ItemData active = ActiveItem;
        if (active == null) return;

        rebuildTimer -= Time.deltaTime;
        if (active != matchingFor || rebuildTimer <= 0f) RebuildMatchingItems(active);

        Material material = GhostPreviewUtility.GetVisionMaterial();
        if (material == null) return;

        var renderParams = new RenderParams(material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        foreach (var item in matchingItems)
        {
            if (item == null) continue;
            if (item.State != WorldItem.ItemState.InWorld && item.State != WorldItem.ItemState.PlacedOnShelf) continue;

            renderParams.layer = item.gameObject.layer;
            item.DrawOverlay(renderParams);
        }
    }

    private void RebuildMatchingItems(ItemData active)
    {
        matchingItems.Clear();
        foreach (var item in FindObjectsByType<WorldItem>(FindObjectsInactive.Exclude))
            if (item.itemData == active) matchingItems.Add(item);

        matchingFor = active;
        rebuildTimer = ItemRebuildInterval;
    }
}
