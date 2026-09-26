using UnityEngine;

/// <summary>
/// Корень постоянных сервисов игры — настройки, локализация, Steam, статистика и тосты достижений
/// живут на этом объекте (префаб 03_Prefabs/Systems/PersistentServices) через все сцены.
///
/// Экземпляр префаба лежит и в Bootstrap, и в игровой сцене: Play прямо на TestScene в редакторе
/// работает так же, как запуск через меню. Первый проснувшийся становится постоянным
/// (DontDestroyOnLoad), любой следующий (приехал со сценой) уничтожается ЦЕЛИКОМ раньше, чем
/// проснутся его сервисы: DefaultExecutionOrder(-3000) ставит этот Awake впереди Awake всех
/// сервисов, а DestroyImmediate не даёт им проснуться вовсе — иначе дубликат, например, второй
/// раз вызвал бы SteamAPI.Init. Защита от дубликатов у самих сервисов остаётся вторым рубежом.
///
/// Отдельный объект, а не компоненты на GameBootstrap: бутстрапа нет при Play на игровой сцене,
/// а при выходе в меню он пересоздаётся вместе со сценой меню — сервисы же должны жить весь процесс.
/// </summary>
[DefaultExecutionOrder(-3000)]
[DisallowMultipleComponent]
public class PersistentServicesRoot : MonoBehaviour
{
    private static PersistentServicesRoot active;

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;

    private void Awake()
    {
        if (active != null && active != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        active = this;
        // DontDestroyOnLoad работает только для корневых объектов.
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }
}
