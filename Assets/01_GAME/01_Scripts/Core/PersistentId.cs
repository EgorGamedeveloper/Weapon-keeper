using UnityEngine;

/// <summary>
/// Стабильный идентификатор авторского объекта сцены (ячейка полки, точка ремонта, пятно,
/// ломаемая доска) — по нему сохранение находит нужный объект после перезапуска игры.
///
/// Обычные предметы (WorldItem) этот компонент НЕ получают: они не переживают загрузку,
/// а спавнятся заново из записей сейва по ItemData.itemId. Идентичность нужна только тем
/// объектам, которые расставлены в сцене руками и существуют в единственном экземпляре.
///
/// GUID выдаётся один раз в редакторе и дальше не меняется: менять его у уже вышедшей игры
/// нельзя — сейвы игроков перестанут находить объект.
/// </summary>
[DisallowMultipleComponent]
public class PersistentId : MonoBehaviour
{
    [Tooltip("Генерируется автоматически при добавлении компонента. Руками не трогать.")]
    [SerializeField] private string id;

    public string Id => id;

#if UNITY_EDITOR
    private void Reset() => Generate();

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            Generate();
            return;
        }

        // Копирование объекта в сцене копирует и id — две ячейки начали бы спорить за одну
        // запись сейва. Дубликату выдаём новый id; ищем только среди загруженных объектов,
        // их тут пара десятков, так что перебор дешевле любого реестра.
        foreach (var other in FindObjectsByType<PersistentId>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other == this || other.id != id) continue;
            Generate();
            return;
        }
    }

    /// <summary>
    /// Выдать id, если его ещё нет. Нужно при массовом навешивании компонента скриптом:
    /// Reset() срабатывает только когда компонент добавляют через инспектор, а при
    /// AddComponent из кода объект остался бы с пустым id.
    /// </summary>
    public void EnsureId()
    {
        if (string.IsNullOrEmpty(id)) Generate();
    }

    private void Generate()
    {
        id = System.Guid.NewGuid().ToString("N");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
