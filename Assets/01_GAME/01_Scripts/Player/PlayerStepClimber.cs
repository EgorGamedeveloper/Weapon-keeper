using UnityEngine;

/// <summary>
/// Позволяет персонажу подниматься по ступеням обычным шагом, без прыжка.
///
/// Зачем нужен. Передвижение игрока делает сторонний FirstPersonCharacter (Easy Weapons) —
/// это связка Rigidbody + CapsuleCollider, а не CharacterController, поэтому у него нет
/// ничего похожего на stepOffset. Хуже того, каждый FixedUpdate он принудительно обнуляет
/// вертикальную скорость, пока персонаж на земле, и «приклеивает» его к точке под ногами
/// (groundStickyEffect). Из-за этого любой подъём, который физика успевает дать капсуле
/// на ступени, тут же уничтожается — взойти на ступень нельзя в принципе, спасает только
/// прыжок (он добавляет скорость до обнуления).
///
/// Как работает. Компонент выполняется ПОСЛЕ FirstPersonCharacter (DefaultExecutionOrder),
/// поэтому его правка позиции — последняя за кадр, и её уже никто не отменяет.
/// Если персонаж стоит на земле, движется и упирается ногами во что-то крутое, компонент
/// подбирает МИНИМАЛЬНУЮ высоту подъёма, при которой капсула (а) никуда не проваливается
/// и (б) может пройти вперёд ещё forwardClearance метров. Найдена такая высота — Rigidbody
/// поднимается ровно на неё. Не найдена (стена, высокий ящик) — персонаж остаётся на месте.
///
/// Почему подбор высоты, а не «поднять ровно на высоту ступени». Радиус капсулы 0.5 м больше
/// глубины проступи главной лестницы сцены (0.29 м при подъёме 0.30 м, уклон 46°), поэтому
/// капсула физически не встаёт на проступь — она катится по кромкам ступеней. Подъём ровно
/// на одну ступень в такой геометрии капсулу не освобождает: следующая кромка сразу снова
/// в неё упирается, и персонаж «дрожит» на месте. Подбор минимальной высоты, открывающей
/// проход вперёд, одинаково хорошо работает и на пологих ступенях (0.21 м), и на крутых.
///
/// Почему нужно удержание высоты (climbHold). FirstPersonCharacter каждый кадр тянет игрока
/// вниз к точке под ногами со скоростью groundStickyEffect (5 м/с — это 0.1 м за физический
/// кадр), то есть больше типичного подъёма. Без удержания персонаж поднимался бы и тут же
/// падал обратно, дрожа на месте. Поэтому после успешного подъёма компонент несколько кадров
/// удерживает достигнутую высоту ступней — пока персонаж не встанет на новую опору,
/// не остановится, не развернётся или не истечёт climbHoldDuration.
///
/// Решение работает для всех ступеней сцены сразу — не требует невидимых пандусов
/// (которые ловили бы луч взаимодействия и пули) и не требует правок стороннего кода.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerStepClimber : MonoBehaviour
{
    [Header("Шаг по ступеням")]
    [Tooltip("Максимальная высота, на которую персонаж может подняться за один физический кадр (м). Ступени сцены — 0.21 м и 0.30 м, но на крутой лестнице капсуле нужен запас.")]
    public float maxStepHeight = 0.45f;

    [Tooltip("Шаг перебора высоты подъёма (м). Меньше — точнее и плавнее, но дороже по физическим запросам.")]
    public float stepHeightResolution = 0.05f;

    [Tooltip("Насколько свободно капсула должна проходить вперёд на новой высоте, чтобы подъём считался осмысленным (м).")]
    public float forwardClearance = 0.15f;

    [Tooltip("Запас между капсулой и геометрией при проверке высоты (м). Без запаса подбирается подъём, оставляющий сантиметр зазора: формально свободно, а на практике капсула цепляется за кромку и стоит на месте.")]
    public float clearanceSkin = 0.04f;

    [Tooltip("Насколько дальше радиуса капсулы прощупывается препятствие перед ступнями (м).")]
    public float forwardProbe = 0.08f;

    [Tooltip("Высота луча-щупа над нижней точкой капсулы (м).")]
    public float probeFootOffset = 0.03f;

    [Tooltip("Минимальная горизонтальная скорость, при которой включается подъём (м/с).")]
    public float minMoveSpeed = 0.3f;

    [Tooltip("Максимальный угол поверхности, которую физика проходит сама (градусы). Всё более крутое считается ступенью или стеной.")]
    public float maxWalkableAngle = 55f;

    [Tooltip("Насколько ниже капсулы ищется опора при проверке «стоит на земле» (м).")]
    public float groundCheckDistance = 0.25f;

    [Tooltip("Вертикальная скорость, выше которой подъём отключается — персонаж прыгает или его подбросило (м/с).")]
    public float maxVerticalSpeed = 1f;

    [Header("Удержание набранной высоты")]
    [Tooltip("Сколько секунд после подъёма удерживать достигнутую высоту ступней, не давая FirstPersonCharacter утянуть персонажа обратно вниз.")]
    public float climbHoldDuration = 0.35f;

    [Tooltip("Насколько близко опора должна подойти к удерживаемой высоте, чтобы считать подъём завершённым (м).")]
    public float climbHoldArriveTolerance = 0.06f;

    [Header("Слои")]
    [Tooltip("Слои геометрии, по которой ходит игрок. Слой самого игрока (Player) обязательно исключить.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Не вставать шагом на объекты с обычным (не кинематическим) Rigidbody — ящики, выброшенные предметы.")]
    public bool ignoreDynamicBodies = true;

    [Header("Отладка")]
    [Tooltip("Рисовать лучи-щупы в Scene View.")]
    public bool drawDebugRays = false;

    /// <summary>Высота последнего выполненного подъёма, в метрах. Нужна для отладки и тестов.</summary>
    public float LastStepHeight { get; private set; }

    /// <summary>Сколько раз компонент поднимал персонажа. Нужно для отладки и тестов.</summary>
    public int StepUpCount { get; private set; }

    private Rigidbody body;
    private CapsuleCollider capsule;

    /// <summary>Высота ступней, которую компонент удерживает после подъёма.</summary>
    private float climbHoldFeetY;

    /// <summary>Сколько ещё секунд удерживать высоту.</summary>
    private float climbHoldTimer;

    /// <summary>Направление, в котором начинался подъём — при развороте удержание снимается.</summary>
    private Vector3 climbHoldDirection;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
    }

    private void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        bool moving = horizontal.sqrMagnitude >= minMoveSpeed * minMoveSpeed;
        Vector3 moveDirection = moving ? horizontal.normalized : Vector3.zero;

        float radius = capsule.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float halfHeight = Mathf.Max(radius, capsule.height * 0.5f * Mathf.Abs(transform.lossyScale.y));

        MaintainClimbHold(moving, moveDirection, radius, halfHeight);

        // В прыжке и при подбрасывании подъём не нужен.
        if (!moving || velocity.y > maxVerticalSpeed)
        {
            return;
        }

        TryStepUp(moveDirection, radius, halfHeight);
    }

    /// <summary>
    /// Удерживает высоту, набранную при подъёме, пока персонаж не встанет на новую опору.
    /// Без этого ground-stick стороннего контроллера стягивает персонажа обратно вниз
    /// быстрее, чем компонент успевает поднимать.
    /// </summary>
    private void MaintainClimbHold(bool moving, Vector3 moveDirection, float radius, float halfHeight)
    {
        if (climbHoldTimer <= 0f)
        {
            return;
        }

        climbHoldTimer -= Time.fixedDeltaTime;

        // Остановился или развернулся — пусть спокойно опускается на опору.
        if (!moving || Vector3.Dot(moveDirection, climbHoldDirection) < 0f)
        {
            climbHoldTimer = 0f;
            return;
        }

        Vector3 center = transform.TransformPoint(capsule.center);
        float feetY = center.y - halfHeight;

        // Опора уже подошла к нужной высоте — подъём завершён.
        RaycastHit groundHit;
        if (CastGround(center, radius, halfHeight, out groundHit) && groundHit.point.y >= climbHoldFeetY - climbHoldArriveTolerance)
        {
            climbHoldTimer = 0f;
            return;
        }

        if (feetY < climbHoldFeetY)
        {
            body.position += Vector3.up * (climbHoldFeetY - feetY);
        }
    }

    /// <summary>
    /// Если персонаж упёрся в ступень — поднимает Rigidbody на минимальную высоту,
    /// при которой снова открывается проход вперёд.
    /// </summary>
    private void TryStepUp(Vector3 moveDirection, float radius, float halfHeight)
    {
        Vector3 center = transform.TransformPoint(capsule.center);
        float feetY = center.y - halfHeight;

        RaycastHit groundHit;
        if (!CastGround(center, radius, halfHeight, out groundHit))
        {
            return;
        }

        // 1. Ноги должны во что-то упираться, иначе подниматься незачем.
        Vector3 lowOrigin = new Vector3(center.x, feetY + probeFootOffset, center.z);
        float lowDistance = radius + forwardProbe;
        RaycastHit obstacle;
        bool blocked = Physics.Raycast(lowOrigin, moveDirection, out obstacle, lowDistance, groundLayers, QueryTriggerInteraction.Ignore);

        if (drawDebugRays)
        {
            Debug.DrawRay(lowOrigin, moveDirection * lowDistance, blocked ? Color.red : Color.green);
        }

        if (!blocked || !IsAllowedSurface(obstacle.collider))
        {
            return;
        }

        // Пологий склон физика проходит сама — это не ступень.
        if (Vector3.Angle(obstacle.normal, Vector3.up) <= maxWalkableAngle)
        {
            return;
        }

        // 2. Ищем минимальную высоту, с которой капсула снова может идти вперёд.
        float resolution = Mathf.Max(0.01f, stepHeightResolution);
        int stepCount = Mathf.CeilToInt(maxStepHeight / resolution);

        for (int i = 1; i <= stepCount; i++)
        {
            float lift = Mathf.Min(maxStepHeight, resolution * i);
            Vector3 liftedCenter = center + Vector3.up * lift;

            // На новой высоте капсула не должна ни во что попадать...
            if (OverlapsAt(liftedCenter, radius, halfHeight))
            {
                continue;
            }

            // ...и должна свободно проходить вперёд.
            if (BlockedAhead(liftedCenter, radius, halfHeight, moveDirection))
            {
                continue;
            }

            body.position += Vector3.up * lift;

            climbHoldFeetY = feetY + lift;
            climbHoldDirection = moveDirection;
            climbHoldTimer = climbHoldDuration;

            LastStepHeight = lift;
            StepUpCount++;
            return;
        }
    }

    /// <summary>
    /// Ищет опору под капсулой. Сфера чуть уже капсулы, чтобы уверенно ловить кромку
    /// ступени, на которой персонаж на лестнице обычно и стоит.
    /// </summary>
    private bool CastGround(Vector3 center, float radius, float halfHeight, out RaycastHit groundHit)
    {
        float checkRadius = radius * 0.85f;
        float distance = Mathf.Max(0.01f, halfHeight - checkRadius + groundCheckDistance);
        return Physics.SphereCast(center, checkRadius, Vector3.down, out groundHit, distance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Пересекается ли капсула с геометрией, если поставить её центр в указанную точку.</summary>
    private bool OverlapsAt(Vector3 center, float radius, float halfHeight)
    {
        Vector3 top, bottom;
        GetCapsulePoints(center, radius, halfHeight, out top, out bottom);

        // Слой игрока исключён из groundLayers, поэтому собственная капсула сюда не попадёт.
        return Physics.CheckCapsule(top, bottom, radius + clearanceSkin, groundLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Упрётся ли капсула во что-нибудь, пройдя вперёд forwardClearance метров.</summary>
    private bool BlockedAhead(Vector3 center, float radius, float halfHeight, Vector3 direction)
    {
        Vector3 top, bottom;
        GetCapsulePoints(center, radius, halfHeight, out top, out bottom);

        RaycastHit hit;
        return Physics.CapsuleCast(top, bottom, radius + clearanceSkin, direction, out hit, forwardClearance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Центры полусфер капсулы для позиции center. Радиус запроса раздувается на clearanceSkin
    /// отдельно — сами центры полусфер считаются по настоящему радиусу, иначе раздувание
    /// подняло бы нижнюю точку капсулы и проверка стала бы менее строгой, а не более.
    /// </summary>
    private void GetCapsulePoints(Vector3 center, float radius, float halfHeight, out Vector3 top, out Vector3 bottom)
    {
        float offset = Mathf.Max(0.01f, halfHeight - radius);
        top = center + Vector3.up * offset;
        bottom = center - Vector3.up * offset;
    }

    /// <summary>
    /// Разрешает опираться только на статичную геометрию и кинематические тела,
    /// чтобы персонаж не «залезал» на катающиеся предметы.
    /// </summary>
    private bool IsAllowedSurface(Collider other)
    {
        if (!ignoreDynamicBodies)
        {
            return true;
        }

        Rigidbody attached = other.attachedRigidbody;
        return attached == null || attached == body || attached.isKinematic;
    }
}
