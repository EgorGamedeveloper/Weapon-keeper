using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Чем объект сломали: сняли ломом целым или разбили кувалдой.</summary>
public enum BreakMode
{
    /// <summary>Лом: доску поддели и сняли целой — она падает там, где висела.</summary>
    Pry,
    /// <summary>Кувалда: объект разбит в щепки/осколки — обломки разлетаются.</summary>
    Smash,
}

/// <summary>
/// Объект, который убирают инструментом: прибитая доска на окне/двери, заложенный кирпичом проём,
/// ящик с оружием. Один и тот же компонент покрывает все сценарии через данные, без подклассов — что
/// именно происходит при поломке (открыть окно, включить свет и т.п.), решает отдельный сценарный
/// скрипт, подписанный на OnBroken (см. RevealOnBreak), сам Breakable об этом не знает.
///
/// Два инструмента:
/// - лом (canPry) — режим рычага (PlayerToolActions): лапка заходит под конец объекта по длинной оси,
///   игрок качает мышью, объект приподнимается вокруг противоположного конца (шарнира) и снимается
///   целым — spawnOnBreak падает на его месте;
/// - кувалда (canSmash) — удары (SledgehammerSwing): каждый удар трясёт объект, после hitPoints ударов
///   он разбит — обломки (spawnOnSmash, если пусто — spawnOnBreak) разлетаются.
/// Частичный прогресс (приподнятая доска, число ударов) в сейв не пишется: после загрузки объект снова
/// целый. Разобранный — сохраняется, как и раньше.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour
{
    [Tooltip("Целый вид объекта — выключается при поломке; его же приподнимает лом. Пусто — сам объект.")]
    public GameObject intactVisual;

    [Tooltip("Что выпадает при поломке: обломки-мусор (доски) ИЛИ лут (оружие/патроны из ящика). Лом кладёт " +
             "их на место объекта, кувалда (если spawnOnSmash пуст) — разбрасывает.")]
    public GameObject[] spawnOnBreak;

    [Tooltip("Что выпадает, если объект разбили кувалдой. Пусто — то же, что spawnOnBreak.")]
    public GameObject[] spawnOnSmash;

    [Tooltip("Случайный разброс точек спавна обломков по X/Z от позиции объекта (при ударе кувалдой).")]
    public Vector2 spawnScatterRadius = new Vector2(0.3f, 0.3f);

    [Tooltip("Спрятать весь объект при поломке (SetActive(false)). Если выключено — объект остаётся (без коллайдера), " +
             "просто прячет intactVisual. Объект сознательно не уничтожается: вместе с ним пропал бы его PersistentId, " +
             "и сейв не узнал бы, что он разобран, — после загрузки доска снова была бы целой, а её лут задвоился бы.")]
    [FormerlySerializedAs("destroySelf")]
    public bool hideSelfOnBreak = true;

    [Tooltip("Цвет подсветки (emission) при наведении с подходящим инструментом.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    [Tooltip("Ключ строки заголовка подсказки у прицела (strings.csv), например hud.breakable.board — «Прибитая доска».")]
    public string titleKey = "hud.breakable.board";

    [Header("Чем ломается")]
    [Tooltip("Можно поддеть и снять ломом (режим рычага).")]
    public bool canPry = true;

    [Tooltip("Можно разбить кувалдой.")]
    public bool canSmash = true;

    [Tooltip("Сколько ударов кувалдой выдерживает.")]
    [Min(1)] public int hitPoints = 2;

    [Tooltip("Насколько объект вздрагивает от удара кувалдой, градусы (доска — заметно, стена — чуть-чуть).")]
    [Range(0f, 10f)] public float hitShakeAngle = 4f;

    [Header("Лом")]
    [Tooltip("Куда заходит лапка лома: позиция — точка у кромки, ось Z — вдоль объекта наружу (от шарнира), " +
             "ось Y — от стены к игроку. Пусто — конец объекта по длинной оси, ближайший к прицелу.")]
    public Transform pryPoint;

    [Tooltip("На сколько градусов объект приподнимается к концу работы ломом (вокруг противоположного конца).")]
    [Range(0f, 45f)] public float maxLiftAngle = 10f;

    [Tooltip("Насколько объект «ходит» вместе с рычагом, градусы (растёт с прогрессом).")]
    [Range(0f, 10f)] public float leverWobbleAngle = 2.5f;

    [Header("Звуки")]
    [Tooltip("Скрип, когда рычаг меняет направление (громче к концу).")]
    public SoundCue creakSound;

    [Tooltip("Удар кувалдой, который объект выдержал.")]
    public SoundCue hitSound;

    [Tooltip("Объект снят или разбит.")]
    public SoundCue breakSound;

    [Header("Эффекты")]
    [Tooltip("Облачко пыли — при ударе кувалдой и при поломке (префаб с ParticleSystem, сам себя уничтожает).")]
    public GameObject dustEffect;

    [Tooltip("Щепки/осколки, когда объект разбит кувалдой. Пусто — только пыль.")]
    public GameObject smashEffect;

    /// <summary>Объект уже разобран.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Насколько объект уже отжат ломом, 0..1 (держится между заходами, в сейв не пишется).</summary>
    public float PryProgress { get; private set; }

    /// <summary>Сколько ударов кувалдой уже принято.</summary>
    public int HitsTaken { get; private set; }

    /// <summary>Объект разобран — хук для сценарных скриптов (открыть окно, включить свет и т.п.) и трекеров.</summary>
    public event Action<Breakable> OnBroken;

    /// <summary>Как лом держит объект: точка лапки, направление наружу вдоль объекта, нормаль к игроку,
    /// шарнир (противоположный конец, задняя грань) и ось подъёма.</summary>
    public struct PryFrame
    {
        public Vector3 point;
        public Vector3 outward;
        public Vector3 normal;
        public Vector3 hinge;
        public Vector3 liftAxis;
    }

    private Renderer[] renderers;
    private bool isHighlighted;
    private Transform liftTarget;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool hasRestPose;
    private PryFrame activeFrame;
    private float currentLiftAngle;
    private Tween poseTween;
    private Tween shakeTween;

    private Transform LiftTarget => liftTarget != null ? liftTarget : (liftTarget = intactVisual != null ? intactVisual.transform : transform);

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnDisable()
    {
        poseTween?.Kill();
        shakeTween?.Kill();
    }

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Включает/выключает подсветку — та же схема через emission, что и у WorldItem/CleanableStain.</summary>
    public void SetHighlight(bool state)
    {
        if (isHighlighted == state) return;
        isHighlighted = state;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (!mat.HasProperty("_EmissionColor")) continue;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", state ? highlightColor : Color.black);
            }
        }
    }

    // ───────────────────────── Лом ─────────────────────────

    /// <summary>
    /// Где у объекта работает лом, если игрок целится в aimPoint из viewer. Считается в позе покоя
    /// (без уже набранного подъёма): длинная ось, толщина и ширина — по границам меша в осях объекта.
    /// </summary>
    public PryFrame GetPryFrame(Vector3 aimPoint, Vector3 viewer)
    {
        EnsureRestPose();
        Transform target = LiftTarget;

        if (pryPoint != null)
        {
            // Шарнир — точка, симметричная лапке относительно центра объекта.
            Vector3 center = GetRestBounds(target, out _, out _, out _);
            Vector3 point = ToRest(pryPoint.position);
            Vector3 outward = ToRestDirection(pryPoint.forward);
            Vector3 normal = ToRestDirection(pryPoint.up);
            Vector3 hinge = center - (point - center);
            return new PryFrame { point = point, outward = outward, normal = normal, hinge = hinge, liftAxis = Vector3.Cross(outward, normal).normalized };
        }

        Vector3 c = GetRestBounds(target, out Vector3 longAxis, out Vector3 thinAxis, out Vector3 halfSize);
        float side = Vector3.Dot(aimPoint - c, longAxis) >= 0f ? 1f : -1f;
        Vector3 outwardDir = longAxis * side;
        Vector3 normalDir = Vector3.Dot(viewer - c, thinAxis) >= 0f ? thinAxis : -thinAxis;

        // Лапка — у кромки конца, чуть за передней гранью; шарнир — задняя грань противоположного конца.
        Vector3 end = c + outwardDir * halfSize.x;
        return new PryFrame
        {
            point = end - outwardDir * 0.03f - normalDir * (halfSize.y * 0.5f),
            outward = outwardDir,
            normal = normalDir,
            hinge = c - outwardDir * halfSize.x - normalDir * halfSize.y,
            liftAxis = Vector3.Cross(outwardDir, normalDir).normalized,
        };
    }

    /// <summary>Начать работу ломом в этой рамке (PlayerToolActions).</summary>
    public void BeginPry(PryFrame frame)
    {
        EnsureRestPose();
        activeFrame = frame;
        poseTween?.Kill();
    }

    /// <summary>Добавить прогресс рычага; при 1 — объект снят (Break(BreakMode.Pry)). Возвращает true, если снят.</summary>
    public bool AddPryProgress(float amount)
    {
        if (IsBroken || !canPry) return IsBroken;
        PryProgress = Mathf.Clamp01(PryProgress + Mathf.Max(0f, amount));
        if (PryProgress < 1f) return false;

        ApplyLift(0f, 0f);
        Break(BreakMode.Pry);
        return true;
    }

    /// <summary>Поза объекта под рычагом: подъём по прогрессу + покачивание вместе с рычагом (lever −1..1).</summary>
    public void SetLever(float lever)
    {
        if (IsBroken) return;
        float wobble = -lever * leverWobbleAngle * Mathf.Lerp(0.3f, 1f, PryProgress);
        ApplyLift(PryProgress * maxLiftAngle + wobble, 0f);
    }

    /// <summary>Рычаг отпущен: объект оседает на набранный подъём (прогресс остаётся).</summary>
    public void EndPry()
    {
        if (IsBroken) return;
        ApplyLift(PryProgress * maxLiftAngle, 0.15f);
    }

    public void PlayCreak(float volume) => SoundPlayer.Play(creakSound, activeFrame.point, volume);

    /// <summary>Где сейчас точка объекта, заданная в позе покоя (например, лапка лома): с учётом подъёма —
    /// лом едет вместе с отжимаемым концом.</summary>
    public Vector3 LiftPoint(Vector3 restPoint)
    {
        return activeFrame.hinge + Quaternion.AngleAxis(currentLiftAngle, activeFrame.liftAxis) * (restPoint - activeFrame.hinge);
    }

    private void ApplyLift(float angle, float duration)
    {
        if (!hasRestPose) return;
        Transform target = LiftTarget;
        angle = Mathf.Max(0f, angle);
        currentLiftAngle = angle;

        Quaternion rotation = Quaternion.AngleAxis(angle, activeFrame.liftAxis);
        Vector3 position = activeFrame.hinge + rotation * (restPosition - activeFrame.hinge);
        Quaternion finalRotation = rotation * restRotation;

        poseTween?.Kill();
        if (duration <= 0f)
        {
            target.SetPositionAndRotation(position, finalRotation);
            return;
        }
        poseTween = DOTween.Sequence()
            .Join(target.DOMove(position, duration))
            .Join(target.DORotateQuaternion(finalRotation, duration));
    }

    private void EnsureRestPose()
    {
        if (hasRestPose) return;
        hasRestPose = true;
        restPosition = LiftTarget.position;
        restRotation = LiftTarget.rotation;
    }

    // Текущую (приподнятую) позу переводим в позу покоя: рамка всегда считается без подъёма.
    private Vector3 ToRest(Vector3 world)
    {
        Transform target = LiftTarget;
        Vector3 local = target.InverseTransformPoint(world);
        return Matrix4x4.TRS(restPosition, restRotation, target.lossyScale).MultiplyPoint3x4(local);
    }

    private Vector3 ToRestDirection(Vector3 worldDirection)
    {
        Vector3 local = Quaternion.Inverse(LiftTarget.rotation) * worldDirection;
        return (restRotation * local).normalized;
    }

    /// <summary>Центр и оси объекта в позе покоя: длинная ось (x полуразмера), самая тонкая (y), средняя (z).</summary>
    private Vector3 GetRestBounds(Transform target, out Vector3 longAxis, out Vector3 thinAxis, out Vector3 halfSize)
    {
        Vector3[] axes = { restRotation * Vector3.right, restRotation * Vector3.up, restRotation * Vector3.forward };
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        foreach (var filter in target.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 offset = ToRest(filter.transform.TransformPoint(corner)) - restPosition;
                for (int a = 0; a < 3; a++)
                {
                    float d = Vector3.Dot(offset, axes[a]);
                    min[a] = Mathf.Min(min[a], d);
                    max[a] = Mathf.Max(max[a], d);
                }
                any = true;
            }
        }

        if (!any)
        {
            longAxis = axes[0];
            thinAxis = axes[1];
            halfSize = new Vector3(0.5f, 0.02f, 0.1f);
            return restPosition;
        }

        Vector3 size = max - min;
        int longIndex = 0, thinIndex = 0;
        for (int a = 1; a < 3; a++)
        {
            if (size[a] > size[longIndex]) longIndex = a;
            if (size[a] < size[thinIndex]) thinIndex = a;
        }
        int midIndex = 3 - longIndex - thinIndex;

        Vector3 center = restPosition;
        for (int a = 0; a < 3; a++) center += axes[a] * ((min[a] + max[a]) * 0.5f);

        longAxis = axes[longIndex];
        thinAxis = axes[thinIndex];
        halfSize = new Vector3(size[longIndex], size[thinIndex], size[midIndex]) * 0.5f;
        return center;
    }

    // ───────────────────────── Кувалда ─────────────────────────

    /// <summary>
    /// Удар кувалдой в точку point. Объект, который кувалдой не ломается, только звучит. Возвращает
    /// true, если удар его разбил.
    /// </summary>
    public bool Hit(Vector3 point)
    {
        if (IsBroken) return false;
        SpawnEffect(dustEffect, point);

        if (!canSmash)
        {
            SoundPlayer.Play(hitSound, point, 0.6f);
            return false;
        }

        HitsTaken++;
        if (HitsTaken >= hitPoints)
        {
            SpawnEffect(smashEffect, point);
            Break(BreakMode.Smash);
            return true;
        }

        SoundPlayer.Play(hitSound, point);
        shakeTween?.Complete();
        if (hitShakeAngle > 0f) shakeTween = LiftTarget.DOShakeRotation(0.25f, hitShakeAngle, 18, 90f);
        return false;
    }

    // ───────────────────────── Поломка ─────────────────────────

    /// <summary>Разобрать объект: звук, пыль, обломки (лом — на месте объекта, кувалда — разлетаются), событие.</summary>
    public void Break(BreakMode mode = BreakMode.Smash)
    {
        if (IsBroken) return;
        IsBroken = true;

        SetHighlight(false);
        poseTween?.Kill();
        shakeTween?.Kill();

        Transform target = LiftTarget;
        Vector3 center = GetVisualCenter();
        SoundPlayer.Play(breakSound, center);
        SpawnEffect(dustEffect, center);

        GameObject[] spawns = mode == BreakMode.Smash && spawnOnSmash != null && spawnOnSmash.Length > 0 ? spawnOnSmash : spawnOnBreak;
        for (int i = 0; i < spawns.Length; i++)
        {
            GameObject prefab = spawns[i];
            if (prefab == null) continue;
            if (mode == BreakMode.Pry)
            {
                // Снятая доска падает оттуда, где висела, чуть отходя от стены; несколько обломков —
                // слоями друг перед другом, чтобы не родиться друг в друге.
                Vector3 position = target.position + activeFrame.normal * (0.05f + i * 0.1f);
                GameObject spawned = Instantiate(prefab, position, target.rotation);
                Rigidbody body = spawned.GetComponent<Rigidbody>();
                if (body != null) body.linearVelocity = activeFrame.normal * 0.8f;
                continue;
            }

            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-spawnScatterRadius.x, spawnScatterRadius.x),
                0f,
                UnityEngine.Random.Range(-spawnScatterRadius.y, spawnScatterRadius.y));
            Instantiate(prefab, center + offset, UnityEngine.Random.rotation);
        }

        if (intactVisual != null) intactVisual.SetActive(false);

        OnBroken?.Invoke(this);

        ApplyBrokenVisual();
    }

    /// <summary>
    /// Восстановление из сейва: воспроизводит только визуальный итог поломки (прячет intactVisual,
    /// гасит коллайдеры) — БЕЗ спавна обломков (обломки/лут уже восстановлены отдельно, как
    /// обычные предметы мира), БЕЗ звука и БЕЗ события OnBroken (иначе сценарные скрипты вроде
    /// RevealOnBreak среагировали бы повторно при каждой загрузке).
    ///
    /// Итог тот же, что у Break(): RevealOnBreak и подобные скрипты держат ссылку на этот Breakable и
    /// перечитывают board.IsBroken в своём OnEnable — объект только прячется, не уничтожается.
    /// </summary>
    public void RestoreBroken()
    {
        if (IsBroken) return;
        IsBroken = true;

        if (intactVisual != null) intactVisual.SetActive(false);
        ApplyBrokenVisual();
    }

    private void ApplyBrokenVisual()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        if (hideSelfOnBreak) gameObject.SetActive(false);
    }

    /// <summary>Центр видимой части объекта (у проёма пивот стоит на полу — обломки не должны
    /// рождаться в полу).</summary>
    private Vector3 GetVisualCenter()
    {
        bool any = false;
        Bounds bounds = default;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any ? bounds.center : LiftTarget.position;
    }

    private static void SpawnEffect(GameObject prefab, Vector3 position)
    {
        if (prefab != null) Instantiate(prefab, position, Quaternion.identity);
    }
}
