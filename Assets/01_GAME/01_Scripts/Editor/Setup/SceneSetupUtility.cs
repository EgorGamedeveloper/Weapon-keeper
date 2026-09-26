using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Общие действия меню настройки сцены: найти компонент в открытой сцене, поставить экземпляр префаба,
/// записать изменение поля так, чтобы оно сохранилось и в экземпляре префаба, добавить EventSystem.
/// Все изменения — через Undo: их можно откатить Ctrl+Z.
/// </summary>
public static class SceneSetupUtility
{
    public static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    public static List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    /// <summary>Компонент T из сцены или новый экземпляр префаба с ним (если в сцене его ещё нет).</summary>
    public static T EnsurePrefabInstance<T>(Scene scene, string prefabPath, StringBuilder report) where T : Component
    {
        T existing = FindInScene<T>(scene);
        if (existing != null) return existing;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            report.AppendLine($"✖ Нет префаба {prefabPath}.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Add " + prefab.name);
        report.AppendLine($"✔ Добавлен {prefab.name}.");
        return instance.GetComponent<T>();
    }

    /// <summary>Пометить изменённым (и записать переопределение, если объект — часть экземпляра префаба).</summary>
    public static void MarkModified(Object target)
    {
        if (target == null) return;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    public static void EnsureEventSystem(Scene scene, StringBuilder report)
    {
        if (FindInScene<EventSystem>(scene) != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
        report.AppendLine("✔ Добавлен EventSystem (кнопкам окон нужен ввод UI).");
    }

    /// <summary>Объект с компонентом T в корне сцены (создать, если его нет).</summary>
    public static T EnsureSceneObject<T>(Scene scene, string name, StringBuilder report) where T : Component
    {
        T existing = FindInScene<T>(scene);
        if (existing != null) return existing;

        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Add " + name);
        report.AppendLine($"✔ Добавлен {name}.");
        return go.AddComponent<T>();
    }

    public static bool CheckNotPlaying(string title)
    {
        if (!EditorApplication.isPlaying) return true;
        EditorUtility.DisplayDialog(title, "Выйдите из Play Mode: изменения сцены в игре не сохраняются.", "OK");
        return false;
    }

    public static void Finish(Scene scene, string title, StringBuilder report)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        string text = report.Length > 0 ? report.ToString() : "Нечего менять — всё уже настроено.";
        Debug.Log($"[{title}] Сцена {scene.name}:\n{text}");
        EditorUtility.DisplayDialog(title, $"Сцена {scene.name}:\n\n{text}\nСохраните сцену (Ctrl+S).", "OK");
    }
}
