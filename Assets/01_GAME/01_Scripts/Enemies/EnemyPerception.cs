using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Чувства врага: замечает игрока, если тот вплотную (proximityRadius — с любой стороны, даже
/// спиной), или в радиусе и угле обзора с прямой видимостью (луч от глаз до головы игрока не
/// упирается в стену). Помнит, где и когда видел игрока в последний раз, — по этому Enemy решает,
/// продолжать погоню или вернуться домой. Сам ничего не решает, только сообщает.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPerception : MonoBehaviour
{
    [Header("Проверка")]
    [Tooltip("Как часто (с) проверять игрока. Чаще — отзывчивее, но дороже при толпе врагов.")]
    [Min(0.02f)] public float checkInterval = 0.2f;

    [Tooltip("Что заслоняет обзор. Триггеры не учитываются никогда.")]
    public LayerMask obstacleMask = ~0;

    /// <summary>Игрок сейчас замечен (виден или вплотную).</summary>
    public bool CanSenseTarget { get; private set; }

    /// <summary>Где игрок был замечен в последний раз.</summary>
    public Vector3 LastKnownPosition { get; private set; }

    /// <summary>Сколько секунд прошло с момента, когда игрок был замечен в последний раз.</summary>
    public float TimeSinceSensed => Time.time - lastSensedTime;

    private NavMeshAgent agent;
    private EnemyData data;
    private PlayerHealth target;
    private float checkTimer;
    private float lastSensedTime = float.NegativeInfinity;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Разносим проверки разных врагов по кадрам.
        checkTimer = Random.Range(0f, checkInterval);
    }

    /// <summary>Задать параметры и цель (вызывает Enemy).</summary>
    public void Setup(EnemyData enemyData, PlayerHealth player)
    {
        data = enemyData;
        target = player;
    }

    /// <summary>Считать игрока только что замеченным — например, враг получил пулю, не видя стрелка.</summary>
    public void Alert()
    {
        if (target == null) return;
        lastSensedTime = Time.time;
        LastKnownPosition = target.transform.position;
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkInterval;

        CanSenseTarget = Sense();
        if (CanSenseTarget) Alert();
    }

    private bool Sense()
    {
        if (data == null || target == null || target.IsDead) return false;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= data.proximityRadius) return true;
        if (distance > data.sightRadius) return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (Vector3.Angle(forward, toTarget) > data.sightAngle * 0.5f) return false;

        // Глаза считаем от основания агента: pivot капсулы стоит в её центре (baseOffset).
        Vector3 eye = transform.position + Vector3.up * (data.eyeHeight - agent.baseOffset);
        if (!Physics.Linecast(eye, target.AimPosition, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.transform.IsChildOf(target.transform);
    }

    private void OnDrawGizmosSelected()
    {
        EnemyData d = data;
        if (d == null && TryGetComponent(out Enemy enemy)) d = enemy.data;
        if (d == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, d.proximityRadius);

        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0f, -d.sightAngle * 0.5f, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, d.sightAngle * 0.5f, 0f) * forward;
        Gizmos.color = CanSenseTarget ? Color.red : Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + left * d.sightRadius);
        Gizmos.DrawLine(transform.position, transform.position + right * d.sightRadius);
    }
}
