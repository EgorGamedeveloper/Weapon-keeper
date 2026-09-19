using System;
using UnityEngine;

/// <summary>
/// Объект, который можно разобрать ломом (режим Tool, клавиша 3): прибитая доска на окне/двери,
/// старая сломанная полка "на снос" или ящик с оружием. Один и тот же компонент покрывает все три
/// сценария через данные (spawnOnBreak), без подклассов — что именно происходит при поломке (открыть
/// окно, включить свет и т.п.) решает отдельный сценарный скрипт, подписанный на OnBroken
/// (см. RevealOnBreak), сам Breakable об этом не знает.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour
{
    [Tooltip("Целый вид объекта — выключается/уничтожается при поломке.")]
    public GameObject intactVisual;

    [Tooltip("Что заспавнить при поломке: обломки-мусор (доски) ИЛИ лут (оружие/патроны из ящика) — один и тот же механизм.")]
    public GameObject[] spawnOnBreak;

    [Tooltip("Случайный разброс точек спавна по X/Z от позиции объекта.")]
    public Vector2 spawnScatterRadius = new Vector2(0.3f, 0.3f);

    [Tooltip("Уничтожить весь объект при поломке. Если выключено — объект остаётся (без коллайдера), просто прячет intactVisual.")]
    public bool destroySelf = true;

    [Tooltip("Цвет подсветки (emission) при наведении лучом в режиме Tool.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    /// <summary>Объект уже разобран.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Объект разобран — хук для сценарных скриптов (открыть окно, включить свет и т.п.) и трекеров.</summary>
    public event Action<Breakable> OnBroken;

    private Renderer[] renderers;
    private bool isHighlighted;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
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

    /// <summary>Разобрать объект ломом.</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        SetHighlight(false);

        foreach (var prefab in spawnOnBreak)
        {
            if (prefab == null) continue;
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-spawnScatterRadius.x, spawnScatterRadius.x),
                0f,
                UnityEngine.Random.Range(-spawnScatterRadius.y, spawnScatterRadius.y));
            Instantiate(prefab, transform.position + offset, UnityEngine.Random.rotation);
        }

        if (intactVisual != null) intactVisual.SetActive(false);

        OnBroken?.Invoke(this);

        if (destroySelf)
        {
            Destroy(gameObject);
        }
        else
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
    }
}
