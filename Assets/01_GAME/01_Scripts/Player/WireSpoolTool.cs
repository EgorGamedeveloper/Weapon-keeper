using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Протяжка провода с катушки (пока катушка — активный инструмент в экипировке).
///
/// ЛКМ по свободному разъёму (WireSocket) — вилка в разъёме, провод тянется от него к катушке в руке.
/// Провод ложится на пол там, где прошёл игрок: под ногами каждые crumbSpacing метров ставится точка,
/// поэтому провод не проходит сквозь стены. Вернулся к предыдущей точке — последняя убирается, провод
/// сматывается. Длина пути доходит до ItemData.wireLength — провод натягивается: игрока возвращает к
/// последней точке, звучит скрип. ЛКМ по свободному разъёму другой роли — провод закреплён, разъёмы
/// соединены, катушка израсходована (одна катушка — одно соединение). ПКМ или смена инструмента —
/// провод сматывается обратно в катушку.
///
/// Незаконченная протяжка не сохраняется: катушка до конца остаётся в экипировке.
/// </summary>
public class WireSpoolTool : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: из её центра — луч на разъёмы.")]
    public Camera playerCamera;

    [Tooltip("Режим инвентаря: провод ведётся, только пока катушка — активный инструмент.")]
    public PlayerInventoryModeController modeController;

    [Tooltip("Инвентарь экипировки: отсюда катушка расходуется.")]
    public EquipmentInventory equipmentInventory;

    [Tooltip("Модели инструментов в руке: из модели катушки выходит провод и крутится барабан.")]
    public ToolPresenter toolPresenter;

    [Tooltip("CharacterController игрока: натянутый провод не пускает его дальше.")]
    public CharacterController characterController;

    [Tooltip("Подсказки у прицела.")]
    public ItemInfoUI infoUI;

    [Tooltip("Кольцо у прицела: сколько провода осталось.")]
    public HoldProgressUI progressUI;

    [Tooltip("Префаб провода (LineRenderer + WireCable).")]
    public WireCable cablePrefab;

    [Tooltip("Выносливость: вымотанный игрок протяжку не начинает, законченное соединение утомляет как " +
             "установка детали. Пусто — без ограничений.")]
    public PlayerStamina stamina;

    [Header("Протяжка")]
    [Tooltip("Дальность, с которой можно подключить провод к разъёму, м.")]
    [Min(0.5f)] public float interactRange = 2.5f;

    [Tooltip("Через сколько метров пути под ногами ставится новая точка провода.")]
    [Min(0.1f)] public float crumbSpacing = 0.4f;

    [Tooltip("Насколько провод на полу приподнят над поверхностью, м.")]
    [Min(0f)] public float floorOffset = 0.015f;

    [Tooltip("Насколько провод выходит из разъёма наружу, прежде чем свиснуть к полу, м.")]
    [Min(0f)] public float plugOutOffset = 0.12f;

    [Tooltip("Что считается полом под ногами (игрока, инструмент и предметы не включать).")]
    public LayerMask floorLayers = ~((1 << 8) | (1 << 9) | (1 << 24) | (1 << 30));

    [Tooltip("По чему идёт луч на разъёмы (игрока и инструмент не включать).")]
    public LayerMask socketLayers = ~((1 << 8) | (1 << 9) | (1 << 30));

    [Header("Модель катушки")]
    [Tooltip("Имя дочернего объекта модели катушки, откуда выходит провод.")]
    public string cableExitName = "CableExit";

    [Tooltip("Имя дочернего объекта модели катушки, который крутится при размотке.")]
    public string reelName = "Reel";

    [Tooltip("Радиус намотки барабана, м: от него скорость вращения.")]
    [Min(0.01f)] public float reelRadius = 0.06f;

    [Header("Звуки")]
    [Tooltip("Вилка вошла в разъём (начало и конец протяжки).")]
    public SoundCue plugSound;

    [Tooltip("Провод смотан обратно (отмена).")]
    public SoundCue cancelSound;

    [Tooltip("Провод натянулся до конца.")]
    public SoundCue tensionSound;

    [Tooltip("Щелчок барабана — раз на метр размотанного провода.")]
    public SoundCue tickSound;

    /// <summary>Провод сейчас тянется.</summary>
    public bool IsDragging => startSocket != null;

    private const float TensionSoundInterval = 0.8f;
    private const float RetractDuration = 0.3f;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private readonly List<Vector3> crumbs = new List<Vector3>();
    private readonly List<Vector3> points = new List<Vector3>();
    private WireSocket startSocket;
    private WireCable cable;
    private float maxLength;
    private float lastLength;
    private float tickAccumulator;
    private float nextTensionSoundTime;
    private bool taut;

    private Transform spoolVisual;
    private Transform cableExit;
    private Transform reel;

    private void OnDisable()
    {
        if (IsDragging) Cancel();
    }

    private void Update()
    {
        WorldItem spool = ActiveSpool();
        if (spool == null)
        {
            if (IsDragging) Cancel();
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked) return;

        WireSocket hovered = RaycastSocket();
        if (!IsDragging) UpdateIdle(hovered, spool);
        else UpdateDragging(hovered, spool);
    }

    private void LateUpdate()
    {
        if (!IsDragging) return;
        // После передвижения игрока в этом кадре: точки пути, натяжение, перерисовка провода.
        UpdateCrumbs();
        ApplyLeash();
        RedrawDraggedCable();
    }

    private WorldItem ActiveSpool()
    {
        if (modeController == null || modeController.ActiveToolKind != ToolKind.WireSpool || equipmentInventory == null) return null;
        return equipmentInventory.ActiveItem;
    }

    // ───────────────────────── Состояния ─────────────────────────

    private void UpdateIdle(WireSocket hovered, WorldItem spool)
    {
        if (hovered == null) return;
        float length = spool.itemData != null ? spool.itemData.wireLength : 0f;

        if (hovered.IsConnected)
        {
            ShowHint(hovered, Loc.Get("hud.wire.connected"));
            return;
        }

        if (stamina != null && stamina.IsExhausted)
        {
            ShowHint(hovered, Loc.Get("hud.stamina.too_tired"));
            return;
        }

        ShowHint(hovered, Loc.Get("hud.wire.plug", Mathf.RoundToInt(length)));
        if (Input.GetMouseButtonDown(0) && length > 0f) Begin(hovered, length);
    }

    private void UpdateDragging(WireSocket hovered, WorldItem spool)
    {
        if (Input.GetMouseButtonDown(1))
        {
            Cancel();
            return;
        }

        float remaining = Mathf.Max(0f, maxLength - PathLength());
        if (progressUI != null) progressUI.SetProgress(maxLength > 0f ? remaining / maxLength : 0f);

        if (hovered != null && hovered != startSocket)
        {
            if (!startSocket.CanConnectTo(hovered))
            {
                ShowHint(hovered, Loc.Get(hovered.IsConnected ? "hud.wire.connected" : "hud.wire.incompatible"));
                return;
            }

            ShowHint(hovered, Loc.Get("hud.wire.finish"));
            if (Input.GetMouseButtonDown(0)) Finish(hovered, spool);
            return;
        }

        string text = taut ? Loc.Get("hud.wire.taut") : Loc.Get("hud.wire.dragging", Mathf.RoundToInt(remaining));
        if (infoUI != null) infoUI.ShowHint(Loc.Get("hud.wire.title"), text);
    }

    private void Begin(WireSocket socket, float length)
    {
        startSocket = socket;
        maxLength = length;
        taut = false;
        tickAccumulator = 0f;

        crumbs.Clear();
        crumbs.Add(FloorBelow(socket.PlugPosition + socket.PlugOutward * plugOutOffset));
        lastLength = PathLength();

        if (cablePrefab != null) cable = Instantiate(cablePrefab);
        SoundPlayer.Play(plugSound, socket.PlugPosition);
        RedrawDraggedCable();
    }

    private void Finish(WireSocket end, WorldItem spool)
    {
        // Опорные точки закреплённого провода: вилка → наружу → пол → путь игрока → пол у второго
        // разъёма → наружу → вилка. В сейве и в проводе порядок всегда «источник → потребитель».
        points.Clear();
        AddPlugPoints(startSocket, true);
        for (int i = 1; i < crumbs.Count; i++) points.Add(crumbs[i]);
        AddPlugPoints(end, false);
        if (startSocket.role == WireSocketRole.Consumer) points.Reverse();

        if (cable == null && cablePrefab != null) cable = Instantiate(cablePrefab);
        if (cable != null) cable.SetPoints(points);

        WireSocket source = startSocket.role == WireSocketRole.Source ? startSocket : end;
        WireSocket consumer = source == startSocket ? end : startSocket;
        WireSocket.Connect(source, consumer, cable, true);
        SoundPlayer.Play(plugSound, end.PlugPosition);
        if (stamina != null) stamina.WorkRepairPart();

        // Одна катушка — одно соединение: остаток провода пропадает вместе с катушкой.
        equipmentInventory.Remove(spool);
        Destroy(spool.gameObject);

        cable = null;
        startSocket = null;
        crumbs.Clear();
        if (progressUI != null) progressUI.Hide();
        if (infoUI != null) infoUI.Hide();
    }

    /// <summary>Отмена: провод быстро сматывается к катушке и исчезает, катушка остаётся целой.</summary>
    private void Cancel()
    {
        WireCable retracting = cable;
        var path = new List<Vector3>(points);
        cable = null;
        startSocket = null;
        crumbs.Clear();
        taut = false;
        if (progressUI != null) progressUI.Hide();
        SoundPlayer.Play(cancelSound, transform.position);

        if (retracting == null) return;
        if (path.Count < 2)
        {
            Destroy(retracting.gameObject);
            return;
        }

        // Провод укорачивается со стороны разъёма к руке.
        float total = WireCable.Length(path);
        DOVirtual.Float(1f, 0f, RetractDuration, fraction =>
            {
                if (retracting != null) retracting.SetPoints(Tail(path, total * fraction));
            })
            .SetEase(Ease.InQuad)
            .OnComplete(() => { if (retracting != null) Destroy(retracting.gameObject); });
    }

    // ───────────────────────── Путь провода ─────────────────────────

    /// <summary>Точки под ногами: новая — когда игрок отошёл на crumbSpacing; вернулся к предпоследней —
    /// последняя убирается (провод сматывается). Новая точка не ставится, если провода уже не хватит.</summary>
    private void UpdateCrumbs()
    {
        Vector3 feet = FeetPoint();

        while (crumbs.Count >= 2 && HorizontalDistance(feet, crumbs[crumbs.Count - 2]) < crumbSpacing * 0.8f)
            crumbs.RemoveAt(crumbs.Count - 1);

        Vector3 last = crumbs[crumbs.Count - 1];
        if (HorizontalDistance(feet, last) < crumbSpacing) return;
        if (CrumbsLength() + Vector3.Distance(last, feet) > maxLength) return;
        crumbs.Add(feet);
    }

    /// <summary>Провод кончился — игрока возвращает к последней точке на лишнее расстояние.</summary>
    private void ApplyLeash()
    {
        float excess = PathLength() - maxLength;
        taut = excess > 0.01f;
        if (!taut || characterController == null) return;

        Vector3 feet = FeetPoint();
        Vector3 toAnchor = crumbs[crumbs.Count - 1] - feet;
        toAnchor.y = 0f;
        float distance = toAnchor.magnitude;
        if (distance > 0.001f) characterController.Move(toAnchor / distance * Mathf.Min(excess, distance));

        if (Time.time >= nextTensionSoundTime)
        {
            SoundPlayer.Play(tensionSound, crumbs[crumbs.Count - 1]);
            nextTensionSoundTime = Time.time + TensionSoundInterval;
        }
    }

    /// <summary>Длина провода: вилка → пол → точки пути → до игрока.</summary>
    private float PathLength()
    {
        if (startSocket == null || crumbs.Count == 0) return 0f;
        return CrumbsLength() + HorizontalDistance(crumbs[crumbs.Count - 1], FeetPoint());
    }

    private float CrumbsLength()
    {
        float length = Vector3.Distance(startSocket.PlugPosition, crumbs[0]);
        for (int i = 1; i < crumbs.Count; i++) length += Vector3.Distance(crumbs[i - 1], crumbs[i]);
        return length;
    }

    private void RedrawDraggedCable()
    {
        if (startSocket == null) return;

        points.Clear();
        AddPlugPoints(startSocket, true);
        for (int i = 1; i < crumbs.Count; i++) points.Add(crumbs[i]);
        Vector3 feet = FeetPoint();
        if (HorizontalDistance(feet, points[points.Count - 1]) > 0.05f) points.Add(feet);

        Transform exit = FindCableExit();
        if (exit != null) points.Add(exit.position);
        if (cable != null) cable.SetPoints(points);

        // Барабан крутится на размотанную/смотанную длину, щелчок — раз на метр.
        float length = PathLength();
        float delta = length - lastLength;
        lastLength = length;
        if (reel != null && Mathf.Abs(delta) > 0f) reel.Rotate(Vector3.right, delta / reelRadius * Mathf.Rad2Deg, Space.Self);
        tickAccumulator += Mathf.Abs(delta);
        if (tickAccumulator >= 1f)
        {
            tickAccumulator = 0f;
            SoundPlayer.Play(tickSound, exit != null ? exit.position : transform.position);
        }
    }

    /// <summary>Концевые точки у разъёма: вилка, выход наружу и пол под ним. fromSocket — порядок «от разъёма»
    /// (начало провода), иначе «к разъёму» (конец).</summary>
    private void AddPlugPoints(WireSocket socket, bool fromSocket)
    {
        Vector3 plug = socket.PlugPosition;
        Vector3 outPoint = plug + socket.PlugOutward * plugOutOffset;
        Vector3 floor = FloorBelow(outPoint);
        if (fromSocket)
        {
            points.Add(plug);
            points.Add(outPoint);
            points.Add(floor);
        }
        else
        {
            points.Add(floor);
            points.Add(outPoint);
            points.Add(plug);
        }
    }

    /// <summary>Начало пути длиной length, считая с конца (со стороны руки) — для сматывания.</summary>
    private static List<Vector3> Tail(List<Vector3> path, float length)
    {
        var result = new List<Vector3> { path[path.Count - 1] };
        float left = length;
        for (int i = path.Count - 1; i > 0 && left > 0f; i--)
        {
            float segment = Vector3.Distance(path[i], path[i - 1]);
            if (segment >= left)
            {
                result.Add(Vector3.Lerp(path[i], path[i - 1], left / segment));
                break;
            }
            result.Add(path[i - 1]);
            left -= segment;
        }
        result.Reverse();
        if (result.Count < 2) result.Add(result[0]);
        return result;
    }

    // ───────────────────────── Геометрия ─────────────────────────

    private WireSocket RaycastSocket()
    {
        if (playerCamera == null) return null;
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, Hits, interactRange, socketLayers, QueryTriggerInteraction.Collide);

        WireSocket best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.distance >= bestDistance) continue;
            WireSocket socket = hit.collider.GetComponentInParent<WireSocket>();
            // Посторонние триггеры не загораживают разъём; сплошная стена — загораживает.
            if (socket == null && hit.collider.isTrigger) continue;
            best = socket;
            bestDistance = hit.distance;
        }
        return best;
    }

    private Vector3 FeetPoint()
    {
        Vector3 position = characterController != null ? characterController.transform.position : transform.position;
        return FloorBelow(position + Vector3.up * 0.5f);
    }

    private Vector3 FloorBelow(Vector3 from)
    {
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 5f, floorLayers, QueryTriggerInteraction.Ignore))
            return hit.point + hit.normal * floorOffset;
        return from;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Transform FindCableExit()
    {
        Transform visual = toolPresenter != null ? toolPresenter.GetVisual(ToolKind.WireSpool) : null;
        if (visual != spoolVisual)
        {
            spoolVisual = visual;
            cableExit = null;
            reel = null;
            if (visual != null)
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == cableExitName) cableExit = child;
                    else if (child.name == reelName) reel = child;
                }
        }
        return cableExit != null ? cableExit : spoolVisual;
    }

    private void ShowHint(WireSocket socket, string text)
    {
        if (infoUI != null) infoUI.ShowHint(Loc.Get(socket.titleKey), text);
    }
}
