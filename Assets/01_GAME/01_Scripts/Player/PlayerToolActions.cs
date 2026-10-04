using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Режим работы с объектом: игрок «встаёт» к объекту и работает руками, а мышь управляет не камерой,
/// а инструментом.
/// - Тряпка или швабра по пятну (CleanableStain): тряпка (есть всегда) или головка швабры (если швабра —
///   активный инструмент) переносится из руки на пятно, мышь водит её по плоскости пятна, и оно стирается
///   там, где она прошла. Швабра шире тряпки и моет средние пятна (CleanableStain.CanCleanWith).
///   Камера чуть приближается (FOV) и слегка следит за тряпкой; после конца работы она плавно
///   возвращается, и только тогда игроку отдаётся мышь и ходьба. Тряпка и швабра без анимации
///   разворачиваются по траектории, как танк: плавно поворачиваются к усреднённому направлению движения,
///   назад едут «задним ходом», не разворачиваясь.
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

    [Tooltip("Выносливость: рычаг и тряпка тратят бар по ходу мыши и утомляют по прогрессу; при одышке " +
             "инструмент стоит, вымотанный игрок работу не начинает. Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Header("Конфиг")]
    [Tooltip("Если задан — чувствительность и параметры рычага берутся из GameConfig.tools при старте.")]
    public GameConfig config;

    [Header("Тряпка")]
    [Tooltip("Насколько тряпка сдвигается по пятну на единицу движения мыши, м.")]
    [Min(0.0001f)] public float ragSensitivity = 0.012f;

    [Tooltip("Насколько тряпка приподнята над поверхностью пятна, м.")]
    [Min(0f)] public float ragLift = 0.012f;

    [Tooltip("Радиус тряпки, м.")]
    [Min(0.01f)] public float ragRadius = 0.11f;

    [Tooltip("Сколько альфы пятна снимает один штамп тряпки в центре.")]
    [Range(0.01f, 1f)] public float ragStrength = 0.14f;

    [Tooltip("Досягаемость тряпки и швабры: радиус вокруг игрока по горизонтали, м. Перекрывается " +
             "GameConfig.tools.scrubReach, если задан конфиг.")]
    [Min(0.3f)] public float scrubReach = 1.6f;

    [Header("Швабра")]
    [Tooltip("Радиус головки швабры, м.")]
    [Min(0.01f)] public float mopRadius = 0.2f;

    [Tooltip("Сколько альфы пятна снимает один штамп швабры в центре.")]
    [Range(0.01f, 1f)] public float mopStrength = 0.09f;

    [Tooltip("Шорох швабры (у тряпки — звук самого пятна).")]
    public SoundCue mopScrubSound;

    [Header("Разворот тряпки и швабры")]
    [Tooltip("Наибольшая скорость разворота тряпки и швабры по направлению движения, градусы в секунду.")]
    [FormerlySerializedAs("mopTurnSpeed")]
    [Min(1f)] public float scrubTurnSpeed = 220f;

    [Tooltip("Насколько тряпка и швабра могут развернуться от направления «от игрока», градусы: черенок " +
             "швабры всегда остаётся с его стороны.")]
    [FormerlySerializedAs("mopMaxYaw")]
    [Range(0f, 90f)] public float scrubMaxYaw = 70f;

    [Tooltip("На каком пути, м, усредняется направление движения: мелкая дрожь мыши не дёргает поворот.")]
    [Min(0.001f)] public float steerSmoothing = 0.08f;

    [Tooltip("Время сглаживания поворота, с: больше — плавнее и ленивее.")]
    [Min(0.01f)] public float turnSmoothTime = 0.15f;

    [Header("Камера при оттирании")]
    [Tooltip("Приближение на время оттирания: множитель угла обзора (1 — без зума).")]
    [Range(0.3f, 1f)] public float scrubZoom = 0.8f;

    [Tooltip("Насколько взгляд следует за тряпкой: 0 — камера стоит, 1 — смотрит прямо на тряпку.")]
    [Range(0f, 1f)] public float scrubFollow = 0.3f;

    [Tooltip("Плавность зума и слежения: больше — быстрее.")]
    [Min(0.1f)] public float cameraSharpness = 6f;

    [Tooltip("За сколько секунд камера возвращается после оттирания (управление отдаётся после этого).")]
    [Min(0.01f)] public float cameraReturnDuration = 0.35f;

    [Header("Лом")]
    [Tooltip("Насколько рычаг сдвигается на единицу движения мыши по вертикали (весь ход — от −1 до 1).")]
    [Min(0.001f)] public float leverSensitivity = 0.08f;

    [Tooltip("Угол качания лома вокруг лапки на краю хода рычага, градусы.")]
    [Range(1f, 45f)] public float leverAngle = 14f;

    [Tooltip("Суммарный ход рычага, чтобы снять доску. Полный качок вверх-вниз — 4, упор в край ход не даёт.")]
    [Min(0.5f)] public float pryTravelToBreak = 14f;

    [Header("Свет на инструменте")]
    [Tooltip("Слой модели инструмента, пока он работает на объекте. В руке модели лежат на слое Tools, который " +
             "фонарик не освещает (иначе вблизи пересвечивает); на пятне или доске инструмент должен освещаться " +
             "как всё вокруг. После возврата в руку слой восстанавливается.")]
    public int workLayer = 0;

    [Header("Перенос инструмента")]
    [Tooltip("За сколько секунд тряпка или лом долетает из руки до объекта и обратно.")]
    [Min(0.01f)] public float flightDuration = 0.22f;

    [Tooltip("Высота дуги перелёта, м.")]
    [Min(0f)] public float flightArc = 0.08f;

    /// <summary>Режим работы сейчас идёт.</summary>
    public bool IsActive => mode != ActionMode.None;

    /// <summary>Режим закончился, но камера ещё возвращается — ввод игроку пока не отдан.</summary>
    public bool IsCameraReturning => cameraTween != null && cameraTween.IsActive();

    /// <summary>Кадр, в котором режим закончился: клик выхода (ПКМ) не должен в том же кадре бросить
    /// предмет из рук (PlayerItemInteraction это проверяет).</summary>
    public int LastEndFrame { get; private set; } = -1;

    // Ниже этого хода рычага между сменами направления скрип не играет — дрожь мыши не скрипит.
    private const float CreakMinTravel = 0.3f;

    private readonly Dictionary<Transform, (Vector3 position, Quaternion rotation)> restPoses =
        new Dictionary<Transform, (Vector3, Quaternion)>();

    // Исходные слои объектов модели инструмента (в руке) — возвращаются, когда инструмент снова в руке.
    private readonly Dictionary<Transform, (GameObject go, int layer)[]> restLayers =
        new Dictionary<Transform, (GameObject, int)[]>();

    private ActionMode mode;
    private CleanableStain stain;
    private ToolKind scrubTool;
    private float brushRadius;
    private float brushStrength;
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
    private string workHint;
    private bool windedHintShown;
    // Разворот инструмента по траектории: сглаженное направление пути, цель и текущий угол.
    private Vector3 travelDirection;
    private float toolYaw;
    private float yawTarget;
    private float yawVelocity;
    // Точка, за которой следит камера: сглаженное положение инструмента (без дрожи мыши).
    private Vector3 lookPoint;

    // Камера на время оттирания: исходные FOV и поворот, базис осей (на момент начала — мышь водит
    // тряпку в этих осях, а не в осях подвижной камеры).
    private bool cameraEngaged;
    private float baseFov;
    private Quaternion baseCameraLocalRotation;
    private Quaternion workBasis;
    private Tween cameraTween;

    private void Awake()
    {
        if (config != null)
        {
            ToolSettings s = config.tools;
            ragSensitivity = s.ragSensitivity;
            ragRadius = s.ragRadius;
            ragStrength = s.ragStrength;
            scrubReach = s.scrubReach;
            mopRadius = s.mopRadius;
            mopStrength = s.mopStrength;
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
        // Выключили посреди возврата камеры — вернуть сразу и отдать ввод.
        if (IsCameraReturning) cameraTween.Complete();
    }

    // ───────────────────────── Вход ─────────────────────────

    /// <summary>Точка на пятне в пределах досягаемости: по горизонтали от ног игрока не дальше scrubReach.</summary>
    public bool IsWithinReach(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= scrubReach * scrubReach;
    }

    /// <summary>Инструмент, которым сейчас трут пятна: швабра, если она в руках, иначе тряпка (None).</summary>
    public ToolKind CurrentScrubTool =>
        modeController != null && modeController.ActiveToolKind == ToolKind.Mop ? ToolKind.Mop : ToolKind.None;

    /// <summary>Начать оттирать пятно: тряпка или головка швабры ложится туда, куда смотрит прицел.
    /// false — пятно этим инструментом не отмыть (размер, CleanableStain.CanCleanWith).</summary>
    public bool TryBeginScrub(CleanableStain target, Ray aimRay)
    {
        if (IsActive || IsCameraReturning || target == null || target.IsClean || playerCamera == null) return false;
        if (stamina != null && stamina.IsExhausted) return false;

        ToolKind tool = CurrentScrubTool;
        if (!target.CanCleanWith(tool)) return false;
        Transform visual = tool == ToolKind.Mop ? (toolPresenter != null ? toolPresenter.GetVisual(ToolKind.Mop) : null) : rag;
        if (visual == null) return false;

        if (!target.Raycast(aimRay, out Vector3 point) || !IsWithinReach(point)) return false;
        stain = target;
        scrubTool = tool;
        brushRadius = tool == ToolKind.Mop ? mopRadius : ragRadius;
        brushStrength = tool == ToolKind.Mop ? mopStrength : ragStrength;
        mode = ActionMode.Scrub;
        toolYaw = yawTarget = yawVelocity = 0f;
        travelDirection = Vector3.zero;
        EngageCamera();
        visual.gameObject.SetActive(true);
        StartAction(visual, stain.ClampToStain(point, 0f), RagRotation(stain.SurfaceNormal));
        lookPoint = workPoint;
        ShowHint(Loc.Get(stain.titleKey), Loc.Get("hud.mode.scrub"));
        return true;
    }

    /// <summary>Начать отжимать доску ломом: лапка заходит под конец доски, ближайший к прицелу.</summary>
    public bool TryBeginPry(Breakable target, Vector3 aimPoint)
    {
        if (IsActive || IsCameraReturning || target == null || target.IsBroken || !target.canPry || playerCamera == null) return false;
        if (stamina != null && stamina.IsExhausted) return false;
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
        SetWorkLayer(visual, true);
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
        workHint = hint;
        windedHintShown = false;

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
        UpdateWindedHint();
        if (mode == ActionMode.Scrub) UpdateScrub();
        else UpdatePry();
    }

    /// <summary>Есть ли силы двигать инструмент: при одышке он стоит, пока бар не восстановится.</summary>
    private bool CanMoveTool => stamina == null || stamina.CanExert;

    /// <summary>Одышка посреди работы: подсказка «Отдышитесь», потом — снова обычная.</summary>
    private void UpdateWindedHint()
    {
        if (infoUI == null || stamina == null) return;
        bool winded = !stamina.CanExert;
        if (winded == windedHintShown) return;
        windedHintShown = winded;

        string title = mode == ActionMode.Scrub ? Loc.Get(stain.titleKey) : Loc.Get(breakable.titleKey);
        infoUI.ShowHint(title, winded ? Loc.Get("hud.mode.winded") : workHint);
    }

    private bool IsTargetValid()
    {
        if (mode == ActionMode.Scrub)
            return stain != null && !stain.IsClean && stain.isActiveAndEnabled && CurrentScrubTool == scrubTool;
        return breakable != null && !breakable.IsBroken && breakable.isActiveAndEnabled
               && (modeController == null || modeController.ActiveToolKind == ToolKind.Crowbar);
    }

    private void UpdateScrub()
    {
        Vector3 normal = stain.SurfaceNormal;
        Vector3 forward = PlaneForward(normal);

        if (flight >= 1f && CanMoveTool)
        {
            Vector3 right = Vector3.ProjectOnPlane(Basis * Vector3.right, normal).normalized;
            Vector2 mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * ragSensitivity;
            Vector3 next = stain.ClampToStain(workPoint + right * mouse.x + forward * mouse.y, brushRadius * 0.5f);

            float moved = Vector3.Distance(workPoint, next);
            SteerTool(next - workPoint, forward, normal);
            float progressBefore = stain.Progress;
            bool cleaned = stain.ScrubSegment(workPoint, next, brushRadius, brushStrength,
                                              scrubTool == ToolKind.Mop ? mopScrubSound : null);
            if (stamina != null) stamina.WorkScrub(moved, stain.Progress - progressBefore);
            workPoint = next;
            if (progressUI != null) progressUI.SetProgress(stain.Progress);
            if (cleaned)
            {
                EndAction();
                return;
            }
        }

        // Без анимаций: тряпка и швабра только разворачиваются по траектории (SteerTool), плавно.
        toolYaw = Mathf.SmoothDampAngle(toolYaw, yawTarget, ref yawVelocity, turnSmoothTime, scrubTurnSpeed);
        ApplyToolPose(workPoint + normal * ragLift, Quaternion.AngleAxis(toolYaw, normal) * RagRotation(normal));
        UpdateScrubCamera();
    }

    /// <summary>
    /// Тряпка и швабра как танк: разворачиваются к направлению движения. Направление усредняется по пути
    /// (steerSmoothing) — дрожь мыши и короткие рывки его не дёргают; пока направление неуверенное (мышь
    /// мечется), цель не меняется. Движение назад — «задний ход»: на противоположное направление цель
    /// переходит, только если оно заметно ближе к нынешнему углу. Поворот ограничен scrubMaxYaw.
    /// </summary>
    private void SteerTool(Vector3 delta, Vector3 forward, Vector3 normal)
    {
        delta = Vector3.ProjectOnPlane(delta, normal);
        float length = delta.magnitude;
        if (length < 1e-5f) return;

        travelDirection = Vector3.Lerp(travelDirection, delta / length, Mathf.Clamp01(length / steerSmoothing));
        if (travelDirection.magnitude < 0.6f) return;

        float ahead = Vector3.SignedAngle(forward, travelDirection, normal);
        float reverse = Mathf.DeltaAngle(0f, ahead + 180f);
        float target = Mathf.Abs(Mathf.DeltaAngle(yawTarget, reverse)) + 20f < Mathf.Abs(Mathf.DeltaAngle(yawTarget, ahead))
            ? reverse : ahead;
        yawTarget = Mathf.Clamp(target, -scrubMaxYaw, scrubMaxYaw);
    }

    // ───────────────────────── Камера при оттирании ─────────────────────────

    private void EngageCamera()
    {
        Transform cam = playerCamera.transform;
        if (!cameraEngaged)
        {
            baseFov = playerCamera.fieldOfView;
            baseCameraLocalRotation = cam.localRotation;
        }
        cameraTween?.Kill();
        cameraEngaged = true;
        workBasis = cam.rotation;
    }

    /// <summary>Зум к пятну и лёгкое слежение взгляда за тряпкой.</summary>
    private void UpdateScrubCamera()
    {
        if (!cameraEngaged) return;
        Transform cam = playerCamera.transform;
        float k = 1f - Mathf.Exp(-cameraSharpness * Time.deltaTime);

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFov * scrubZoom, k);

        Vector3 baseForward = workBasis * Vector3.forward;
        // Камера смотрит на сглаженную точку: следует за тряпкой плавно и не трясётся от дрожи мыши.
        lookPoint = Vector3.Lerp(lookPoint, workPoint, 1f - Mathf.Exp(-3f * Time.deltaTime));
        Vector3 toTool = lookPoint - cam.position;
        Vector3 look = Vector3.Slerp(baseForward, toTool.normalized, scrubFollow);
        Quaternion world = Quaternion.LookRotation(look, workBasis * Vector3.up);
        Quaternion local = cam.parent != null ? Quaternion.Inverse(cam.parent.rotation) * world : world;
        cam.localRotation = Quaternion.Slerp(cam.localRotation, local, k);
    }

    /// <summary>Вернуть зум и взгляд; ввод отдаётся игроку только когда камера на месте.</summary>
    private void ReturnCamera()
    {
        Transform cam = playerCamera.transform;
        cameraTween?.Kill();
        cameraTween = DOTween.Sequence()
            .Join(cam.DOLocalRotateQuaternion(baseCameraLocalRotation, cameraReturnDuration).SetEase(Ease.InOutSine))
            .Join(DOTween.To(() => playerCamera.fieldOfView, v => playerCamera.fieldOfView = v, baseFov, cameraReturnDuration)
                         .SetEase(Ease.InOutSine))
            // Без масштаба времени: выход в паузу (timeScale 0) не должен оставить камеру и ввод «застрявшими».
            .SetUpdate(true)
            .OnComplete(() =>
            {
                cameraEngaged = false;
                cameraTween = null;
                ReleaseInput();
            });
    }

    private void ReleaseInput()
    {
        if (inputBlocker != null) inputBlocker.Release(this);
        LastEndFrame = Time.frameCount;
    }

    private void UpdatePry()
    {
        if (flight >= 1f && CanMoveTool)
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

                float boardFraction = travel / pryTravelToBreak;
                if (stamina != null) stamina.WorkPry(boardFraction);
                if (breakable.AddPryProgress(boardFraction))
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

    /// <summary>Оси, в которых мышь водит инструмент: при оттирании — оси камеры на момент начала (сама
    /// камера в это время следит за тряпкой), иначе — нынешние.</summary>
    private Quaternion Basis => cameraEngaged ? workBasis : playerCamera.transform.rotation;

    /// <summary>«Вперёд» на плоскости: мышь вверх двигает тряпку от игрока (пол) или вверх (стена).</summary>
    private Vector3 PlaneForward(Vector3 normal)
    {
        Vector3 forward = Vector3.ProjectOnPlane(Basis * Vector3.up, normal);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Basis * Vector3.forward, normal);
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
        // После оттирания камера сначала возвращается, и только потом ввод отдаётся игроку.
        if (cameraEngaged && playerCamera != null && isActiveAndEnabled) ReturnCamera();
        else
        {
            if (cameraEngaged && playerCamera != null)
            {
                playerCamera.transform.localRotation = baseCameraLocalRotation;
                playerCamera.fieldOfView = baseFov;
                cameraEngaged = false;
            }
            if (inputBlocker != null) inputBlocker.Release(this);
        }
        if (progressUI != null) progressUI.Hide();
        if (infoUI != null) infoUI.Hide();

        // Тряпка живёт в руке только на время оттирания; швабра и лом — инструменты, остаются видны.
        ReturnTool(ended == ActionMode.Scrub && scrubTool != ToolKind.Mop);
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
                SetWorkLayer(visual, false);
                if (hideWhenBack && visual != null) visual.gameObject.SetActive(false);
            });
    }

    /// <summary>На объекте инструмент — на workLayer (его освещает фонарик), в руке — на своих слоях.</summary>
    private void SetWorkLayer(Transform visual, bool working)
    {
        if (visual == null) return;
        if (!restLayers.TryGetValue(visual, out var layers))
        {
            Transform[] all = visual.GetComponentsInChildren<Transform>(true);
            layers = new (GameObject, int)[all.Length];
            for (int i = 0; i < all.Length; i++) layers[i] = (all[i].gameObject, all[i].gameObject.layer);
            restLayers[visual] = layers;
        }
        foreach (var (go, layer) in layers)
            if (go != null) go.layer = working ? workLayer : layer;
    }

    private void RememberRestPose(Transform visual)
    {
        if (visual != null && !restPoses.ContainsKey(visual))
            restPoses[visual] = (visual.localPosition, visual.localRotation);
    }
}
