using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools/Weapon Keeper/Achievements/Build Achievements UI: ассеты достижений, префабы окна достижений и
/// PersistentServices (со StatsService и тостами) — и расстановка в открытой игровой сцене: окно
/// достижений (ссылка у меню паузы) и StatsEventAdapter (мусорная категория полок, достижение «все пятна»).
/// </summary>
public static class AchievementsSceneSetup
{
    public const string TrashCategoryPath = "Assets/01_GAME/04_Data/Shelves/Category_Trash.asset";
    private const string Title = "Достижения";

    [MenuItem("Tools/Weapon Keeper/Achievements/Build Achievements UI")]
    private static void BuildAchievementsUI()
    {
        if (!SceneSetupUtility.CheckNotPlaying(Title)) return;

        AchievementAssetsCreator.CreateOrUpdateAssets();
        AchievementsUIBuilder.BuildAchievementsWindow();
        PersistentServicesBuilder.Build();
        AssetDatabase.SaveAssets();

        Scene scene = SceneManager.GetActiveScene();
        var report = new StringBuilder("✔ Ассеты достижений и префабы собраны.\n");
        SceneSetupUtility.EnsurePrefabInstance<PersistentServicesRoot>(scene, PersistentServicesBuilder.PrefabPath, report);
        SetupScene(scene, report);
        SceneSetupUtility.Finish(scene, Title, report);
    }

    /// <summary>Окно достижений и адаптер статистики — только в игровой сцене (есть PlayerCharacterController).</summary>
    public static void SetupScene(Scene scene, StringBuilder report)
    {
        if (SceneSetupUtility.FindInScene<PlayerCharacterController>(scene) == null) return;

        AchievementsWindow window =
            SceneSetupUtility.EnsurePrefabInstance<AchievementsWindow>(scene, AchievementsUIBuilder.WindowPath, report);
        foreach (PauseMenu pause in SceneSetupUtility.FindAllInScene<PauseMenu>(scene))
        {
            Undo.RecordObject(pause, "Achievements window");
            pause.achievementsWindow = window;
            SceneSetupUtility.MarkModified(pause);
        }

        StatsEventAdapter adapter = SceneSetupUtility.EnsureSceneObject<StatsEventAdapter>(scene, "StatsEventAdapter", report);
        Undo.RecordObject(adapter, "Stats adapter");

        var trash = AssetDatabase.LoadAssetAtPath<ShelfCategory>(TrashCategoryPath);
        if (trash != null && !adapter.trashCategories.Contains(trash)) adapter.trashCategories.Add(trash);
        else if (trash == null) report.AppendLine($"• Нет {TrashCategoryPath} — задайте мусорные категории у StatsEventAdapter вручную.");

        if (adapter.allStainsCleanAchievement == null)
            adapter.allStainsCleanAchievement = AssetDatabase.LoadAssetAtPath<AchievementData>(AchievementAssetsCreator.AllStainsAchievementPath);
        SceneSetupUtility.MarkModified(adapter);
    }
}
