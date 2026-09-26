using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Кирпичная кладка, которую разбивают кувалдой по кирпичу (пример — заложенный проём
/// BrickedDoorway). Удар (SledgehammerSwing) приходит сюда, а не в Breakable.Hit:
/// - задетым считается кирпич, ближайший к точке удара в плоскости кладки;
/// - первые удары сдвигают кирпич вглубь, последний (hitsToKnockOut) выбивает его: целый кирпич
///   вылетает от игрока и становится обычным предметом-кирпичом (его можно подобрать), половинка у края
///   рассыпается в пыль. Кирпич рядом с уже выбитым держится слабее — вылетает с одного удара;
/// - когда выбито knockoutsToCollapse кирпичей, следующий удар обрушивает всю кладку: оставшиеся
///   кирпичи рассыпаются пылью и крошкой (предметами не становятся), а Breakable разбит (событие,
///   статистика, сейв «сломан» — как у любого Breakable; spawnOnBreak у такой кладки пуст).
///
/// Какие кирпичи выбиты, хранится в сейве (SaveGameData.brickWalls): выбитые кирпичи уже лежат в мире
/// как предметы и сохраняются сами, и если бы стена после загрузки снова была целой, кирпичи можно
/// было бы «добывать» бесконечно. Сдвинутые, но не выбитые кирпичи в сейв не пишутся.
/// </summary>
[RequireComponent(typeof(Breakable))]
public class BrickWallSmash : MonoBehaviour
{
    [Header("Кладка")]
    [Tooltip("Родитель кирпичей: кирпич — прямой ребёнок с MeshRenderer (куб, размер = масштаб). Пусто — этот объект.")]
    public Transform bricksRoot;

    [Tooltip("Предмет «кирпич», которым становится выбитый целый кирпич.")]
    public GameObject brickItemPrefab;

    [Tooltip("Кирпич короче этого (половинка у края) при выбивании рассыпается в пыль, а не становится предметом, м.")]
    [Min(0f)] public float minItemLength = 0.2f;

    [Header("Удары")]
    [Tooltip("Сколько ударов выдерживает кирпич: первые сдвигают его вглубь, последний выбивает.")]
    [Min(1)] public int hitsToKnockOut = 2;

    [Tooltip("Сколько кирпичей нужно выбить, чтобы следующий удар обрушил всю кладку.")]
    [Min(1)] public int knockoutsToCollapse = 3;

    [Tooltip("Дальше этого от точки удара (в плоскости кладки) кирпич не считается задетым, м.")]
    [Min(0.01f)] public float hitRadius = 0.2f;

    [Header("Движение кирпичей")]
    [Tooltip("На сколько кирпич уходит вглубь кладки за удар, м.")]
    [Min(0f)] public float shiftDistance = 0.025f;

    [Tooltip("Насколько кирпич перекашивает за удар, градусы.")]
    [Min(0f)] public float shiftAngle = 5f;

    [Tooltip("Скорость, с которой выбитый кирпич вылетает от игрока, м/с.")]
    [Min(0f)] public float knockOutSpeed = 2.5f;

    [Tooltip("Добавка скорости вверх у выбитого кирпича, м/с.")]
    [Min(0f)] public float knockOutUpSpeed = 0.8f;

    [Tooltip("Насколько сильно выбитый кирпич закручивается, рад/с.")]
    [Min(0f)] public float knockOutSpin = 8f;

    [Header("Звуки и эффекты")]
    [Tooltip("Кирпич сдвинулся от удара.")]
    public SoundCue shiftSound;

    [Tooltip("Кирпич выбит.")]
    public SoundCue knockOutSound;

    [Tooltip("Облачко пыли в точке удара (префаб с ParticleSystem, сам себя уничтожает).")]
    public GameObject hitDust;

    [Tooltip("Обрушение кладки: пыль и кирпичная крошка (префаб; область испускания растягивается по кладке).")]
    public GameObject crumbleEffect;

    /// <summary>Сколько кирпичей уже выбито.</summary>
    public int KnockedOutCount { get; private set; }

    /// <summary>Кладка обрушена.</summary>
    public bool IsBroken => breakable != null && breakable.IsBroken;

    private class Brick
    {
        public Transform transform;
        public Vector3 restPosition;
        public Quaternion restRotation;
        public Vector3 size;
        public int hits;
        public bool knockedOut;
        public Tween tween;
    }

    private const float AdjacencyGap = 0.02f;

    private readonly List<Brick> bricks = new List<Brick>();
    private Breakable breakable;
    private bool initialized;

    private void Awake() => EnsureInitialized();

    private void OnDisable()
    {
        foreach (Brick brick in bricks) brick.tween?.Kill();
    }

    /// <summary>Ленивая инициализация: SaveLoadService восстанавливает выбитые кирпичи из своего Awake,
    /// раньше Awake кладки. Порядок кирпичей (иерархия) — индекс в сейве.</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        breakable = GetComponent<Breakable>();
        Transform root = bricksRoot != null ? bricksRoot : transform;
        foreach (Transform child in root)
        {
            if (child.GetComponent<MeshRenderer>() == null) continue;
            bricks.Add(new Brick
            {
                transform = child,
                restPosition = child.localPosition,
                restRotation = child.localRotation,
                size = child.localScale,
            });
        }
    }

    /// <summary>Удар кувалдой в точку point, direction — куда направлен удар (от игрока).</summary>
    public void Hit(Vector3 point, Vector3 direction)
    {
        EnsureInitialized();
        if (IsBroken) return;

        SpawnEffect(hitDust, point);
        if (KnockedOutCount >= knockoutsToCollapse)
        {
            Collapse();
            return;
        }

        Brick brick = FindBrick(point);
        if (brick == null)
        {
            SoundPlayer.Play(shiftSound, point, 0.6f);
            return;
        }

        brick.hits++;
        int required = IsLoose(brick) ? 1 : hitsToKnockOut;
        if (brick.hits >= required) KnockOut(brick, direction);
        else Shift(brick, direction);
    }

    /// <summary>Ближайший к точке удара невыбитый кирпич (по расстоянию до его прямоугольника в
    /// плоскости кладки) или null, если он дальше hitRadius.</summary>
    private Brick FindBrick(Vector3 point)
    {
        Vector3 local = BricksRoot.InverseTransformPoint(point);
        Brick best = null;
        float bestDistance = hitRadius;
        foreach (Brick brick in bricks)
        {
            if (brick.knockedOut) continue;
            float dx = Mathf.Max(0f, Mathf.Abs(local.x - brick.restPosition.x) - brick.size.x * 0.5f);
            float dy = Mathf.Max(0f, Mathf.Abs(local.y - brick.restPosition.y) - brick.size.y * 0.5f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > bestDistance) continue;
            // Точка внутри нескольких (на стыке) — ближе к центру.
            if (best != null && distance == bestDistance
                && (local - brick.restPosition).sqrMagnitude >= (local - best.restPosition).sqrMagnitude) continue;
            best = brick;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>Соседствует ли кирпич с выбитым (соприкасаются прямоугольники в плоскости кладки).</summary>
    private bool IsLoose(Brick brick)
    {
        foreach (Brick other in bricks)
        {
            if (!other.knockedOut || other == brick) continue;
            bool overlapX = Mathf.Abs(brick.restPosition.x - other.restPosition.x) <= (brick.size.x + other.size.x) * 0.5f + AdjacencyGap;
            bool overlapY = Mathf.Abs(brick.restPosition.y - other.restPosition.y) <= (brick.size.y + other.size.y) * 0.5f + AdjacencyGap;
            if (overlapX && overlapY) return true;
        }
        return false;
    }

    private Transform BricksRoot => bricksRoot != null ? bricksRoot : transform;

    private float PushSign(Vector3 direction) =>
        Vector3.Dot(BricksRoot.InverseTransformDirection(direction), Vector3.forward) >= 0f ? 1f : -1f;

    private void Shift(Brick brick, Vector3 direction)
    {
        Vector3 target = brick.restPosition + Vector3.forward * (PushSign(direction) * shiftDistance * brick.hits);
        Quaternion tilt = Quaternion.Euler(Random.Range(-shiftAngle, shiftAngle), Random.Range(-shiftAngle, shiftAngle), Random.Range(-shiftAngle, shiftAngle));

        brick.tween?.Kill();
        brick.tween = DOTween.Sequence()
            .Join(brick.transform.DOLocalMove(target, 0.12f).SetEase(Ease.OutBack))
            .Join(brick.transform.DOLocalRotateQuaternion(brick.restRotation * tilt, 0.12f).SetEase(Ease.OutBack));
        SoundPlayer.Play(shiftSound, brick.transform.position);
    }

    private void KnockOut(Brick brick, Vector3 direction)
    {
        brick.tween?.Kill();
        brick.knockedOut = true;
        KnockedOutCount++;

        Vector3 position = brick.transform.position;
        Quaternion rotation = brick.transform.rotation;
        brick.transform.gameObject.SetActive(false);
        SoundPlayer.Play(knockOutSound, position);

        if (brickItemPrefab == null || brick.size.x < minItemLength)
        {
            // Половинка у края — в пыль.
            SpawnEffect(hitDust, position);
            return;
        }

        GameObject item = Instantiate(brickItemPrefab, position, rotation);
        // Предмет рождается внутри сплошного коллайдера кладки — без этого его вытолкнуло бы рывком.
        foreach (Collider wallCollider in GetComponentsInChildren<Collider>())
        {
            if (wallCollider.isTrigger) continue;
            foreach (Collider itemCollider in item.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(itemCollider, wallCollider);
        }

        Rigidbody body = item.GetComponent<Rigidbody>();
        if (body == null) return;
        Vector3 away = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        body.linearVelocity = away * knockOutSpeed + Vector3.up * knockOutUpSpeed + Random.insideUnitSphere * 0.4f;
        body.angularVelocity = Random.insideUnitSphere * knockOutSpin;
    }

    /// <summary>Обрушение: оставшиеся кирпичи — в пыль и крошку, Breakable разбит.</summary>
    private void Collapse()
    {
        bool any = false;
        Bounds bounds = default;
        foreach (Brick brick in bricks)
        {
            if (brick.knockedOut) continue;
            brick.tween?.Kill();
            var brickBounds = new Bounds(brick.restPosition, brick.size);
            if (!any) { bounds = brickBounds; any = true; }
            else bounds.Encapsulate(brickBounds);
        }

        if (any && crumbleEffect != null)
        {
            GameObject effect = Instantiate(crumbleEffect, BricksRoot.TransformPoint(bounds.center), BricksRoot.rotation);
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.scale = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.05f, bounds.size.z));
            }
        }

        breakable.Break(BreakMode.Smash);
    }

    // ───────────────────────── Сейв ─────────────────────────

    /// <summary>Индексы выбитых кирпичей (по порядку иерархии) — для сейва.</summary>
    public int[] GetKnockedOut()
    {
        EnsureInitialized();
        var result = new List<int>();
        for (int i = 0; i < bricks.Count; i++)
            if (bricks[i].knockedOut) result.Add(i);
        return result.ToArray();
    }

    /// <summary>Восстановление из сейва: выбитые кирпичи просто спрятаны — без предметов (они
    /// восстанавливаются отдельно, как лежащие в мире), звуков и эффектов.</summary>
    public void RestoreKnockedOut(int[] indices)
    {
        EnsureInitialized();
        if (indices == null) return;
        foreach (int index in indices)
        {
            if (index < 0 || index >= bricks.Count || bricks[index].knockedOut) continue;
            bricks[index].knockedOut = true;
            bricks[index].transform.gameObject.SetActive(false);
            KnockedOutCount++;
        }
    }

    private static void SpawnEffect(GameObject prefab, Vector3 position)
    {
        if (prefab != null) Instantiate(prefab, position, Quaternion.identity);
    }
}
