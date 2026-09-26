using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Режим работы с объектом: игрок «встаёт» к объекту и работает руками, а мышь управляет не камерой,
/// а инструментом.
/// - Тряпка по пятну (CleanableStain): тряпка вылетает из руки на пятно, мышь водит её по плоскости
///   пятна, и оно стирается там, где тряпка прошла.
/// - Лом-рычаг (Breakable.canPry, лом в руках): лом переносится из руки к концу доски, лапка — под
///   кромку; мышь вверх-вниз качает рычаг, доска приподнимается и в конце снимается.
///
/// Вход — ЛКМ по объекту (зовёт PlayerItemInteraction). Пока режим идёт, обзор, ходьба и клики по миру
/// выключены через GameplayInputBlocker (курсор остаётся захваченным). Выход — ПКМ, конец работы,
/// освободившийся курсор (пауза, окно) или пропавшая цель; прогресс остаётся на объекте.
/// </summary>
public class PlayerToolActions : MonoBehaviour
{
    private enum ActionMode { None, Scrub, Pry }

    [Header("Ссылки")]
    [Tooltip("Камера игрока: от неё считаются направления «вправо/вверх» для тряпки и положение рук для лома.")]
    public Camera playerCamera;

    [Tooltip("Блокировка ввода: на время режима выключает обзор, ходьбу и клики по миру (курсор не освобождает).")]
    public GameplayInputBlocker inputBlocker;

    [Tooltip("Режим инвентаря: лом должен оставаться в руках, пока им работают.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Модели инструментов в руке: отсюда берётся модель лома.")]
    public ToolPresenter toolPresenter;

    [Tooltip("Подсказка у прицела на время режима («ПКМ — выйти»).")]
    public ItemInfoUI infoUI;

    [Tooltip("Кольцо прогресса у прицела.")]
    public HoldProgressUI progressUI;

    [Tooltip("Модель тряпки в руке (дочерний объект HandPoint): видна только во время оттирания. Лицевая " +
             "сторона — локальная +Y.")]
    public Transform rag;

    [Header("Конфиг")]
    [Tooltip("Если задан — чувствительность и параметры рычага берутся из GameConfig.tools при старте.")]
    public GameConfig config;

    [Header("Тряпка")]
    [Tooltip("Насколько тряпка сдвигается по пятну на единицу движения мыши, м.")]
    [Min(0.0001f)] public float ragSensitivity = 0.012f;

    [Tooltip("Насколько тряпка приподнята над поверхностью пятна, м.")]
    [Min(0f)] public float ragLift = 0.012f;

    [Header("Лом")]
    [Tooltip("Насколько рычаг сдвигается на единицу движения мыши по вертикали (весь ход — от −1 до 1).")]
    [Min(0.001f)] public float leverSensitivity = 0.08f;

    [Tooltip("Угол качания лома вокруг лапки на краю хода рычага, градусы.")]
    [Range(1f, 45f)] public float leverAngle = 14f;

    [Tooltip("Суммарный ход рычага, чтобы снять доску. Полный качок вверх-вниз — 4, упор в край ход не даёт.")]
    [Min(0.5f)] public float pryTravelToBreak = 14f;

    [Header("Перенос инструмента")]
    [Tooltip("За сколько секунд тряпка или лом долетает из руки до объекта и обратно.")]
    [Min(0.01f)] public float flightDuration = 0.22f;

    [Tooltip("Высота дуги перелёта, м.")]
    [Min(0f)] public float flightArc = 0.08f;

    /// <summary>Режим работы сейчас идёт.</summary>
    public bool IsActive => mode != ActionMode.None;

    /// <summary>Кадр, в котором режим закончился: клик выхода (ПКМ) не должен в том же кадре бросить
    /// предмет из рук (PlayerItemInteraction это проверяет).</summary>
    public int LastEndFrame { get; private set; } = -1;

    // Ниже этого хода рычага между сменами направления скрип не играет — дрожь мыши не скрипит.
    private const float CreakMinTravel = 0.3f;

    private readonly Dictionary<Transform, (Vector3 position, Quaternion rotation)> restPoses =
        new Dictionary<Transform, (Vector3, Quaternion)>();

    private ActionMode mode;
    private CleanableStain stain;
    private Breakable breakable;
    private Breakable.PryFrame pryFrame;

    private Transform toolVisual;
    private Vector3 workPoint;
    private Quaternion workRotation;
    private float flight;
    private Vector3 flightFromPosition;
    private Quaternion flightFromRotation;
    private Tween returnTween;

    private float lever;
    private float lastLeverDirection;
    private float travelSinceCreak;
    private Vector3 leverAxis;
    private float ragWobblePhase;

    private void Awake()
    {
        if (config != null)
        {
            ToolSettings s = config.tools;
            ragSensitivity = s.ragSensitivity;
            leverSensitivity = s.leverSensitivity;
            leverAngle = s.leverAngle;
            pryTravelToBreak = s.pryTravelToBreak;
        }

        if (rag != null)
        {
            RememberRestPose(rag);
            rag.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (IsActive) EndAction();
    }

    // ───────────────────────── Вход ─────────────────────────

    /// <summary>Начать оттирать пятно: тряпка ложится туда, куда смотрит прицел.</summary>
    public bool TryBeginScrub(CleanableStain target, Ray aimRay)
    {
        if (IsActive || target == null || target.IsClean || rag == null || playerCamera == null) return false;

        target.Raycast(aimRay, out Vector3 point);
        stain = target;
        mode = ActionMode.Scrub;
        rag.gameObject.SetActive(true);
        StartAction(rag, stain.ClampToStain(point, 0f), RagRotation(stain.SurfaceNormal));
        ShowHint(Loc.Get("hud.stain.title"), Loc.Get("hud.mode.scrub"));
        return true;
    }

    /// <summary>Начать отжимать доску ломом: лапка заходит под конец доски, ближайший к прицелу.</summary>
    public bool TryBeginPry(Breakable target, Vector3 aimPoint)
    {
        if (IsActive || target == null || target.IsBroken || !target.canPry || playerCamera == null) return false;
        Transform crowbar = toolPresenter != null ? toolPresenter.GetVisual(ToolKind.Crowbar) : null;
        if (crowbar == null) return false;

        Transform cam = playerCamera.transform;
        pryFrame = target.GetPryFrame(aimPoint, cam.position);
        target.BeginPry(pryFrame);
        breakable = target;
        mode = ActionMode.Pry;
        lever = 0f;
        lastLeverDirection = 0f;
        travelSinceCreak = 0f;

        // Шест смотрит к рукам игрока (чуть ниже и впереди камеры), лапка загнута к стене (−normal).
        Vector3 hands = cam.position + cam.forward * 0.35f - cam.up * 0.3f;
        Vector3 shaft = (hands - pryFrame.point).normalized;
        workRotation = Quaternion.LookRotation(shaft, pryFrame.normal);
        leverAxis = Vector3.Cross(shaft, Vector3.up);
        if (leverAxis.sqrMagnitude < 0.01f) leverAxis = cam.right;
        leverAxis.Normalize();

        StartAction(crowbar, target.LiftPoint(pryFrame.point), workRotation);
        ShowHint(Loc.Get(target.titleKey), Loc.Get("hud.mode.pry"));
        return true;
    }

    private void StartAction(Transform visual, Vector3 point, Quaternion rotation)
    {
        returnTween?.Kill();
        RememberRestPose(visual);
        toolVisual = visual;
        workPoint = point;
        workRotation = rotation;
        flight = 0f;
        flightFromPosition = visual.position;
        flightFromRotation = visual.rotation;

        if (inputBlocker != null) inputBlocker.Acquire(this, false);
    }

    private void ShowHint(string title, string hint)
    {
        // После Acquire: блокировка выключает PlayerItemInteraction, а тот в OnDisable прячет подсказку.
        if (infoUI != null) infoUI.ShowHint(title, hint);
        if (progressUI != null) progressUI.SetProgress(mode == ActionMode.Scrub ? stain.Progress : breakable.PryProgress);
    }

    // ───────────────────────── Работа ─────────────────────────

    private void Update()
    {
        if (!IsActive) return;

        if (Cursor.lockState != CursorLockMode.Locked || !IsTargetValid() || Input.GetMouseButtonDown(1))
        {
            EndAction();
            return;
        }

        flight = Mathf.MoveTowards(flight, 1f, Time.deltaTime / flightDuration);
        if (mode == ActionMode.Scrub) UpdateScrub();
        else UpdatePry();
    }

    private bool IsTargetValid()
    {
        if (mode == ActionMode.Scrub) return stain != null && !stain.IsClean && stain.isActiveAndEnabled;
        return breakable != null && !breakable.IsBroken && breakable.isActiveAndEnabled
               && (modeController == null || modeController.ActiveToolKind == ToolKind.Crowbar);
    }

    private void UpdateScrub()
    {
        Vector3 normal = stain.SurfaceNormal;
        Vector3 forward = PlaneForward(normal);

        if (flight >= 1f)
        {
            Vector3 right = Vector3.ProjectOnPlane(playerCamera.transform.right, normal).normalized;
            Vector2 mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * ragSensitivity;
            Vector3 next = stain.ClampToStain(workPoint + right * mouse.x + forward * mouse.y, stain.brushRadius * 0.5f);

            ragWobblePhase += Vector3.Distance(workPoint, next) * 60f;
            bool cleaned = stain.ScrubSegment(workPoint, next);
            workPoint = next;
            if (progressUI != null) progressUI.SetProgress(stain.Progress);
            if (cleaned)
            {
                EndAction();
                return;
            }
        }

        // Тряпка чуть «елозит» — поворачивается туда-сюда по мере движения.
        Quaternion wobble = Quaternion.AngleAxis(Mathf.Sin(ragWobblePhase) * 10f, normal);
        ApplyToolPose(workPoint + normal * ragLift, wobble * RagRotation(normal));
    }

    private void UpdatePry()
    {
        if (flight >= 1f)
        {
            float next = Mathf.Clamp(lever + Input.GetAxis("Mouse Y") * leverSensitivity, -1f, 1f);
            float travel = Mathf.Abs(next - lever);
            if (travel > 0f)
            {
                float direction = Mathf.Sign(next - lever);
                travelSinceCreak += travel;
                if (lastLeverDirection != 0f && direction != lastLeverDirection && travelSinceCreak >= CreakMinTravel)
                {
                    breakable.PlayCreak(Mathf.Lerp(0.4f, 1f, breakable.PryProgress));
                    travelSinceCreak = 0f;
                }
                lastLeverDirection = direction;
                lever = next;

                if (breakable.AddPryProgress(travel / pryTravelToBreak))
                {
                    EndAction();
                    return;
                }
            }

            breakable.SetLever(lever);
            if (progressUI != null) progressUI.SetProgress(breakable.PryProgress);
        }

        // Лом поворачивается вокруг лапки: мышь вверх — рукоять вверх. Лапка едет вместе с концом доски.
        Quaternion leverRotation = Quaternion.AngleAxis(lever * leverAngle, leverAxis);
        ApplyToolPose(breakable.LiftPoint(pryFrame.point), leverRotation * workRotation);
    }

    /// <summary>Поза инструмента в работе; пока он летит из руки — по дуге от позы в руке.</summary>
    private void ApplyToolPose(Vector3 position, Quaternion rotation)
    {
        if (flight >= 1f)
        {
            toolVisual.SetPositionAndRotation(position, rotation);
            return;
        }

        float t = DOVirtual.EasedValue(0f, 1f, flight, Ease.OutCubic);
        Vector3 arc = Vector3.up * (flightArc * Mathf.Sin(t * Mathf.PI));
        toolVisual.SetPositionAndRotation(Vector3.LerpUnclamped(flightFromPosition, position, t) + arc,
                                          Quaternion.SlerpUnclamped(flightFromRotation, rotation, t));
    }

    /// <summary>«Вперёд» на плоскости: мышь вверх двигает тряпку от игрока (пол) или вверх (стена).</summary>
    private Vector3 PlaneForward(Vector3 normal)
    {
        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.up, normal);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, normal);
        return forward.normalized;
    }

    private Quaternion RagRotation(Vector3 normal)
    {
        return Quaternion.LookRotation(PlaneForward(normal), normal);
    }

    // ───────────────────────── Выход ─────────────────────────

    private void EndAction()
    {
        ActionMode ended = mode;
        mode = ActionMode.None;
        LastEndFrame = Time.frameCount;

        if (ended == ActionMode.Pry && breakable != null && !breakable.IsBroken) breakable.EndPry();
        if (inputBlocker != null) inputBlocker.Release(this);
        if (progressUI != null) progressUI.Hide();
        if (infoUI != null) infoUI.Hide();

        ReturnTool(ended == ActionMode.Scrub);
        stain = null;
        breakable = null;
    }

    /// <summary>Инструмент возвращается в руку; тряпка там прячется.</summary>
    private void ReturnTool(bool hideWhenBack)
    {
        Transform visual = toolVisual;
        toolVisual = null;
        if (visual == null || !restPoses.TryGetValue(visual, out var rest)) return;

        returnTween?.Kill();
        returnTween = DOTween.Sequence()
            .Join(visual.DOLocalMove(rest.position, flightDuration).SetEase(Ease.InOutCubic))
            .Join(visual.DOLocalRotateQuaternion(rest.rotation, flightDuration).SetEase(Ease.InOutCubic))
            .OnComplete(() =>
            {
                if (hideWhenBack && visual != null) visual.gameObject.SetActive(false);
            });
    }

    private void RememberRestPose(Transform visual)
    {
        if (visual != null && !restPoses.ContainsKey(visual))
            restPoses[visual] = (visual.localPosition, visual.localRotation);
    }
}
