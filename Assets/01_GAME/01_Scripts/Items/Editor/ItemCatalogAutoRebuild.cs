using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Держит ItemCatalog в актуальном состоянии без ручных действий: после любого импорта, удаления
/// или переноса .asset каталог пересобирается (новый ItemData попадает в него сразу), а перед
/// сборкой билда каталог проверяется — пустой каталог или повторяющиеся itemId валят сборку, а не
/// сейвы игроков.
/// </summary>
public class ItemCatalogAutoRebuild : AssetPostprocessor, IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!TouchesAssetFiles(imported) && !TouchesAssetFiles(deleted) && !TouchesAssetFiles(moved)) return;

        foreach (var catalog in LoadCatalogs())
            catalog.Rebuild();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (var catalog in LoadCatalogs())
        {
            catalog.Rebuild();
            string problems = catalog.Validate();
            if (!string.IsNullOrEmpty(problems))
                throw new BuildFailedException($"[ItemCatalog] {problems}");
        }
        AssetDatabase.SaveAssets();
    }

    private static bool TouchesAssetFiles(string[] paths)
    {
        foreach (var path in paths)
            if (path.EndsWith(".asset")) return true;
        return false;
    }

    private static ItemCatalog[] LoadCatalogs()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemCatalog");
        var catalogs = new ItemCatalog[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            catalogs[i] = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return System.Array.FindAll(catalogs, c => c != null);
    }
}
