using System;
using UnityEngine;

/// <summary>
/// Удар врага в ближнем бою: замах (attackWindup) → если игрок всё ещё в досягаемости, наносит урон →
/// пауза (attackCooldown). Когда бить, решает Enemy; здесь только тайминги и нанесение урона.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    // Небольшой запас к attackRange на момент попадания: игрок, сделавший шаг назад во время замаха,
    // уходит от удара, а стоящий на границе дистанции — нет.
    private const float HitRangeTolerance = 0.3f;

    /// <summary>Враг начал замах — точка для будущей анимации/звука удара.</summary>
    public event Action OnAttackStarted;

    /// <summary>Идёт замах.</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Можно начинать новый удар (не в замахе и не на перезарядке).</summary>
    public bool CanAttack => !IsAttacking && Time.time >= cooldownEndTime;

    private EnemyData data;
    private PlayerHealth target;
    private float hitTime;
    private float cooldownEndTime;

    /// <summary>Начать удар по цели.</summary>
    public void BeginAttack(PlayerHealth player, EnemyData enemyData)
    {
        if (!CanAttack || player == null || enemyData == null) return;

        target = player;
        data = enemyData;
        IsAttacking = true;
        hitTime = Time.time + data.attackWindup;
        OnAttackStarted?.Invoke();
    }

    /// <summary>Прервать замах без урона (цель пропала, враг умер).</summary>
    public void Cancel()
    {
        IsAttacking = false;
        target = null;
    }

    private void Update()
    {
        if (!IsAttacking || Time.time < hitTime) return;

        IsAttacking = false;
        cooldownEndTime = Time.time + data.attackCooldown;

        if (target == null || target.IsDead) return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude <= data.attackRange + HitRangeTolerance)
            target.TakeDamage(data.attackDamage);

        target = null;
    }
}
