using System;
using UnityEngine;

/// <summary>Роль разъёма: откуда ток идёт (генератор) и куда (двигатель лифта).</summary>
public enum WireSocketRole
{
    /// <summary>Отдаёт ток (генератор).</summary>
    Source,
    /// <summary>Принимает ток (двигатель лифта, щиток).</summary>
    Consumer,
}

/// <summary>
/// Разъём под провод. Провод (WireSpoolTool) соединяет источник с потребителем: потребитель запитан, когда
/// соединён с источником, а тот даёт ток (его точка ремонта powerSource починена или её нет). Кто
/// пользуется током, опрашивает HasPower (ElevatorPlatform.powerSocket) — так состояние верно и после
/// загрузки, без отдельного сохранения «запитан ли». Сохраняется само соединение (SaveGameData.wires).
///
/// Лампочка indicator показывает, есть ли ток: зелёная — есть, красная — нет. Триггер-коллайдер — чтобы
/// в разъём можно было прицелиться.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WireSocket : MonoBehaviour
{
    [Header("Разъём")]
    [Tooltip("Source — отдаёт ток (генератор), Consumer — принимает (двигатель лифта). Провод соединяет только разные роли.")]
    public WireSocketRole role = WireSocketRole.Consumer;

    [Tooltip("Ключ заголовка подсказки у прицела (strings.csv): hud.socket.generator, hud.socket.elevator.")]
    public string titleKey = "hud.socket.generator";

    [Tooltip("Куда входит вилка; её ось Z смотрит наружу из разъёма. Пусто — сам объект.")]
    public Transform plugPoint;

    [Header("Питание")]
    [Tooltip("Только у источника: точка ремонта, после починки которой он даёт ток (генератор). Пусто — ток есть всегда.")]
    public RepairPoint powerSource;

    [Header("Индикатор")]
    [Tooltip("Лампочка разъёма. Пусто — без лампочки.")]
    public Renderer indicator;

    [Tooltip("Цвет лампочки, когда ток есть (HDR — светится через Bloom).")]
    [ColorUsage(false, true)] public Color poweredColor = new Color(0.25f, 1.8f, 0.35f);

    [Tooltip("Цвет лампочки без тока.")]
    [ColorUsage(false, true)] public Color unpoweredColor = new Color(1.5f, 0.12f, 0.08f);

    [Header("Провод")]
    [Tooltip("Префаб провода — из него восстанавливается провод из сейва.")]
    public WireCable cablePrefab;

    /// <summary>С кем соединён проводом (null — свободен).</summary>
    public WireSocket ConnectedTo { get; private set; }

    /// <summary>Провод этого соединения.</summary>
    public WireCable Cable { get; private set; }

    public bool IsConnected => ConnectedTo != null;

    /// <summary>Есть ли ток: источник — если починен его генератор; потребитель — если соединён с таким источником.</summary>
    public bool HasPower => role == WireSocketRole.Source
        ? powerSource == null || powerSource.IsRepaired
        : ConnectedTo != null && ConnectedTo.role == WireSocketRole.Source && ConnectedTo.HasPower;

    /// <summary>Точка вилки.</summary>
    public Vector3 PlugPosition => (plugPoint != null ? plugPoint : transform).position;

    /// <summary>Направление наружу из разъёма — провод выходит по нему.</summary>
    public Vector3 PlugOutward => (plugPoint != null ? plugPoint : transform).forward;

    /// <summary>Провод подключён (поднимается на обоих концах; при загрузке сейва — нет).</summary>
    public event Action<WireSocket> OnConnected;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock block;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnEnable()
    {
        if (powerSource != null) powerSource.OnRepaired += HandleSourceRepaired;
    }

    private void OnDisable()
    {
        if (powerSource != null) powerSource.OnRepaired -= HandleSourceRepaired;
    }

    // Start, а не Awake: соединения и починку генератора SaveLoadService восстанавливает в своём Awake.
    private void Start() => RefreshIndicator();

    /// <summary>Можно ли соединить проводом с other: оба свободны и роли разные.</summary>
    public bool CanConnectTo(WireSocket other)
    {
        return other != null && other != this && !IsConnected && !other.IsConnected && other.role != role;
    }

    /// <summary>Соединить разъёмы проводом. raiseEvents = false — восстановление из сейва.</summary>
    public static bool Connect(WireSocket a, WireSocket b, WireCable cable, bool raiseEvents)
    {
        if (a == null || !a.CanConnectTo(b)) return false;

        a.ConnectedTo = b;
        b.ConnectedTo = a;
        a.Cable = cable;
        b.Cable = cable;
        a.RefreshIndicator();
        b.RefreshIndicator();

        if (!raiseEvents) return true;
        a.OnConnected?.Invoke(a);
        b.OnConnected?.Invoke(b);
        return true;
    }

    /// <summary>Восстановление из сейва: провод по сохранённым точкам и соединение без событий.</summary>
    public static void RestoreConnection(WireSocket source, WireSocket consumer, Vector3[] points)
    {
        if (source == null || consumer == null) return;
        WireCable prefab = source.cablePrefab != null ? source.cablePrefab : consumer.cablePrefab;
        WireCable cable = null;
        if (prefab != null && points != null && points.Length >= 2)
        {
            cable = Instantiate(prefab);
            cable.SetPoints(points);
        }
        Connect(source, consumer, cable, false);
    }

    private void HandleSourceRepaired(RepairPoint _)
    {
        RefreshIndicator();
        if (ConnectedTo != null) ConnectedTo.RefreshIndicator();
    }

    /// <summary>Перекрасить лампочку по наличию тока.</summary>
    public void RefreshIndicator()
    {
        if (indicator == null) return;
        block ??= new MaterialPropertyBlock();
        Color color = HasPower ? poweredColor : unpoweredColor;
        indicator.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionColorId, color);
        indicator.SetPropertyBlock(block);
    }
}
