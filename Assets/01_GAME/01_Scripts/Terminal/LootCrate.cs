using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Привезённый ящик с товаром. Вскрывается ломом, как любой Breakable; при вскрытии бросает таблицу
/// лута своего LootBoxData и высыпает предметы вокруг себя — дальше обычная уборка и расстановка.
/// Разбитые доски самого ящика (Breakable.spawnOnBreak) — тоже мусор, который нужно убрать.
/// </summary>
[RequireComponent(typeof(Breakable))]
public class LootCrate : MonoBehaviour
{
    [Header("Высыпание")]
    [Tooltip("Разброс выпавших предметов вокруг ящика.")]
    [Min(0f)] public float scatter = 0.5f;

    [Tooltip("Высота, с которой предметы падают.")]
    [Min(0f)] public float spawnHeight = 0.6f;

    [Tooltip("Через сколько секунд убирать разбитый ящик.")]
    [Min(0f)] public float destroyDelay = 1.5f;

    public LootBoxData Box { get; private set; }

    private SupplyService service;
    private Breakable breakable;

    public void Init(SupplyService owner, LootBoxData box)
    {
        service = owner;
        Box = box;
    }

    private void Awake()
    {
        breakable = GetComponent<Breakable>();
        breakable.OnBroken += HandleBroken;
    }

    private void OnDestroy()
    {
        if (breakable != null) breakable.OnBroken -= HandleBroken;
    }

    private void HandleBroken(Breakable _)
    {
        var spawned = new List<WorldItem>();
        if (Box != null)
        {
            Transform parent = service != null ? service.itemsContainer : null;
            foreach (var itemData in Box.Roll())
            {
                if (itemData == null || itemData.worldPrefab == null) continue;

                Vector2 offset = Random.insideUnitCircle * scatter;
                Vector3 position = transform.position + new Vector3(offset.x, spawnHeight, offset.y);
                var go = Instantiate(itemData.worldPrefab, position, Random.rotation, parent);
                var item = go.GetComponent<WorldItem>();
                if (item != null) spawned.Add(item);
            }
        }

        if (service != null) service.HandleCrateOpened(this, spawned);
        Destroy(gameObject, destroyDelay);
    }
}
