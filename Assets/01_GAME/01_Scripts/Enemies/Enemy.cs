using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Враг: машина состояний поверх NavMeshAgent.
///
/// Idle — стоит на месте («спящий» враг в комнате), Wandering — «живое» блуждание вокруг точки
/// появления. Из обоих враг переходит в Chasing, когда EnemyPerception замечает игрока (виден или
/// вплотную), когда в него попали (EnemyHealth.OnDamaged) или по внешнему Aggro() — через него будущие
/// события (орды, штурм базы) поднимают врагов. В досягаемости удара — Attacking (бьёт через
/// EnemyAttack). Потерял игрока дольше EnemyData.loseTargetTime или игрок умер — Returning домой,
/// там снова Idle/Wandering.
///
/// Где враг вообще может ходить, определяет только NavMesh (NavMeshSurface на объектах, по которым
/// ходят NPC); радиус EnemyData.wanderRadius лишь не даёт разбредаться от HomePosition.
/// Блуждание — цепочка коротких шагов с предпочтением направления «вперёд», иногда с остановкой и
/// осматриванием. Корпус поворачивается сам (agent.updateRotation = false).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyAttack))]
public class Enemy : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Wandering,
        Chasing,
        Attacking,
        Returning,
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

    private const float RepathInterval = 0.25f;
    // Выход из атаки чуть дальше входа — чтобы враг не дёргался на границе дистанции.
    private const float AttackExitFactor = 1.15f;
    private const float HomeArriveDistance = 0.6f;
    private const float FaceTargetSharpness = 10f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Данные")]
    [Tooltip("Данные врага (здоровье, скорость, блуждание, восприятие, атака). При спавне через " +
             "EnemySpawner перезаписываются из EnemyData точки спавна.")]
    public EnemyData data;

    [Header("Поведение")]
    [Tooltip("Спит на месте, пока не заметит игрока (враг в комнате), вместо блуждания. При спавне " +
             "перезаписывается флагом точки спавна.")]
    public bool startIdle;

    /// <summary>Текущее состояние врага.</summary>
    public EnemyState State { get; private set; } = EnemyState.Wandering;

    /// <summary>Точка, вокруг которой враг блуждает (там, где появился).</summary>
    public Vector3 HomePosition { get; private set; }

    /// <summary>Враг умер — публичный хук поверх EnemyHealth.OnDied (для будущих квестов/трекеров).</summary>
    public event Action<Enemy> OnDied;

    private NavMeshAgent agent;
    private EnemyHealth health;
    private EnemyPerception perception;
    private EnemyAttack attack;
    private NavMeshPath pathBuffer;
    private PlayerHealth target;
    private bool persistentAggro;
    private float repathTimer;

    private WanderPhase phase = WanderPhase.Starting;
    private float phaseTimer;
    private bool pauseAtEnd;
    private float stuckTimer;
    private float lookTimer;
    private float lookYaw;

    private float speedFactor = 1f;
    private float shambleSeed;

    private EnemyState RestState => startIdle ? EnemyState.Idle : EnemyState.Wandering;
    private bool TargetAlive => target != null && !target.IsDead;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        perception = GetComponent<EnemyPerception>();
        attack = GetComponent<EnemyAttack>();
        pathBuffer = new NavMeshPath();

        agent.updateRotation = false;
        agent.autoBraking = false;
        HomePosition = transform.position;
        shambleSeed = Random.value * 100f;
        lookYaw = transform.eulerAngles.y;
        State = RestState;

        // Враг, поставленный в сцену руками (не через спавнер), работает на данных своего префаба.
        ApplyData();
    }

    /// <summary>
    /// Настройка сразу после Instantiate (EnemySpawner). Awake к этому моменту уже отработал на данных
    /// префаба, поэтому данные точки спавна применяются здесь явно.
    /// </summary>
    public void Initialize(EnemyData enemyData, Vector3 home, PlayerHealth player, bool idle)
    {
        data = enemyData;
        HomePosition = home;
        target = player;
        startIdle = idle;
        State = RestState;
        ApplyData();
        health.Initialize(enemyData);
        if (agent.isOnNavMesh) agent.Warp(home);
    }

    private void Start()
    {
        // Враг, поставленный в сцену руками, ищет игрока сам.
        if (target == null) target = FindAnyObjectByType<PlayerHealth>();
        perception.Setup(data, target);
    }

    private void ApplyData()
    {
        if (data == null) return;

        speedFactor = 1f + Random.Range(-data.speedVariance, data.speedVariance);
        agent.speed = data.moveSpeed * speedFactor;
        agent.acceleration = data.acceleration;
        agent.stoppingDistance = data.stoppingDistance;
        phaseTimer = Random.Range(0f, data.startDelayMax);
        perception.Setup(data, target);
        ApplyTint();
    }

    /// <summary>Перекраска капсулы-заглушки в цвет типа врага (через PropertyBlock — материал не клонируется).</summary>
    private void ApplyTint()
    {
        var block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, data.tint);
            block.SetColor(ColorId, data.tint);
            r.SetPropertyBlock(block);
        }
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.OnDied -= HandleDied;
        health.OnDamaged -= HandleDamaged;
    }

    /// <summary>
    /// Принудительно натравить врага на игрока — для событий (враг в комнате, орда, штурм базы).
    /// persistent: не терять цель, даже если игрока долго не видно.
    /// </summary>
    public void Aggro(bool persistent = false)
    {
        if (State == EnemyState.Dead || !TargetAlive) return;
        persistentAggro |= persistent;
        if (State != EnemyState.Chasing && State != EnemyState.Attacking) StartChase();
    }

    private void Update()
    {
        if (State == EnemyState.Dead || data == null || !agent.isOnNavMesh) return;

        switch (State)
        {
            case EnemyState.Idle:
                if (perception.CanSenseTarget) StartChase();
                break;

            case EnemyState.Wandering:
                if (perception.CanSenseTarget) StartChase();
                else UpdateWander();
                break;

            case EnemyState.Chasing:
                UpdateChase();
                break;

            case EnemyState.Attacking:
                UpdateAttack();
                break;

            case EnemyState.Returning:
                if (perception.CanSenseTarget) StartChase();
                else UpdateReturn();
                break;
        }

        UpdateSpeed();
        UpdateRotation();
    }

    // ---------- Погоня и атака ----------

    private void StartChase()
    {
        if (!TargetAlive) return;

        State = EnemyState.Chasing;
        perception.Alert();
        agent.isStopped = false;
        agent.autoBraking = true;
        agent.stoppingDistance = data.attackRange * 0.8f;
        repathTimer = 0f;
    }

    private void UpdateChase()
    {
        if (!TargetAlive)
        {
            ReturnHome();
            return;
        }

        if (!persistentAggro && !perception.CanSenseTarget && perception.TimeSinceSensed > data.loseTargetTime)
        {
            ReturnHome();
            return;
        }

        if (FlatDistanceToTarget() <= data.attackRange)
        {
            State = EnemyState.Attacking;
            agent.isStopped = true;
            agent.ResetPath();
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer > 0f) return;
        repathTimer = RepathInterval;

        // Не видя игрока, идёт туда, где заметил его в последний раз; при persistent-агре знает, где он.
        Vector3 destination = perception.CanSenseTarget || persistentAggro
            ? target.transform.position
            : perception.LastKnownPosition;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private void UpdateAttack()
    {
        if (!TargetAlive)
        {
            ReturnHome();
            return;
        }

        if (attack.IsAttacking) return;

        if (FlatDistanceToTarget() > data.attackRange * AttackExitFactor)
        {
            StartChase();
            return;
        }

        if (attack.CanAttack) attack.BeginAttack(target, data);
    }

    private void ReturnHome()
    {
        State = EnemyState.Returning;
        persistentAggro = false;
        attack.Cancel();
        agent.isStopped = false;
        agent.autoBraking = true;
        agent.stoppingDistance = data.stoppingDistance;
        agent.SetDestination(HomePosition);
    }

    private void UpdateReturn()
    {
        if (agent.pathPending) return;
        if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, HomeArriveDistance)) return;

        State = RestState;
        if (State == EnemyState.Wandering)
        {
            BeginPause();
        }
        else
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private float FlatDistanceToTarget()
    {
        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        return toTarget.magnitude;
    }

    private void HandleDamaged(float amount)
    {
        Aggro();
    }

    // ---------- Блуждание ----------

    private void UpdateWander()
    {
        switch (phase)
        {
            case WanderPhase.Starting:
            case WanderPhase.Paused:
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f) PickNextStep();
                break;

            case WanderPhase.Moving:
                UpdateMoving();
                break;
        }
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

    // ---------- Скорость и поворот ----------

    /// <summary>Шаркающая походка: скорость медленно «плавает» вокруг базовой (шум Перлина).</summary>
    private void UpdateSpeed()
    {
        bool chasing = State == EnemyState.Chasing || State == EnemyState.Attacking;
        float baseSpeed = (chasing ? data.chaseSpeed : data.moveSpeed) * speedFactor;
        float noise = Mathf.PerlinNoise(Time.time * data.shambleFrequency, shambleSeed) * 2f - 1f;
        agent.speed = Mathf.Max(0.1f, baseSpeed * (1f + data.shambleAmplitude * noise));
    }

    private void UpdateRotation()
    {
        if (State == EnemyState.Idle) return;

        if (State == EnemyState.Wandering && phase == WanderPhase.Paused)
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
        bool engaged = (State == EnemyState.Chasing || State == EnemyState.Attacking) && TargetAlive;

        // В атаке и когда почти стоит в погоне — разворачивается лицом к игроку.
        if (engaged && (State == EnemyState.Attacking || velocity.sqrMagnitude < 0.01f))
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return;
            Quaternion face = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-FaceTargetSharpness * Time.deltaTime));
            return;
        }

        if (velocity.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(velocity);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-data.turnSharpness * Time.deltaTime));
    }

    private void HandleDied(EnemyHealth h)
    {
        State = EnemyState.Dead;
        attack.Cancel();
        // isStopped бросает исключение, если агент не на сетке, — а оно прервало бы Die() до Destroy
        // и до респавна, оставив в мире «мёртвого» врага.
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        OnDied?.Invoke(this);
    }
}
