using System;
using UnityEngine;

/// <summary>
/// Здоровье и смерть врага. ChangeHealth(float) — метод с сигнатурой, которую Easy Weapons вызывает
/// через SendMessageUpwards("ChangeHealth", -damage, ...) из Weapon/Projectile/Explosion при попадании
/// в коллайдер этого объекта или его дочерних объектов. Само по себе не связано ни с IPlaceableSlot,
/// ни с Breakable/CleanableStain — это отдельный контракт под стрельбу Easy Weapons, а не под
/// инструменты игрока.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [Tooltip("Данные врага. Если заданы, maxHealth берётся отсюда, иначе — из поля ниже. При спавне через " +
             "EnemySpawner перезаписываются данными точки спавна (Initialize).")]
    public EnemyData data;

    [Tooltip("Максимальное здоровье, если data не задана.")]
    [Min(1f)] public float maxHealth = 100f;

    /// <summary>Текущее здоровье.</summary>
    public float CurrentHealth { get; private set; }

    /// <summary>Враг умер.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Враг умер — хук для Enemy и будущей интеграции с квестами/трекерами.</summary>
    public event Action<EnemyHealth> OnDied;

    private void Awake()
    {
        if (data != null) maxHealth = data.maxHealth;
        CurrentHealth = maxHealth;
    }

    /// <summary>Применить данные точки спавна (вызывается из Enemy.Initialize сразу после Instantiate,
    /// до первого попадания).</summary>
    public void Initialize(EnemyData enemyData)
    {
        if (enemyData == null) return;
        data = enemyData;
        maxHealth = enemyData.maxHealth;
        CurrentHealth = maxHealth;
    }

    // Коллайдер намеренно остаётся физическим (не триггер) — Easy Weapons определяет цель через
    // Physics.Raycast и Collision, которые триггеры не видят. Это отличает EnemyHealth от зон вроде
    // DeliveryZone, где Reset() наоборот включает isTrigger.

    /// <summary>Вызывается Easy Weapons через SendMessageUpwards при попадании. amount отрицательный при уроне.</summary>
    public void ChangeHealth(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0f, maxHealth);
        if (CurrentHealth <= 0f) Die();
    }

    /// <summary>Смерть врага: отключает коллайдеры, оповещает подписчиков и уничтожает объект.</summary>
    public void Die()
    {
        if (IsDead) return;
        IsDead = true;

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
