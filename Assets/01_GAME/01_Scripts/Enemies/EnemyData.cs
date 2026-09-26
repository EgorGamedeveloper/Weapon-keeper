using UnityEngine;

/// <summary>
/// ScriptableObject с описанием типа врага (здоровье, параметры передвижения по NavMesh, блуждание).
/// Создаётся через Assets > Create > Enemies > Enemy Data. Один ассет = один "тип" врага.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Enemies/Enemy Data", order = 30)]
public class EnemyData : ScriptableObject
{
    [Header("Идентификация")]
    [Tooltip("Стабильный id для сохранений. Заполняется автоматически из имени ассета.")]
    public string enemyId;

    [Header("Здоровье")]
    [Tooltip("Максимальное здоровье врага.")]
    [Min(1f)] public float maxHealth = 100f;

    [Header("Спавн")]
    [Tooltip("Префаб, который создаёт EnemySpawner для этого типа врага (должен содержать Enemy/EnemyHealth).")]
    public GameObject enemyPrefab;

    [Header("Передвижение (NavMeshAgent)")]
    [Tooltip("Базовая скорость перемещения, м/с.")]
    [Min(0f)] public float moveSpeed = 1.4f;

    [Tooltip("Ускорение, м/с².")]
    [Min(0f)] public float acceleration = 6f;

    [Tooltip("На каком расстоянии до точки остановки (перед паузой) считать её достигнутой.")]
    [Min(0f)] public float stoppingDistance = 0.3f;

    [Header("Блуждание")]
    [Tooltip("Как далеко (м) враг может уйти от точки, где появился. Сама область хождения задаётся NavMesh " +
             "(NavMeshSurface на объектах, по которым ходят NPC), радиус лишь не даёт разбредаться по всей карте.")]
    [Min(1f)] public float wanderRadius = 10f;

    [Tooltip("Длина одного шага блуждания (м): следующая точка выбирается на таком расстоянии от текущей позиции.")]
    public Vector2 stepDistanceRange = new Vector2(2f, 6f);

    [Tooltip("Насколько (±градусов) следующий шаг может отклониться от текущего направления взгляда. " +
             "Меньше — идёт плавнее и прямее, больше — петляет. Без этого враг ходил бы челноком туда-обратно.")]
    [Range(10f, 180f)] public float forwardBiasAngle = 70f;

    [Tooltip("Вероятность остановиться и постоять после шага. Остальные шаги идут цепочкой без остановки.")]
    [Range(0f, 1f)] public float pauseChance = 0.3f;

    [Tooltip("Диапазон длительности паузы, сек.")]
    public Vector2 wanderPauseRange = new Vector2(1.5f, 4f);

    [Tooltip("Максимальная случайная задержка перед первым шагом — чтобы несколько врагов не шагали синхронно.")]
    [Min(0f)] public float startDelayMax = 1.5f;

    [Header("Живость движения")]
    [Tooltip("Разброс базовой скорости между экземплярами (±доля): 0.15 — одни на 15% быстрее, другие медленнее.")]
    [Range(0f, 0.5f)] public float speedVariance = 0.15f;

    [Tooltip("Насколько сильно скорость «плавает» во время ходьбы (доля от базовой) — шаркающая походка.")]
    [Range(0f, 0.8f)] public float shambleAmplitude = 0.3f;

    [Tooltip("Как быстро плавает скорость, колебаний в секунду (примерно).")]
    [Min(0f)] public float shambleFrequency = 0.7f;

    [Tooltip("Резкость поворота корпуса по направлению движения: больше — поворачивается быстрее.")]
    [Min(0.1f)] public float turnSharpness = 4f;

    [Tooltip("Скорость поворота при осматривании на паузе, градусов в секунду.")]
    [Min(0f)] public float lookAroundTurnSpeed = 70f;

    [Tooltip("Как часто (сек) враг на паузе поворачивается в новую сторону.")]
    public Vector2 lookAroundInterval = new Vector2(0.8f, 2f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(enemyId)) return;

        // Автозаполнение один раз, из имени ассета: EnemyData_Zombie -> zombie.
        string source = name.StartsWith("EnemyData_") ? name.Substring("EnemyData_".Length) : name;
        var builder = new System.Text.StringBuilder(source.Length);
        foreach (char c in source.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        enemyId = builder.ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
