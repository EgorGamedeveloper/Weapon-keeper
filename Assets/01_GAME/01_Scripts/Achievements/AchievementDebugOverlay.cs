using UnityEngine;

/// <summary>
/// Отладка достижений (только в редакторе и Development Build, клавиша F9): статистика, достижения с
/// кнопкой выдачи, выдача по API-имени, сброс всего и принудительная отправка. Компонент есть и в
/// релизе (он в префабе PersistentServices), но там ничего не делает — Debug.isDebugBuild = false.
/// Курсор для кликов свободен на паузе (Esc).
/// </summary>
public class AchievementDebugOverlay : MonoBehaviour
{
    [Header("Отладка (редактор и Development Build)")]
    [Tooltip("Клавиша показа панели.")]
    public KeyCode toggleKey = KeyCode.F9;

    private bool visible;
    private string apiName = "";
    private Vector2 scroll;

    private void Update()
    {
        if (Debug.isDebugBuild && Input.GetKeyDown(toggleKey)) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible || !Debug.isDebugBuild) return;

        StatsService stats = StatsService.Instance;
        GUILayout.BeginArea(new Rect(20f, 20f, 520f, Screen.height - 40f), GUI.skin.box);
        GUILayout.Label($"Достижения ({toggleKey} — скрыть). Бэкенд: {(stats != null && stats.Backend != null ? stats.Backend.Name : "нет StatsService")}");

        if (stats != null && stats.Catalog != null)
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Screen.height - 220f));
            GUILayout.Label("Статистика:");
            foreach (StatDefinition definition in stats.Catalog.stats)
                if (definition != null) GUILayout.Label($"  {definition.apiName} = {stats.GetValue(definition.stat):0.##}");

            GUILayout.Label("Достижения:");
            foreach (AchievementData achievement in stats.Catalog.achievements)
            {
                if (achievement == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label((stats.IsUnlocked(achievement) ? "[x] " : "[ ] ") + achievement.apiName);
                if (GUILayout.Button("Выдать", GUILayout.Width(90f))) stats.Unlock(achievement);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            apiName = GUILayout.TextField(apiName, GUILayout.Width(300f));
            if (GUILayout.Button("Выдать по API-имени")) stats.Unlock(apiName.Trim());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Отправить (Store)")) stats.RequestStore(true);
            if (GUILayout.Button("Сбросить всё")) stats.ResetAll();
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }
}
