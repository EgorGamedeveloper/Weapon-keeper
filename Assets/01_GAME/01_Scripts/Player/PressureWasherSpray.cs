using UnityEngine;

/// <summary>
/// Мойка высокого давления: пока она активный инструмент (слот экипировки) и игрок держит ЛКМ, из сопла
/// бьёт струя туда, куда смотрит прицел (луч из центра камеры, до range метров). Камера и ходьба при этом
/// свободны — как в симуляторах мойки. Попала струя в пятно (CleanableStain) — пятно смывается по пути
/// прицела (ScrubSegment) и, если прицел стоит, в одной точке (SprayAt). Мойка отмывает пятна любого
/// размера, большие (копоть, граффити) — только она.
///
/// Визуал — LineRenderer струи от сопла до точки, капли у сопла и брызги в точке попадания (частицы
/// на объекте этого компонента, их ставит сцена). Сопло ищется по имени в модели мойки в руке
/// (ToolPresenter). Звук — зацикленный AudioSource; клип пока не назначен, мойка молчит.
/// </summary>
public class PressureWasherSpray : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: из её центра идёт луч струи.")]
    public Camera playerCamera;

    [Tooltip("Режим инвентаря: моем, только пока мойка — активный инструмент.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Модели инструментов в руке: отсюда берётся сопло мойки.")]
    public ToolPresenter toolPresenter;

    [Tooltip("Режим работы с объектом: пока он идёт, мойка не стреляет.")]
    public PlayerToolActions toolActions;

    [Tooltip("Подсказка у прицела при наведении на пятно.")]
    public ItemInfoUI infoUI;

    [Tooltip("Кольцо прогресса: сколько пятна уже смыто.")]
    public HoldProgressUI progressUI;

    [Tooltip("Выносливость: смытое пятно утомляет, как оттёртое; вымотанный игрок мойкой не работает. " +
             "Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Header("Конфиг")]
    [Tooltip("Если задан — дальность, радиус и сила струи берутся из GameConfig.tools при старте.")]
    public GameConfig config;

    [Header("Струя")]
    [Tooltip("Дальность струи от камеры, м.")]
    [Min(0.5f)] public float range = 6f;

    [Tooltip("Радиус пятна от струи, м.")]
    [Min(0.01f)] public float radius = 0.14f;

    [Tooltip("Сколько альфы снимает штамп струи, когда прицел ведут по пятну.")]
    [Range(0.01f, 1f)] public float strength = 0.12f;

    [Tooltip("Как быстро струя смывает пятно, если прицел стоит на месте (доля в секунду в центре струи).")]
    [Min(0f)] public float holdRate = 2.5f;

    [Tooltip("По чему бьёт струя. Игрока и инструмент в руке не включать.")]
    public LayerMask hitLayers = ~((1 << 8) | (1 << 9) | (1 << 30));

    [Header("Визуал")]
    [Tooltip("Струя: LineRenderer от сопла до точки попадания.")]
    public LineRenderer jet;

    [Tooltip("Капли у сопла: ParticleSystem, который ставится на сопло и смотрит в точку попадания.")]
    public ParticleSystem streamParticles;

    [Tooltip("Брызги в точке попадания: ParticleSystem, который переносится туда, куда бьёт струя.")]
    public ParticleSystem splashParticles;

    [Tooltip("Имя дочернего объекта сопла в модели мойки.")]
    public string nozzleName = "Nozzle";

    [Tooltip("Насколько струя провисает к концу (доля длины).")]
    [Range(0f, 0.2f)] public float jetDrop = 0.03f;

    [Tooltip("Дрожь модели мойки в руке, пока бьёт струя, м.")]
    [Min(0f)] public float kickAmount = 0.003f;

    [Header("Звук")]
    [Tooltip("Зацикленный шум струи: AudioSource с назначенным клипом. Клип пока не назначен — мойка молчит.")]
    public AudioSource loopSource;

    /// <summary>Струя сейчас бьёт.</summary>
    public bool IsSpraying { get; private set; }

    private const int JetPoints = 8;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private Transform washerVisual;
    private Transform nozzle;
    private Vector3 visualRestPosition;
    private CleanableStain lastStain;
    private Vector3 lastAimPoint;

    private void Awake()
    {
        if (config != null)
        {
            ToolSettings s = config.tools;
            range = s.washerRange;
            radius = s.washerRadius;
            strength = s.washerStrength;
            holdRate = s.washerHoldRate;
        }

        if (jet != null)
        {
            jet.positionCount = JetPoints;
            jet.enabled = false;
        }
        SetEmission(streamParticles, false);
        SetEmission(splashParticles, false);
    }

    private void OnDisable()
    {
        StopSpray();
    }

    private void Update()
    {
        bool equipped = modeController != null && modeController.ActiveToolKind == ToolKind.PressureWasher;
        if (!equipped)
        {
            StopSpray();
            return;
        }

        bool busy = toolActions != null && (toolActions.IsActive || toolActions.LastEndFrame == Time.frameCount);
        bool tired = stamina != null && stamina.IsExhausted;
        bool wants = Cursor.lockState == CursorLockMode.Locked && !busy && !tired && Input.GetMouseButton(0);

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hit = TraceStream(ray, out Vector3 point, out Vector3 normal, out CleanableStain stain);
        if (tired && stain != null && infoUI != null) infoUI.ShowHint(Loc.Get(stain.titleKey), Loc.Get("hud.stamina.too_tired"));
        else ShowHint(stain);

        if (!wants)
        {
            StopSpray();
            return;
        }

        IsSpraying = true;
        UpdateVisuals(point, normal, hit);
        if (loopSource != null && loopSource.clip != null && !loopSource.isPlaying) loopSource.Play();

        if (stain == null)
        {
            lastStain = null;
            if (progressUI != null) progressUI.Hide();
            return;
        }

        // Ведут прицел по пятну — полоса вдоль пути; стоят на месте — точка «проедается» постепенно.
        float progressBefore = stain.Progress;
        if (lastStain == stain) stain.ScrubSegment(lastAimPoint, point, radius, strength);
        if (!stain.IsClean) stain.SprayAt(point, radius, holdRate * Time.deltaTime);
        if (stamina != null) stamina.WorkScrub(0f, stain.Progress - progressBefore);
        lastStain = stain;
        lastAimPoint = point;
        if (progressUI != null)
        {
            if (stain.IsClean) progressUI.Hide();
            else progressUI.SetProgress(stain.Progress);
        }
    }

    /// <summary>Куда бьёт струя: ближайшее пятно (его плоскость) или твёрдая поверхность; иначе — конец дальности.</summary>
    private bool TraceStream(Ray ray, out Vector3 point, out Vector3 normal, out CleanableStain stain)
    {
        point = ray.GetPoint(range);
        normal = -ray.direction;
        stain = null;

        int count = Physics.RaycastNonAlloc(ray, Hits, range, hitLayers, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.distance >= best) continue;

            CleanableStain candidate = null;
            if (hit.collider.isTrigger)
            {
                candidate = hit.collider.GetComponentInParent<CleanableStain>();
                if (candidate == null || candidate.IsClean) continue;
            }

            best = hit.distance;
            found = true;
            stain = candidate;
            point = hit.point;
            normal = hit.normal;
        }

        // По пятну бьём в его плоскость (поверхность стены/пола), а не в край тонкого триггера.
        if (stain != null && stain.Raycast(ray, out Vector3 onStain))
        {
            point = onStain;
            normal = stain.SurfaceNormal;
        }
        return found;
    }

    private void ShowHint(CleanableStain stain)
    {
        if (infoUI == null || stain == null) return;
        infoUI.ShowHint(Loc.Get(stain.titleKey), Loc.Get("hud.action.wash"));
    }

    private void UpdateVisuals(Vector3 point, Vector3 normal, bool hit)
    {
        Transform currentNozzle = FindNozzle();
        Vector3 origin = currentNozzle != null ? currentNozzle.position : playerCamera.transform.position;
        Vector3 direction = point - origin;

        if (jet != null)
        {
            jet.enabled = true;
            float drop = direction.magnitude * jetDrop;
            for (int i = 0; i < JetPoints; i++)
            {
                float t = i / (float)(JetPoints - 1);
                // Струя чуть провисает к концу и мелко дрожит.
                Vector3 p = Vector3.Lerp(origin, point, t) + Vector3.down * (drop * t * t)
                            + Random.insideUnitSphere * (0.004f * t);
                jet.SetPosition(i, p);
            }
        }

        if (streamParticles != null && direction.sqrMagnitude > 0.0001f)
        {
            streamParticles.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            SetEmission(streamParticles, true);
        }

        if (splashParticles != null)
        {
            splashParticles.transform.SetPositionAndRotation(point + normal * 0.02f, Quaternion.LookRotation(normal));
            SetEmission(splashParticles, hit);
        }

        // Отдача: модель мелко дрожит в руке.
        if (washerVisual != null && kickAmount > 0f)
            washerVisual.localPosition = visualRestPosition + Random.insideUnitSphere * kickAmount;
    }

    private Transform FindNozzle()
    {
        Transform visual = toolPresenter != null ? toolPresenter.GetVisual(ToolKind.PressureWasher) : null;
        if (visual != washerVisual)
        {
            washerVisual = visual;
            nozzle = null;
            if (visual != null)
            {
                visualRestPosition = visual.localPosition;
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                    if (child.name == nozzleName) { nozzle = child; break; }
            }
        }
        return nozzle != null ? nozzle : washerVisual;
    }

    private void StopSpray()
    {
        if (!IsSpraying) return;
        IsSpraying = false;
        lastStain = null;
        if (jet != null) jet.enabled = false;
        SetEmission(streamParticles, false);
        SetEmission(splashParticles, false);
        if (loopSource != null) loopSource.Stop();
        if (progressUI != null) progressUI.Hide();
        if (washerVisual != null) washerVisual.localPosition = visualRestPosition;
    }

    private static void SetEmission(ParticleSystem system, bool state)
    {
        if (system == null) return;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = state;
        if (state && !system.isPlaying) system.Play();
    }
}
