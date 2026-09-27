using DG.Tweening;
using UnityEngine;

/// <summary>
/// Удар кувалдой: пока кувалда в руках (слот экипировки), ЛКМ — замах, удар и возврат (анимация
/// модели в руке через DOTween). В момент удара — SphereCast из центра камеры:
/// - враг (EnemyHealth) — урон через ChangeHealth(−damage), тот же путь, что у Easy Weapons;
/// - кирпичная кладка (BrickWallSmash) — удар по конкретному кирпичу: сдвигается, вылетает, а после
///   нескольких выбитых кладка обрушивается;
/// - Breakable — Hit(): дрожит, пыль; после своего числа ударов разбит (или только звучит, если
///   кувалдой он не ломается);
/// - любая другая твёрдая поверхность — звук удара и пыль.
/// С кувалдой в руках ЛКМ только бьёт: подбор и установка в это время недоступны (PlayerItemInteraction
/// ведёт себя как с оружием).
/// </summary>
public class SledgehammerSwing : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: из её центра идёт удар.")]
    public Camera playerCamera;

    [Tooltip("Режим инвентаря: бьём, только пока кувалда — активный инструмент.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Модели инструментов в руке: отсюда берётся модель кувалды для замаха.")]
    public ToolPresenter toolPresenter;

    [Tooltip("Режим работы с объектом: пока он идёт (тряпка, лом), кувалда не бьёт.")]
    public PlayerToolActions toolActions;

    [Tooltip("Выносливость: каждый замах тратит бар и немного утомляет; при одышке замах не начинается, а " +
             "вымотанный игрок ломать объекты не может (врагов бьёт как обычно). Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Header("Конфиг")]
    [Tooltip("Если задан — урон и дальность берутся из GameConfig.tools при старте.")]
    public GameConfig config;

    [Header("Удар")]
    [Tooltip("Урон по врагу за один удар.")]
    [Min(0f)] public float damage = 40f;

    [Tooltip("Дальность удара от камеры, м.")]
    [Min(0.5f)] public float range = 2.2f;

    [Tooltip("Радиус «головы» удара, м: попасть кувалдой легче, чем пулей.")]
    [Min(0.01f)] public float radius = 0.25f;

    [Tooltip("По чему бьёт удар. Игрока, инструмент в руке и подбираемые предметы (IgnoreBullets) не включать.")]
    public LayerMask hitLayers = ~((1 << 8) | (1 << 9) | (1 << 24) | (1 << 30));

    [Header("Анимация")]
    [Tooltip("Замах, с.")]
    [Min(0.01f)] public float windupDuration = 0.2f;

    [Tooltip("Удар, с — в конце удара считается попадание.")]
    [Min(0.01f)] public float strikeDuration = 0.1f;

    [Tooltip("Возврат в руку, с. Следующий удар — только после возврата.")]
    [Min(0.01f)] public float recoverDuration = 0.35f;

    [Tooltip("Поворот модели в замахе поверх позы в руке, градусы, в осях HandPoint (X — вправо: " +
             "минус — кувалда запрокидывается назад-вверх).")]
    public Vector3 windupEuler = new Vector3(-40f, 0f, -8f);

    [Tooltip("Сдвиг модели в замахе, м (в осях HandPoint).")]
    public Vector3 windupOffset = new Vector3(0.02f, 0.12f, -0.08f);

    [Tooltip("Поворот модели в конце удара, градусы, в осях HandPoint (плюс по X — вперёд-вниз).")]
    public Vector3 strikeEuler = new Vector3(65f, 0f, 6f);

    [Tooltip("Сдвиг модели в конце удара, м.")]
    public Vector3 strikeOffset = new Vector3(-0.04f, -0.12f, 0.16f);

    [Header("Звуки и эффекты")]
    [Tooltip("Свист замаха.")]
    public SoundCue swingSound;

    [Tooltip("Попадание по врагу.")]
    public SoundCue enemyHitSound;

    [Tooltip("Попадание по стене, полу и прочему, что не ломается.")]
    public SoundCue surfaceHitSound;

    [Tooltip("Облачко пыли в точке удара о поверхность (префаб с ParticleSystem, сам себя уничтожает).")]
    public GameObject hitDust;

    /// <summary>Кувалда сейчас в замахе/ударе/возврате.</summary>
    public bool IsSwinging => swing != null && swing.IsActive();

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private Transform visual;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Sequence swing;

    private void Awake()
    {
        if (config == null) return;
        damage = config.tools.sledgehammerDamage;
        range = config.tools.sledgehammerRange;
    }

    private void OnDisable()
    {
        StopSwing();
    }

    private void Update()
    {
        bool equipped = modeController != null && modeController.ActiveToolKind == ToolKind.Sledgehammer;
        if (!equipped)
        {
            StopSwing();
            return;
        }

        // Клик, которым игрок возвращает захват курсора (CursorLockController — позже в кадре), не бьёт.
        if (Cursor.lockState != CursorLockMode.Locked || IsSwinging) return;
        if (toolActions != null && (toolActions.IsActive || toolActions.LastEndFrame == Time.frameCount)) return;
        if (Input.GetMouseButtonDown(0)) StartSwing();
    }

    private void StartSwing()
    {
        visual = toolPresenter != null ? toolPresenter.GetVisual(ToolKind.Sledgehammer) : null;
        if (visual == null) return;

        // Одышка — сил на замах нет.
        if (stamina != null && !stamina.TrySwing()) return;

        restPosition = visual.localPosition;
        restRotation = visual.localRotation;

        swing = DOTween.Sequence()
            .Append(visual.DOLocalRotateQuaternion(Quaternion.Euler(windupEuler) * restRotation, windupDuration).SetEase(Ease.OutQuad))
            .Join(visual.DOLocalMove(restPosition + windupOffset, windupDuration).SetEase(Ease.OutQuad))
            .AppendCallback(() => SoundPlayer.Play(swingSound, visual.position))
            .Append(visual.DOLocalRotateQuaternion(Quaternion.Euler(strikeEuler) * restRotation, strikeDuration).SetEase(Ease.InQuad))
            .Join(visual.DOLocalMove(restPosition + strikeOffset, strikeDuration).SetEase(Ease.InQuad))
            .AppendCallback(ResolveHit)
            .Append(visual.DOLocalRotateQuaternion(restRotation, recoverDuration).SetEase(Ease.OutCubic))
            .Join(visual.DOLocalMove(restPosition, recoverDuration).SetEase(Ease.OutCubic));
    }

    /// <summary>Сбросить замах: модель — в позу покоя (сменили инструмент, выключили компонент).</summary>
    private void StopSwing()
    {
        if (swing == null) return;
        swing.Kill();
        swing = null;
        if (visual == null) return;
        visual.localPosition = restPosition;
        visual.localRotation = restRotation;
    }

    /// <summary>Попадание в момент удара: ближайший враг, объект или твёрдая поверхность перед камерой.</summary>
    private void ResolveHit()
    {
        if (playerCamera == null) return;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        int count = Physics.SphereCastNonAlloc(ray, radius, Hits, range, hitLayers, QueryTriggerInteraction.Collide);
        int best = -1;
        for (int i = 0; i < count; i++)
        {
            Collider collider = Hits[i].collider;
            // Из триггеров интересны только объекты, которые можно разбить (у досок коллайдер-триггер).
            if (collider.isTrigger)
            {
                Breakable target = collider.GetComponentInParent<Breakable>();
                if (target == null || target.IsBroken) continue;
            }
            if (best < 0 || Hits[i].distance < Hits[best].distance) best = i;
        }
        if (best < 0) return;

        RaycastHit hit = Hits[best];
        // SphereCast, начатый уже внутри коллайдера, даёт distance 0 и пустую точку.
        Vector3 point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(ray.origin);

        EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null && !enemy.IsDead)
        {
            enemy.ChangeHealth(-damage);
            SoundPlayer.Play(enemyHitSound, point);
            return;
        }

        // Вымотанный игрок отбиваться может, а работать — нет: кладка и доски только глухо отзываются.
        bool canWork = stamina == null || !stamina.IsExhausted;

        BrickWallSmash wall = hit.collider.GetComponentInParent<BrickWallSmash>();
        if (canWork && wall != null && !wall.IsBroken)
        {
            wall.Hit(point, ray.direction);
            return;
        }

        Breakable breakable = hit.collider.GetComponentInParent<Breakable>();
        if (canWork && breakable != null && !breakable.IsBroken)
        {
            breakable.Hit(point);
            return;
        }

        SoundPlayer.Play(surfaceHitSound, point);
        if (hitDust != null) Instantiate(hitDust, point, Quaternion.LookRotation(hit.normal.sqrMagnitude > 0f ? hit.normal : -ray.direction));
    }
}
