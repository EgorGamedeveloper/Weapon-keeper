using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Враг: «живое» блуждание по NavMesh вокруг точки появления + реакция на смерть. Пока нет ни атаки на
/// игрока, ни атаки на базу — заготовка-капсула, которая ходит и может умереть от стрельбы игрока
/// (урон принимает EnemyHealth).
///
/// Где враг вообще может ходить, определяет только NavMesh: NavMeshSurface ставится на объекты, по
/// которым ходят NPC (пол улицы, крыша), и запекается. Отдельной зоны-коллайдера нет — радиус
/// EnemyData.wanderRadius лишь не даёт разбредаться от HomePosition по всей карте.
///
/// Блуждание — цепочка коротких шагов с предпочтением направления «вперёд» (без челночных
/// разворотов); большинство шагов идут без остановки, иногда враг останавливается и осматривается.
/// Корпус поворачивается сам (agent.updateRotation = false) — плавно, вслед за скоростью агента.
/// EnemyState — задел под будущее дерево поведения (Chasing/Attacking).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class Enemy : MonoBehaviour
{
    public enum EnemyState
    {
        Wandering,
        Dead,
    }

    private enum WanderPhase { Starting, Moving, Paused }

    // Шаг без остановки считается пройденным чуть раньше точки — следующий путь подхватывается
    // на ходу, без торможения агента.
    private const float FlowArriveDistance = 0.8f;
    private const float StepSampleRadius = 1.5f;
    private const int StepAttempts = 10;
    private const float StuckSpeed = 0.05f;
    private const float StuckTime = 1.5f;

    [Header("Данные")]
    [Tooltip("Данные врага (здоровье, скорость, параметры блуждания). При спавне через EnemySpawner " +
             "перезаписываются из EnemyData точки спавна.")]
    public EnemyData data;

    /// <summary>Текущее состояние врага.</summary>
    public EnemyState State { get; private set; } = EnemyState.Wandering;

    /// <summary>Точка, вокруг которой враг блуждает (там, где появился).</summary>
    public Vector3 HomePosition { get; private set; }

    /// <summary>Враг умер — публичный хук поверх EnemyHealth.OnDied (для будущих квестов/трекеров).</summary>
    public event Action<Enemy> OnDied;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private NavMeshPath pathBuffer;

    private WanderPhase phase = WanderPhase.Starting;
    private float phaseTimer;
    private bool pauseAtEnd;
    private float stuckTimer;
    private float lookTimer;
    private float lookYaw;

    private float baseSpeed;
    private float shambleSeed;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        pathBuffer = new NavMeshPath();

        agent.updateRotation = false;
        agent.autoBraking = false;
        HomePosition = transform.position;
        shambleSeed = Random.value * 100f;
        lookYaw = transform.eulerAngles.y;

        // Враг, поставленный в сцену руками (не через спавнер), работает на данных своего префаба.
        ApplyData();
    }

    /// <summary>
    /// Настройка сразу после Instantiate (EnemySpawner). Awake к этому моменту уже отработал на данных
    /// префаба, поэтому данные точки спавна применяются здесь явно — раньше они молча игнорировались.
    /// </summary>
    public void Initialize(EnemyData enemyData, Vector3 home)
    {
        data = enemyData;
        HomePosition = home;
        ApplyData();
        health.Initialize(enemyData);
        if (agent.isOnNavMesh) agent.Warp(home);
    }

    private void ApplyData()
    {
        if (data == null) return;

        baseSpeed = data.moveSpeed * (1f + Random.Range(-data.speedVariance, data.speedVariance));
        agent.speed = baseSpeed;
        agent.acceleration = data.acceleration;
        agent.stoppingDistance = data.stoppingDistance;
        phaseTimer = Random.Range(0f, data.startDelayMax);
    }

    private void OnEnable()
    {
        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDied -= HandleDied;
    }

    private void Update()
    {
        if (State != EnemyState.Wandering || data == null || !agent.isOnNavMesh) return;

        switch (phase)
        {
            case WanderPhase.Starting:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f) PickNextStep();
                break;

            case WanderPhase.Moving:
                UpdateMoving();
                break;

            case WanderPhase.Paused:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f) PickNextStep();
                break;
        }

        UpdateSpeed();
        UpdateRotation();
    }

    private void UpdateMoving()
    {
        if (agent.pathPending) return;

        float arriveDistance = pauseAtEnd ? agent.stoppingDistance : Mathf.Max(agent.stoppingDistance, FlowArriveDistance);
        if (agent.remainingDistance <= arriveDistance)
        {
            if (pauseAtEnd) BeginPause();
            else PickNextStep();
            return;
        }

        // Упёрся (другой агент, край сетки) — не стоим столбом, выбираем другой шаг.
        stuckTimer = agent.velocity.sqrMagnitude < StuckSpeed * StuckSpeed ? stuckTimer + Time.deltaTime : 0f;
        if (stuckTimer > StuckTime) PickNextStep();
    }

    /// <summary>Следующий шаг: точка на расстоянии stepDistanceRange в пределах ±forwardBiasAngle от
    /// взгляда, не дальше wanderRadius от дома, на NavMesh и достижимая целым путём (точка на другом,
    /// несвязанном куске сетки не подходит).</summary>
    private void PickNextStep()
    {
        stuckTimer = 0f;
        // NavMeshPath не сериализуется: после перекомпиляции скриптов прямо в Play Mode поле обнуляется.
        if (pathBuffer == null) pathBuffer = new NavMeshPath();
        Vector3 position = transform.position;
        Vector3 toHome = HomePosition - position;
        toHome.y = 0f;

        for (int attempt = 0; attempt < StepAttempts; attempt++)
        {
            Vector3 direction;
            if (toHome.magnitude > data.wanderRadius * 0.8f)
            {
                // У границы радиуса — поворачиваем в сторону дома, тоже с небольшим разбросом.
                direction = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f) * toHome.normalized;
            }
            else
            {
                // Вторая половина попыток — любое направление: выход из тупика или угла.
                float spread = attempt < StepAttempts / 2 ? data.forwardBiasAngle : 180f;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                direction = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * forward.normalized;
            }

            float distance = Random.Range(data.stepDistanceRange.x, data.stepDistanceRange.y);
            Vector3 candidate = position + direction * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, StepSampleRadius, agent.areaMask)) continue;

            Vector3 fromHome = hit.position - HomePosition;
            fromHome.y = 0f;
            if (fromHome.magnitude > data.wanderRadius) continue;

            if (!NavMesh.CalculatePath(position, hit.position, agent.areaMask, pathBuffer)
                || pathBuffer.status != NavMeshPathStatus.PathComplete)
                continue;

            pauseAtEnd = Random.value < data.pauseChance;
            agent.autoBraking = pauseAtEnd;
            agent.isStopped = false;
            agent.SetPath(pathBuffer);
            phase = WanderPhase.Moving;
            return;
        }

        // Подходящей точки не нашлось (например, сетка вокруг слишком мала) — постоять и попробовать снова.
        BeginPause();
    }

    private void BeginPause()
    {
        phase = WanderPhase.Paused;
        phaseTimer = Random.Range(data.wanderPauseRange.x, data.wanderPauseRange.y);
        agent.isStopped = true;
        agent.ResetPath();
        lookTimer = 0f;
        lookYaw = transform.eulerAngles.y;
    }

    /// <summary>Шаркающая походка: скорость медленно «плавает» вокруг базовой (шум Перлина).</summary>
    private void UpdateSpeed()
    {
        float noise = Mathf.PerlinNoise(Time.time * data.shambleFrequency, shambleSeed) * 2f - 1f;
        agent.speed = Mathf.Max(0.1f, baseSpeed * (1f + data.shambleAmplitude * noise));
    }

    private void UpdateRotation()
    {
        if (phase == WanderPhase.Paused)
        {
            // Осматривается: время от времени выбирает новое направление и медленно к нему поворачивается.
            lookTimer -= Time.deltaTime;
            if (lookTimer <= 0f)
            {
                lookTimer = Random.Range(data.lookAroundInterval.x, data.lookAroundInterval.y);
                lookYaw = transform.eulerAngles.y + Random.Range(30f, 110f) * (Random.value < 0.5f ? -1f : 1f);
            }

            Quaternion look = Quaternion.Euler(0f, lookYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, data.lookAroundTurnSpeed * Time.deltaTime);
            return;
        }

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(velocity);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-data.turnSharpness * Time.deltaTime));
    }

    private void HandleDied(EnemyHealth h)
    {
        State = EnemyState.Dead;
        // isStopped бросает исключение, если агент не на сетке, — а оно прервало бы Die() до Destroy
        // и до респавна, оставив в мире «мёртвого» врага.
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        OnDied?.Invoke(this);
    }
}
