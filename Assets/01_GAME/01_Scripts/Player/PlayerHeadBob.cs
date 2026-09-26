using UnityEngine;

/// <summary>
/// Покачивание камеры при ходьбе и просадка при приземлении.
///
/// Замена стороннего FirstPersonHeadBob (Easy Weapons): тот читал позицию через
/// <c>GetComponent&lt;Rigidbody&gt;()</c> и тип FirstPersonCharacter, поэтому после перехода
/// на CharacterController падал с NullReference. Править сторонний файл нельзя по конвенции
/// проекта, поэтому компонент переписан своим.
///
/// Работает в LateUpdate — после MouseRotator, который крутит тот же Head, иначе покачивание
/// затиралось бы вращением камеры.
///
/// Фаза качания считается от ПРОЙДЕННОГО ПУТИ, а не от желаемой скорости: если упереться
/// в стену, путь не растёт и камера честно перестаёт качаться (этот же приём был в оригинале).
/// </summary>
public class PlayerHeadBob : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Трансформ, который покачиваем (Head). Его же крутит MouseRotator.")]
    public Transform head;

    [Tooltip("Контроллер игрока — источник состояния земли и событий прыжка/приземления.")]
    public PlayerCharacterController controller;

    [Header("Ходьба")]
    [Tooltip("Сколько полных циклов покачивания приходится на метр пути.")]
    public float cyclesPerMeter = 1.5f;

    [Tooltip("Амплитуда вертикального покачивания, м.")]
    public float bobHeight = 0.05f;

    [Tooltip("Амплитуда боковой раскачки, м.")]
    public float bobSide = 0.02f;

    [Tooltip("Скорость, при которой покачивание выходит на полную амплитуду, м/с.")]
    public float fullAmplitudeSpeed = 3.5f;

    [Header("Приземление")]
    [Tooltip("Насколько камера просаживается при приземлении на метр скорости удара, м.")]
    public float landDip = 0.03f;

    [Tooltip("Максимальная просадка при приземлении, м.")]
    public float maxLandDip = 0.15f;

    [Tooltip("Жёсткость возврата после просадки.")]
    public float landSpring = 90f;

    [Tooltip("Затухание пружины приземления.")]
    public float landDamping = 12f;

    [Header("Звуки")]
    [Tooltip("Звук прыжка. Пусто — без звука.")]
    public AudioClip jumpSound;
    [Tooltip("Звук приземления. Пусто — без звука.")]
    public AudioClip landSound;

    private Vector3 baseLocalPosition;
    private Vector3 previousPosition;
    private float travelled;
    private float springOffset;
    private float springVelocity;
    private AudioSource audioSource;

    private void Awake()
    {
        if (head == null) return;
        baseLocalPosition = head.localPosition;
        previousPosition = transform.position;
        audioSource = GetComponent<AudioSource>();
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

        if (head != null) head.localPosition = baseLocalPosition;
    }

    private void LateUpdate()
    {
        if (head == null) return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;

        float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        bool grounded = controller == null || controller.IsGrounded;
        if (grounded) travelled += horizontalDistance;

        float speed = horizontalDistance / deltaTime;
        float amplitude = grounded ? Mathf.Clamp01(speed / Mathf.Max(0.01f, fullAmplitudeSpeed)) : 0f;

        float phase = travelled * cyclesPerMeter * Mathf.PI * 2f;
        // Модуль синуса — шаги идут вниз-вверх дважды за цикл, как настоящая походка.
        float bobY = -Mathf.Abs(Mathf.Sin(phase)) * bobHeight * amplitude;
        float bobX = Mathf.Sin(phase * 0.5f) * bobSide * amplitude;

        UpdateLandSpring(deltaTime);

        head.localPosition = baseLocalPosition + new Vector3(bobX, bobY + springOffset, 0f);
    }

    private void UpdateLandSpring(float deltaTime)
    {
        springVelocity -= springOffset * landSpring * deltaTime;
        springVelocity -= springVelocity * Mathf.Min(1f, landDamping * deltaTime);
        springOffset += springVelocity * deltaTime;

        if (Mathf.Abs(springOffset) < 0.0005f && Mathf.Abs(springVelocity) < 0.0005f)
        {
            springOffset = 0f;
            springVelocity = 0f;
        }
    }

    private void HandleLanded(float impactVelocity)
    {
        springOffset -= Mathf.Min(maxLandDip, Mathf.Abs(impactVelocity) * landDip);
        PlayClip(landSound);
    }

    private void HandleJumped() => PlayClip(jumpSound);

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
