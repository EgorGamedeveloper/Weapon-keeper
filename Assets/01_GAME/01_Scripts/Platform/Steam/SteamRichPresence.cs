using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Статус игрока для друзей в Steam (Rich Presence): «В главном меню» или «Восстанавливает оружейный
/// магазин: 45%». Сами фразы — токены в Docs/Steam/rich_presence_&lt;язык&gt;.vdf, их загружают в Steamworks
/// (Community → Rich Presence), и Steam показывает каждому другу фразу на его языке. Игра передаёт только
/// имя токена (steam_display) и подстановку %progress%.
///
/// Обновляется при загрузке сцены и раз в refreshSeconds (процент восстановления меняется по ходу игры).
/// Без Steam — ничего не делает.
/// </summary>
public class SteamRichPresence : MonoBehaviour
{
    [Header("Токены (Docs/Steam/rich_presence_*.vdf)")]
    [Tooltip("Токен статуса вне игровой сцены (главное меню).")]
    public string menuToken = "#Status_MainMenu";

    [Tooltip("Токен статуса в игре. Подстановка %progress% — процент восстановления здания.")]
    public string gameplayToken = "#Status_Restoring";

    [Tooltip("Как часто (секунды реального времени) обновлять процент восстановления.")]
    [Min(5f)] public float refreshSeconds = 30f;

    private float nextRefreshTime;

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void Start() => Refresh();

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime) Refresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    private void Refresh()
    {
        nextRefreshTime = Time.unscaledTime + refreshSeconds;
        if (!SteamManager.Initialized) return;

        // Трекер восстановления есть только в игровой сцене (в меню Instance уничтожен вместе с уровнем).
        BuildingRestorationTracker tracker = BuildingRestorationTracker.Instance;
        if (tracker != null)
        {
            SteamManager.Instance.SetRichPresence("progress", Mathf.RoundToInt(tracker.ProgressPercent) + "%");
            SteamManager.Instance.SetRichPresence("steam_display", gameplayToken);
        }
        else
        {
            SteamManager.Instance.SetRichPresence("steam_display", menuToken);
        }
    }
}
