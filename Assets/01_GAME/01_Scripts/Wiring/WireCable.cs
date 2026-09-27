using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Провод на экране: LineRenderer, сглаженный по опорным точкам (сплайн Catmull-Rom). Опорные точки даёт
/// тот, кто тянет провод (WireSpoolTool): вилка в разъёме → вниз к полу → точки на полу там, где прошёл
/// игрок → катушка в руке или второй разъём. Для сейва хранятся именно опорные точки (ControlPoints).
/// Точки — в мировых координатах, объект провода стоит в начале координат.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class WireCable : MonoBehaviour
{
    [Tooltip("Сколько промежуточных точек сплайна на один отрезок между опорными точками.")]
    [Range(1, 8)] public int samplesPerSegment = 4;

    /// <summary>Опорные точки провода (для сейва).</summary>
    public IReadOnlyList<Vector3> ControlPoints => controlPoints;

    private readonly List<Vector3> controlPoints = new List<Vector3>();
    private readonly List<Vector3> smoothed = new List<Vector3>();
    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
    }

    /// <summary>Задать опорные точки и перерисовать провод.</summary>
    public void SetPoints(IList<Vector3> points)
    {
        if (line == null) Awake();
        controlPoints.Clear();
        controlPoints.AddRange(points);

        smoothed.Clear();
        int count = controlPoints.Count;
        if (count < 2)
        {
            line.positionCount = 0;
            return;
        }

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = controlPoints[Mathf.Max(0, i - 1)];
            Vector3 p1 = controlPoints[i];
            Vector3 p2 = controlPoints[i + 1];
            Vector3 p3 = controlPoints[Mathf.Min(count - 1, i + 2)];
            for (int s = 0; s < samplesPerSegment; s++)
                smoothed.Add(CatmullRom(p0, p1, p2, p3, s / (float)samplesPerSegment));
        }
        smoothed.Add(controlPoints[count - 1]);

        line.positionCount = smoothed.Count;
        for (int i = 0; i < smoothed.Count; i++) line.SetPosition(i, smoothed[i]);
    }

    /// <summary>Длина провода по опорным точкам, м.</summary>
    public static float Length(IList<Vector3> points)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
        return length;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }
}
