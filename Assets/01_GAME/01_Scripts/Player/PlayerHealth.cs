using System;
using UnityEngine;

/// <summary>
/// Здоровье игрока в стиле Call of Duty: полоски нет, урон виден по экрану (DamageVignetteUI),
/// здоровье само восстанавливается через regenDelay после последнего удара.
///
/// Урон — только через TakeDamage. Метода ChangeHealth здесь намеренно нет: Easy Weapons рассылает
/// его через SendMessageUpwards, и игрок получал бы урон от собственных взрывов и снарядов.
/// Заменяет на игроке Health из Easy Weapons, который при смерти уничтожал весь риг игрока.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — значения ниже перекрываются из GameConfig при старте.")]
    public GameConfig config;

    [Header("Здоровье")]
    [Tooltip("Максимальное здоровье.")]
    [Min(1f)] public float maxHealth = 100f;

    [Tooltip("Через сколько секунд после последнего урона начинается восстановление.")]
    [Min(0f)] public float regenDelay = 4f;

    [Tooltip("Скорость восстановления, единиц здоровья в секунду.")]
    [Min(0f)] public float regenRate = 25f;

    [Header("Ссылки")]
    [Tooltip("Блокировщик ввода: при смерти забирает у игрока движение, обзор и стрельбу.")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Точка, в которую целятся и смотрят враги (голова/камера). Пусто — корень игрока + 1.6 м.")]
    public Transform aimPoint;

    /// <summary>Текущее здоровье.</summary>
    public float CurrentHealth { get; private set; }

    /// <summary>Здоровье в долях от максимума, 0..1.</summary>
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    /// <summary>Игрок мёртв.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Точка прицеливания врагов в мировых координатах.</summary>
    public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * 1.6f;

    /// <summary>Игрок получил урон: (величина урона).</summary>
    public event Action<float> OnDamaged;

    /// <summary>Игрок умер.</summary>
    public event Action OnDied;

    private float lastDamageTime = float.NegativeInfinity;

    private void Awake()
    {
        ApplyConfig();
        CurrentHealth = maxHealth;
    }

    private void ApplyConfig()
    {
        if (config == null) return;
        PlayerHealthSettings s = config.playerHealth;
        maxHealth = s.maxHealth;
        regenDelay = s.regenDelay;
        regenRate = s.regenRate;
    }

    private void Update()
    {
        if (IsDead || CurrentHealth >= maxHealth) return;
        if (Time.time - lastDamageTime < regenDelay) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + regenRate * Time.deltaTime);
    }

    /// <summary>Нанести игроку урон (положительное число).</summary>
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        lastDamageTime = Time.time;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnDamaged?.Invoke(amount);

        if (CurrentHealth <= 0f) Die();
    }

    private void Die()
    {
        IsDead = true;
        if (inputBlocker != null) inputBlocker.Acquire(this);
        OnDied?.Invoke();
    }
}
