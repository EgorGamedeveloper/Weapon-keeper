using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Волна врагов: по клавише (или вызову SpawnWave из будущего события орды) создаёт count врагов
/// кольцом вокруг игрока и сразу натравливает их с постоянным агром — они знают, где игрок, и не
/// теряют его из виду. Точки берутся только на NavMesh и только такие, откуда есть путь до игрока
/// (иначе враг появился бы на крыше или в закрытом помещении и застрял).
/// </summary>
public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("Запуск")]
    [Tooltip("Клавиша вызова волны (старый UnityEngine.Input, как весь ввод проекта).")]
    public KeyCode triggerKey = KeyCode.O;

    [Header("Волна")]
    [Tooltip("Фабрика врагов; цель (игрок) берётся из неё же.")]
    public EnemySpawner spawner;

    [Tooltip("Тип врагов в волне.")]
    public EnemyData enemyData;

    [Tooltip("Сколько врагов в волне.")]
    [Min(1)] public int count = 30;

    [Tooltip("Расстояние от игрока, на котором появляются враги, м (мин/макс).")]
    public Vector2 radiusRange = new Vector2(18f, 28f);

    [Tooltip("Сколько случайных точек пробовать для одного врага, прежде чем пропустить его.")]
    [Min(1)] public int attemptsPerEnemy = 10;

    /// <summary>Волна создана: (сколько врагов реально появилось).</summary>
    public event Action<int> OnWaveSpawned;

    private NavMeshPath pathBuffer;

    private void Awake()
    {
        pathBuffer = new NavMeshPath();
    }

    private void Update()
    {
        if (Time.timeScale > 0f && Input.GetKeyDown(triggerKey)) SpawnWave();
    }

    /// <summary>Создать волну вокруг игрока. Возвращает число появившихся врагов.</summary>
    public int SpawnWave() => SpawnWave(enemyData, count);

    /// <summary>Волна заданного типа и размера (сюжетное событие «Спаун врагов → вокруг игрока»).
    /// spawnedEnemies — если задан, сюда добавляются появившиеся враги.</summary>
    public int SpawnWave(EnemyData enemyData, int count, System.Collections.Generic.List<Enemy> spawnedEnemies = null)
    {
        PlayerHealth player = spawner != null ? spawner.player : null;
        if (player == null || player.IsDead || enemyData == null || count <= 0) return 0;
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerHit, 5f, NavMesh.AllAreas)) return 0;

        Vector3 center = playerHit.position;
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            // Равномерно по кругу с небольшим разбросом — враги подходят со всех сторон.
            float baseAngle = i * 360f / count;
            for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
            {
                float angle = baseAngle + UnityEngine.Random.Range(-180f / count, 180f / count) + attempt * 37f;
                float radius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
                Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, spawner.navMeshSnapRadius, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(hit.position, center, NavMesh.AllAreas, pathBuffer)
                    || pathBuffer.status != NavMeshPathStatus.PathComplete)
                    continue;

                Vector3 look = center - hit.position;
                look.y = 0f;
                Enemy enemy = spawner.Spawn(enemyData, hit.position, Quaternion.LookRotation(look));
                if (enemy == null) continue;

                enemy.Aggro(persistent: true);
                spawnedEnemies?.Add(enemy);
                spawned++;
                break;
            }
        }

        OnWaveSpawned?.Invoke(spawned);
        Debug.Log($"EnemyWaveSpawner: волна — {spawned}/{count} врагов вокруг игрока.", this);
        return spawned;
    }
}
