using UnityEngine;

/// <summary>
/// Тип поверхности под ногами (бетон, дерево, металл, гравий): какими звуками по ней шагают и
/// приземляются. Поверхность задаётся компонентом SurfaceTag на объекте с коллайдером (или на его
/// родителе); без тега — поверхность по умолчанию из PlayerFootsteps. Ассеты — 04_Data/Audio/Surfaces.
/// </summary>
[CreateAssetMenu(fileName = "Surface_New", menuName = "Audio/Surface Type", order = 1)]
public class SurfaceType : ScriptableObject
{
    [Tooltip("Шаги по этой поверхности.")]
    public SoundCue footsteps;

    [Tooltip("Приземление после прыжка или падения. Громкость зависит от скорости удара.")]
    public SoundCue landing;
}
