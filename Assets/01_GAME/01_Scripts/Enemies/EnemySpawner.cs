using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Фабрика врагов: создаёт инстансы Enemy по EnemyData в нужной точке. Нужна, чтобы игровые события
/// (квесты, триггеры, будущие волны/директор врагов) могли порождать врагов одним вызовом, не зная
/// деталей инстанцирования префаба и настройки Enemy/EnemyHealth.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Спавн")]
    [Tooltip("В каком радиусе (м) от запрошенной точки искать NavMesh, чтобы поставить на него врага.")]
    [Min(0.1f)] public float navMeshSnapRadius = 2f;

    /// <summary>Враг создан — хук для систем, которым нужно узнать о новом враге (трекеры, HUD и т.п.).</summary>
    public event Action<Enemy> OnEnemySpawned;

    /// <summary>
    /// Создаёт врага заданного типа в указанной точке, «примагниченной» к NavMesh. Точка становится
    /// домом врага (вокруг неё он блуждает). Возвращает null, если не задан enemyPrefab или под точкой
    /// нет NavMesh (не запечён NavMeshSurface).
    /// </summary>
    public Enemy Spawn(EnemyData data, Vector3 position, Quaternion rotation)
    {
        if (data == null || data.enemyPrefab == null)
        {
            Debug.LogWarning($"EnemySpawner: у EnemyData '{(data != null ? data.name : "null")}' не задан enemyPrefab.", this);
            return null;
        }

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning($"EnemySpawner: под точкой {position} нет NavMesh в радиусе {navMeshSnapRadius} м — " +
                             "проверьте, что на объекте пола есть NavMeshSurface и он запечён.", this);
            return null;
        }

        GameObject instance = Instantiate(data.enemyPrefab, hit.position, rotation);
        Enemy enemy = instance.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogWarning($"EnemySpawner: в префабе '{data.enemyPrefab.name}' нет компонента Enemy.", this);
            return null;
        }

        enemy.Initialize(data, hit.position);
        OnEnemySpawned?.Invoke(enemy);
        return enemy;
    }
}
