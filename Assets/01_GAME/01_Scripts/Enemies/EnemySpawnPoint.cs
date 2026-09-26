using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Точка респавна: держит одного врага заданного типа — спавнит его при старте сцены в своей позиции
/// и снова, как только игрок убивает предыдущего. Позиция точки = дом врага: он блуждает вокруг неё в
/// пределах EnemyData.wanderRadius по NavMesh. Использует EnemySpawner как фабрику, сама не
/// инстанцирует префаб напрямую.
///
/// Гизмо в сцене: зелёная сфера — под точкой есть NavMesh, красная — нет (не запечён NavMeshSurface
/// или точка стоит мимо пола); голубой круг — радиус блуждания.
/// </summary>
public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Спавн")]
    [Tooltip("Фабрика, создающая врагов.")]
    public EnemySpawner spawner;

    [Tooltip("Тип врага, который спавнится в этой точке.")]
    public EnemyData enemyData;

    [Tooltip("Через сколько секунд повторить попытку, если заспавнить не удалось (например, под точкой нет NavMesh).")]
    [Min(0.5f)] public float retryDelay = 3f;

    [Tooltip("Враг стоит на месте («спит» в комнате), пока не заметит игрока, вместо блуждания.")]
    public bool startIdle;

    private Enemy currentEnemy;

    /// <summary>Игрок убил врага этой точки (до респавна). Для сюжетных триггеров: конкретную точку
    /// сюжет узнаёт по PersistentId на её объекте.</summary>
    public event Action<EnemySpawnPoint, Enemy> OnEnemyKilled;

    private void Start()
    {
        TrySpawn();
    }

    private void OnDestroy()
    {
        if (currentEnemy != null) currentEnemy.OnDied -= HandleEnemyDied;
    }

    private void TrySpawn()
    {
        if (spawner == null || enemyData == null) return;

        currentEnemy = spawner.Spawn(enemyData, transform.position, transform.rotation, startIdle);
        if (currentEnemy != null)
            currentEnemy.OnDied += HandleEnemyDied;
        else
            Invoke(nameof(TrySpawn), retryDelay); // раньше точка после неудачи замолкала навсегда
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        currentEnemy = null;
        OnEnemyKilled?.Invoke(this, enemy);
        TrySpawn();
    }

    private void OnDrawGizmos()
    {
        bool onNavMesh = NavMesh.SamplePosition(transform.position, out _, spawner != null ? spawner.navMeshSnapRadius : 2f, NavMesh.AllAreas);
        Gizmos.color = onNavMesh ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.25f, 0.25f);

#if UNITY_EDITOR
        if (enemyData != null)
        {
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, enemyData.wanderRadius);
        }
#endif
    }
}
