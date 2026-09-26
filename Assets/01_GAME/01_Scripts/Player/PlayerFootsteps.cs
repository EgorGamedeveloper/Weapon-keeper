using UnityEngine;

/// <summary>
/// Звуки шагов, прыжка и приземления игрока с учётом поверхности под ногами (SurfaceTag → SurfaceType,
/// без тега — defaultSurface).
///
/// Шаг считается по ПРОЙДЕННОМУ пути на земле, а не по таймеру: упёрся в стену — шаги стихли, бег —
/// шаги чаще (длина шага растёт медленнее скорости) и громче. Поверхность — луч вниз из центра капсулы.
/// Приземление берёт событие Landed контроллера: мелкие «приземления» на спуске по ступеням
/// (скорость удара меньше landingMinSpeed) молчат, громкость растёт с высотой падения.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Контроллер игрока: состояние земли, скорость, события прыжка и приземления.")]
    public PlayerCharacterController controller;

    [Header("Поверхности")]
    [Tooltip("Поверхность, если у объекта под ногами нет SurfaceTag (бетон).")]
    public SurfaceType defaultSurface;

    [Header("Шаги")]
    [Tooltip("Длина шага при ходьбе, м: столько нужно пройти по земле до следующего звука.")]
    [Min(0.1f)] public float walkStride = 1.6f;

    [Tooltip("Длина шага на полной скорости бега, м.")]
    [Min(0.1f)] public float sprintStride = 2.2f;

    [Tooltip("Громкость шага при ходьбе (при беге — полная).")]
    [Range(0f, 1f)] public float walkVolume = 0.75f;

    [Tooltip("Ниже этой горизонтальной скорости игрок считается стоящим, м/с.")]
    [Min(0f)] public float moveThreshold = 0.3f;

    [Header("Прыжок и приземление")]
    [Tooltip("Звук толчка при прыжке (шорох одежды); шаг по поверхности играется вместе с ним.")]
    public SoundCue jumpSound;

    [Tooltip("Приземления медленнее этой скорости (спуск по ступеням, мелкие неровности) молчат, м/с.")]
    [Min(0f)] public float landingMinSpeed = 3f;

    [Tooltip("Скорость удара, при которой приземление звучит на полную громкость, м/с.")]
    [Min(0.1f)] public float landingFullSpeed = 11f;

    private CharacterController characterController;
    private Vector3 previousPosition;
    private float distanceToNextStep;
    private Collider cachedGroundCollider;
    private SurfaceType cachedGroundSurface;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (controller == null) controller = GetComponent<PlayerCharacterController>();
        previousPosition = transform.position;
        distanceToNextStep = walkStride * 0.4f;
    }

    private void OnEnable()
    {
        if (controller == null) return;
        controller.Landed += HandleLanded;
        controller.Jumped += HandleJumped;
    }

    private void OnDisable()
    {
        if (controller == null) return;
        controller.Landed -= HandleLanded;
        controller.Jumped -= HandleJumped;
    }

    private void Update()
    {
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        if (controller == null || Time.deltaTime <= 0f) return;

        float speed = controller.HorizontalSpeed;
        if (!controller.IsGrounded || speed < moveThreshold)
        {
            // Первый шаг после остановки звучит почти сразу, а не через целую длину шага.
            distanceToNextStep = Mathf.Min(distanceToNextStep, walkStride * 0.4f);
            return;
        }

        float sprint = Mathf.InverseLerp(controller.walkSpeed, controller.sprintSpeed, speed);
        float stride = Mathf.Lerp(walkStride, sprintStride, sprint);

        distanceToNextStep -= new Vector2(delta.x, delta.z).magnitude;
        if (distanceToNextStep > 0f) return;

        distanceToNextStep += stride;
        // Телепорт (загрузка сейва, лифт): не догоняем шагами всю пройденную дистанцию.
        if (distanceToNextStep < 0f) distanceToNextStep = stride;

        SurfaceType surface = GetGroundSurface();
        if (surface != null) SoundPlayer.Play2D(surface.footsteps, Mathf.Lerp(walkVolume, 1f, sprint));
    }

    private void HandleJumped()
    {
        SurfaceType surface = GetGroundSurface();
        if (surface != null) SoundPlayer.Play2D(surface.footsteps, walkVolume);
        SoundPlayer.Play2D(jumpSound);
    }

    private void HandleLanded(float impactVelocity)
    {
        float speed = Mathf.Abs(impactVelocity);
        if (speed < landingMinSpeed) return;

        SurfaceType surface = GetGroundSurface();
        if (surface == null) return;

        float volume = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(landingMinSpeed, landingFullSpeed, speed));
        SoundPlayer.Play2D(surface.landing, volume);
        distanceToNextStep = walkStride * 0.5f;
    }

    /// <summary>Поверхность под ногами: луч вниз из центра капсулы до чуть ниже подошвы.</summary>
    private SurfaceType GetGroundSurface()
    {
        Vector3 origin = transform.TransformPoint(characterController.center);
        float distance = characterController.height * 0.5f * transform.lossyScale.y + 0.4f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, controller.groundLayers, QueryTriggerInteraction.Ignore))
            return defaultSurface;

        if (hit.collider != cachedGroundCollider)
        {
            cachedGroundCollider = hit.collider;
            cachedGroundSurface = SurfaceTag.Resolve(hit.collider, defaultSurface);
        }
        return cachedGroundSurface;
    }
}
