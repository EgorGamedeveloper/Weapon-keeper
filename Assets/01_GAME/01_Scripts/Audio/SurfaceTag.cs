using UnityEngine;

/// <summary>
/// Помечает объект (и всех его детей с коллайдерами) типом поверхности для звука шагов. Вешается на
/// пол, лестницу, платформу лифта или на общий родитель группы объектов — ищется вверх по иерархии от
/// коллайдера под ногами. Объекты без тега звучат поверхностью по умолчанию (PlayerFootsteps.defaultSurface).
/// </summary>
public class SurfaceTag : MonoBehaviour
{
    [Tooltip("Тип поверхности этого объекта и его детей.")]
    public SurfaceType surface;

    /// <summary>Поверхность коллайдера: ближайший SurfaceTag вверх по иерархии, иначе fallback.</summary>
    public static SurfaceType Resolve(Collider collider, SurfaceType fallback)
    {
        if (collider == null) return fallback;
        SurfaceTag surfaceTag = collider.GetComponentInParent<SurfaceTag>();
        return surfaceTag != null && surfaceTag.surface != null ? surfaceTag.surface : fallback;
    }
}
