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
/// Здесь же подъём по ступеням делает встроенный CharacterController.stepOffset — поэтому
/// отдельный костыль PlayerStepClimber больше не нужен.
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

    [Header("Клавиши")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Слои")]
    [Tooltip("По чему игрок считает опору. Слой самого игрока обязательно исключить.")]
    public LayerMask groundLayers = ~0;

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

        ApplyHorizontalInput(deltaTime);
        ApplyVertical(deltaTime);

        Vector3 motion = horizontalVelocity;

        // На земле ведём движение вдоль опоры: иначе на спуске игрок уходит по касательной
        // в воздух и сыплется ступеньками, а на подъёме теряет скорость.
        if (IsGrounded && !jumpedThisFrame && groundNormal != Vector3.up)
            motion = Vector3.ProjectOnPlane(motion, groundNormal);

        motion += Vector3.up * verticalVelocity;

        CollisionFlags flags = controller.Move(motion * deltaTime);

        // Упёрлись головой — гасим подъём, иначе игрок «липнет» к потолку до конца прыжка.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        if ((flags & CollisionFlags.Below) != 0) IsGrounded = true;

        if (!IsGrounded && !jumpedThisFrame) SnapToGround();

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (!groundedBefore) Landed?.Invoke(verticalVelocity);
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

        float probeRadius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
        Vector3 origin = transform.TransformPoint(controller.center)
                       + Vector3.down * (controller.height * 0.5f - controller.radius);
        float distance = controller.skinWidth + 0.12f;

        RaycastHit hit;
        bool probed = Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, distance,
                                         groundLayers, QueryTriggerInteraction.Ignore);

        if (probed && Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
        {
            groundNormal = hit.normal;
            IsGrounded = true;
            return;
        }

        IsGrounded = controller.isGrounded;
    }

    private void ApplyHorizontalInput(float deltaTime)
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        float speed = Input.GetKey(sprintKey) ? sprintSpeed : walkSpeed;
        Vector3 target = (transform.forward * input.y + transform.right * input.x) * speed;

        float smoothTime = IsGrounded ? groundSmoothTime : airSmoothTime;
        horizontalVelocity = Vector3.SmoothDamp(horizontalVelocity, target, ref smoothVelocity, smoothTime, Mathf.Infinity, deltaTime);
    }

    private void ApplyVertical(float deltaTime)
    {
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBuffer;

        if (canJump && wantsJump)
        {
            // v = sqrt(2 * g * h) — высота прыжка задаётся в метрах и не зависит от gravityMultiplier.
            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * gravityMultiplier * jumpHeight);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
            jumpedThisFrame = true;
            Jumped?.Invoke();
            return;
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

    /// <summary>
    /// Толчок извне (отдача, взрыв). CharacterController — не Rigidbody, физика его не двигает,
    /// поэтому импульс добавляется вручную.
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        horizontalVelocity += new Vector3(impulse.x, 0f, impulse.z);
        verticalVelocity += impulse.y;
        if (impulse.y > 0f) IsGrounded = false;
    }
}
