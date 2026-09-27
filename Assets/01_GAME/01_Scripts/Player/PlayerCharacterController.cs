using System;
using UnityEngine;

/// <summary>
/// Передвижение игрока от первого лица на <see cref="CharacterController"/>.
///
/// Заменяет сторонний FirstPersonCharacter (Easy Weapons), который был на Rigidbody и имел
/// два настоящих бага: проверку земли и «прилипание» он считал от ЛОКАЛЬНОЙ высоты капсулы,
/// игнорируя масштаб корня, из-за чего игрок считался стоящим на земле, вися в воздухе,
/// а «прилипание» каждый кадр тянуло капсулу выше опоры и дралось с гравитацией. Плюс скорость
/// там писалась в linearVelocity напрямую, без какой-либо модели разгона, а склоны не
/// обрабатывались вовсе.
///
/// Здесь же подъём по ступеням делает встроенный CharacterController.stepOffset — отдельный
/// костыль для ступеней (был PlayerStepClimber) не нужен.
///
/// Работает в Update, а не в FixedUpdate: CharacterController не шагает вместе с физикой,
/// и один Move за кадр даёт ровное движение без дрожания.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCharacterController : MonoBehaviour
{
    [Header("Конфиг")]
    [Tooltip("Если задан — значения ниже перекрываются из GameConfig при старте.")]
    public GameConfig config;

    [Header("Скорости")]
    [Tooltip("Обычная скорость ходьбы, м/с. Одна на все направления: разные скорости вперёд и вбок делают движение по диагонали кривым.")]
    public float walkSpeed = 3.5f;

    [Tooltip("Скорость бега, м/с.")]
    public float sprintSpeed = 5.5f;

    [Tooltip("Время сглаживания разгона/торможения на земле, с. Малое значение ощущается мгновенным, но убирает рывки.")]
    public float groundSmoothTime = 0.06f;

    [Tooltip("То же в воздухе — больше, чтобы управление в прыжке было ограниченным.")]
    public float airSmoothTime = 0.25f;

    [Header("Прыжок и гравитация")]
    [Tooltip("Высота прыжка в метрах. Скорость считается из неё, поэтому высота не зависит от силы тяжести.")]
    public float jumpHeight = 1.1f;

    [Tooltip("Множитель гравитации. 1 — физически честно, но для игры обычно ощущается вяло.")]
    public float gravityMultiplier = 2f;

    [Tooltip("Сколько секунд после схода с края ещё засчитывается прыжок (coyote time).")]
    public float coyoteTime = 0.12f;

    [Tooltip("Сколько секунд держится нажатие прыжка, сделанное чуть раньше приземления.")]
    public float jumpBuffer = 0.12f;

    [Tooltip("На какую глубину доводить игрока к опоре, если контакт потерян не из-за прыжка. Замена «прилипания» старого контроллера — без него на спуске игрок отрывается и падает ступеньками.")]
    public float groundSnapDistance = 0.25f;

    [Header("Крутые склоны")]
    [Tooltip("Предельная скорость соскальзывания с поверхностей круче slopeLimit CharacterController'а, м/с. " +
             "Разгон — от гравитации.")]
    public float maxSlideSpeed = 10f;

    [Header("Клавиши")]
    [Tooltip("Прыжок. Перекрывается GameConfig.input.jumpKey, если задан конфиг.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("Бег (удерживать). Перекрывается GameConfig.input.sprintKey, если задан конфиг.")]
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Слои")]
    [Tooltip("По чему игрок считает опору. Слой самого игрока обязательно исключить.")]
    public LayerMask groundLayers = ~0;

    [Header("Выносливость и навыки")]
    [Tooltip("Выносливость: бег и прыжок её тратят, при одышке и с тяжёлым грузом недоступны. Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Tooltip("Навыки ветки «Выживание»: скорость ходьбы и бега, высота прыжка. Пусто — значения выше как есть.")]
    public PlayerSkills skills;

    /// <summary>Текущая скорость целиком (горизонталь + вертикаль). Читают покачивание камеры и предмета в руке.</summary>
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

    /// <summary>Горизонтальная скорость — именно она нужна покачиванию, вертикальная его только путает.</summary>
    public float HorizontalSpeed => horizontalVelocity.magnitude;

    /// <summary>Стоит ли игрок на опоре в этом кадре.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Игрок коснулся земли после полёта. Передаёт вертикальную скорость удара (отрицательную).</summary>
    public event Action<float> Landed;

    /// <summary>Игрок оттолкнулся в прыжке.</summary>
    public event Action Jumped;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private Vector3 smoothVelocity;
    private float verticalVelocity;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private Vector3 groundNormal = Vector3.up;
    private bool jumpedThisFrame;
    private bool onSteepSlope;
    private Vector3 steepNormal = Vector3.up;
    private float slideSpeed;

    // Контакты последнего controller.Move (OnControllerColliderHit): было ли касание нижней частью
    // капсулы, было ли среди них пологое, и самая пологая из крутых нормалей.
    private bool moveLowerContact;
    private bool moveWalkableContact;
    private Vector3 moveSteepNormal = Vector3.up;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        ApplyConfig();
    }

    private void ApplyConfig()
    {
        if (config == null) return;

        PlayerMovementSettings m = config.movement;
        walkSpeed = m.walkSpeed;
        sprintSpeed = m.sprintSpeed;
        groundSmoothTime = m.groundSmoothTime;
        airSmoothTime = m.airSmoothTime;
        jumpHeight = m.jumpHeight;
        gravityMultiplier = m.gravityMultiplier;
        coyoteTime = m.coyoteTime;
        jumpBuffer = m.jumpBuffer;
        groundSnapDistance = m.groundSnapDistance;
        maxSlideSpeed = m.maxSlideSpeed;

        jumpKey = config.input.jumpKey;
        sprintKey = config.input.sprintKey;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        bool groundedBefore = IsGrounded;
        ProbeGround();

        if (Input.GetKeyDown(jumpKey)) lastJumpPressedTime = Time.time;

        // Скорость удара запоминаем ДО ApplyVertical: на земле он сразу сбрасывает её в -2, и
        // Landed всегда получал бы одно и то же число вместо реальной высоты падения.
        float fallSpeed = verticalVelocity;

        ApplyHorizontalInput(deltaTime);
        ApplyVertical(deltaTime);

        Vector3 motion = horizontalVelocity;

        // На земле ведём движение вдоль опоры: иначе на спуске игрок уходит по касательной
        // в воздух и сыплется ступеньками, а на подъёме теряет скорость.
        if (IsGrounded && !jumpedThisFrame && groundNormal != Vector3.up)
            motion = Vector3.ProjectOnPlane(motion, groundNormal);

        motion += ApplySlide(deltaTime);
        motion += Vector3.up * verticalVelocity;

        moveLowerContact = false;
        moveWalkableContact = false;
        moveSteepNormal = Vector3.up;
        CollisionFlags flags = controller.Move(motion * deltaTime);

        // Упёрлись головой — гасим подъём, иначе игрок «липнет» к потолку до конца прыжка.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        bool steepContact = moveLowerContact && !moveWalkableContact;
        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity <= 0f && !onSteepSlope && !steepContact) IsGrounded = true;

        if (!IsGrounded && !jumpedThisFrame) SnapToGround();

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (!groundedBefore) Landed?.Invoke(Mathf.Min(fallSpeed, verticalVelocity));
        }

        jumpedThisFrame = false;
    }

    /// <summary>
    /// Определяет опору. Полагаться на один CharacterController.isGrounded нельзя — он
    /// печально известен тем, что мигает на стыках коллайдеров и на кромках ступеней,
    /// поэтому дополнительно щупаем сферой и заодно забираем нормаль опоры для склонов.
    /// </summary>
    private void ProbeGround()
    {
        groundNormal = Vector3.up;
        onSteepSlope = false;

        // Летим вверх (прыжок) — опоры нет по определению. Раньше в первые кадры после толчка щуп
        // ещё доставал до пола: срабатывал Landed, сбрасывался coyote time, и можно было прыгнуть второй раз.
        if (verticalVelocity > 0f)
        {
            IsGrounded = false;
            return;
        }

        float probeRadius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
        Vector3 origin = transform.TransformPoint(controller.center)
                       + Vector3.down * (controller.height * 0.5f - controller.radius);
        float distance = controller.skinWidth + 0.12f;

        RaycastHit hit;
        bool probed = Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, distance,
                                         groundLayers, QueryTriggerInteraction.Ignore);

        if (probed)
        {
            if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
            {
                groundNormal = hit.normal;
                IsGrounded = true;
                return;
            }

            // Сфера часто цепляет кромку ступени и отдаёт «крутую» нормаль ребра. Настоящий крутой
            // склон отличаем проверкой поверхности прямо под центром.
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit under, controller.radius + distance,
                                groundLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(under.normal, Vector3.up) <= controller.slopeLimit)
            {
                groundNormal = under.normal;
                IsGrounded = true;
                return;
            }

            // Круче slopeLimit — не опора: игрок соскальзывает (ApplySlide) и не может запрыгнуть
            // наверх серией прыжков (раньше здесь срабатывал запасной controller.isGrounded).
            onSteepSlope = true;
            steepNormal = hit.normal;
            IsGrounded = false;
            return;
        }

        // Щуп опоры не нашёл. Запасной controller.isGrounded верит любому касанию снизу — в том числе
        // крутому склону, если капсула касается его боком (щуп вниз его не достаёт). Поэтому смотрим,
        // чем было это касание в прошлом Move.
        if (controller.isGrounded && moveLowerContact && !moveWalkableContact)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit below, controller.radius + distance,
                                groundLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(below.normal, Vector3.up) <= controller.slopeLimit)
            {
                groundNormal = below.normal;
                IsGrounded = true;
                return;
            }

            onSteepSlope = true;
            steepNormal = moveSteepNormal;
            IsGrounded = false;
            return;
        }

        IsGrounded = controller.isGrounded;
    }

    /// <summary>
    /// Соскальзывание с крутого склона. CharacterController сам по такой поверхности вниз не едет —
    /// упирается и «стоит», — поэтому движение вдоль склона вниз добавляется явно, с разгоном от
    /// гравитации. Ввод игрока не может толкать его вверх по склону. Вне склона — обнуляется.
    /// </summary>
    private Vector3 ApplySlide(float deltaTime)
    {
        if (!onSteepSlope)
        {
            slideSpeed = 0f;
            return Vector3.zero;
        }

        Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, steepNormal).normalized;
        slideSpeed = Mathf.Min(maxSlideSpeed, slideSpeed + Mathf.Abs(Physics.gravity.y) * gravityMultiplier * deltaTime);

        Vector3 upSlope = -new Vector3(slideDirection.x, 0f, slideDirection.z).normalized;
        float intoSlope = Vector3.Dot(horizontalVelocity, upSlope);
        if (intoSlope > 0f) horizontalVelocity -= upSlope * intoSlope;

        // Вниз ведёт само скольжение; копить свободное падение, упираясь в склон, незачем —
        // иначе при сходе со склона игрок получил бы огромную скорость падения.
        verticalVelocity = Mathf.Max(verticalVelocity, -2f);
        return slideDirection * slideSpeed;
    }

    /// <summary>Контакты капсулы во время controller.Move — по ним видно, стоит ли игрок на крутом склоне,
    /// которого не достал щуп опоры.</summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y <= 0.01f) return; // стены и потолок

        Vector3 bottomSphereCenter = transform.TransformPoint(controller.center)
                                     + Vector3.down * (controller.height * 0.5f - controller.radius);
        if (hit.point.y > bottomSphereCenter.y) return; // касание боком выше низа капсулы

        moveLowerContact = true;
        if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
            moveWalkableContact = true;
        else if (moveSteepNormal == Vector3.up || hit.normal.y > moveSteepNormal.y)
            moveSteepNormal = hit.normal;
    }

    private void ApplyHorizontalInput(float deltaTime)
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        float walk = SkillValue(SkillStat.WalkSpeed, walkSpeed);
        bool sprinting = Input.GetKey(sprintKey) && input.sqrMagnitude > 0.01f && (stamina == null || stamina.CanSprint);
        float speed = sprinting ? SkillValue(SkillStat.SprintSpeed, sprintSpeed) : walk;
        if (stamina != null) speed *= stamina.MoveSpeedMultiplier;
        Vector3 target = (transform.forward * input.y + transform.right * input.x) * speed;

        float smoothTime = IsGrounded ? groundSmoothTime : airSmoothTime;
        horizontalVelocity = Vector3.SmoothDamp(horizontalVelocity, target, ref smoothVelocity, smoothTime, Mathf.Infinity, deltaTime);

        // Бег тратит выносливость, только пока игрок реально бежит: упёрся в стену с зажатым Shift — не тратит.
        if (sprinting && stamina != null && horizontalVelocity.magnitude > walk * 1.05f)
            stamina.DrainSprint(deltaTime);
    }

    private float SkillValue(SkillStat stat, float baseValue) => skills != null ? skills.GetValue(stat, baseValue) : baseValue;

    private void ApplyVertical(float deltaTime)
    {
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBuffer;

        if (canJump && wantsJump)
        {
            lastJumpPressedTime = float.NegativeInfinity;

            // Нет сил (одышка) или в руках тяжёлое — нажатие просто гаснет, игрок остаётся на земле.
            if (stamina == null || stamina.TryJump())
            {
                // v = sqrt(2 * g * h) — высота прыжка задаётся в метрах и не зависит от gravityMultiplier.
                float height = SkillValue(SkillStat.JumpHeight, jumpHeight);
                verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * gravityMultiplier * height);
                lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
                jumpedThisFrame = true;
                Jumped?.Invoke();
                return;
            }
        }

        if (IsGrounded && verticalVelocity <= 0f)
        {
            // Небольшая постоянная скорость вниз прижимает контроллер к опоре. Именно это
            // заменяет багованное «прилипание» старого контроллера.
            verticalVelocity = -2f;
            return;
        }

        verticalVelocity += Physics.gravity.y * gravityMultiplier * deltaTime;
    }

    /// <summary>
    /// Доводит игрока к опоре, если контакт потерян не из-за прыжка (спуск по ступеням, перегиб
    /// склона). Без этого спуск превращается в серию маленьких падений.
    /// </summary>
    private void SnapToGround()
    {
        if (verticalVelocity > 0f) return;

        float probeRadius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
        Vector3 origin = transform.TransformPoint(controller.center)
                       + Vector3.down * (controller.height * 0.5f - controller.radius);

        RaycastHit hit;
        if (!Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, groundSnapDistance,
                                groundLayers, QueryTriggerInteraction.Ignore))
            return;

        if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit) return;

        controller.Move(Vector3.down * hit.distance);
        groundNormal = hit.normal;
        IsGrounded = true;
    }

}
