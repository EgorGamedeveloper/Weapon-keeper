using UnityEngine;

/// <summary>
/// Объект сцены, которым управляет сюжет: нода «Событие» графа включает или выключает его по storyId
/// (завал, открывающаяся дверь, появившийся ящик, свет). Id — человекочитаемый, его видно в каталоге сцены
/// (Tools/Weapon Keeper/Story/Export Scene Catalog) и выбирают в веб-редакторе.
///
/// Компонент может стоять и на выключенном объекте: сюжет находит его среди неактивных. Состояние
/// после загрузки сейва StoryDirector восстанавливает сам — по пройденным нодам графа.
/// </summary>
[DisallowMultipleComponent]
public class StoryObject : MonoBehaviour
{
    [Header("Сюжет")]
    [Tooltip("Id для нод графа (латиница, цифры, «_»): например storage_door_rubble. Должен быть уникальным в сцене.")]
    public string storyId;

    [Tooltip("Что включать/выключать. Пусто — сам этот объект.")]
    public GameObject target;

    /// <summary>Включить или выключить объект сюжета.</summary>
    public void SetStoryActive(bool active)
    {
        GameObject go = target != null ? target : gameObject;
        if (go.activeSelf != active) go.SetActive(active);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(storyId)) return;
        // Первое значение — из имени объекта; дальше id живёт своей жизнью, граф ссылается на него.
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (char c in name.ToLowerInvariant()) builder.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        storyId = builder.ToString().Trim('_');
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
