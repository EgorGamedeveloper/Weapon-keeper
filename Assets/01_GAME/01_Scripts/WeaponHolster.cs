using UnityEngine;

/// <summary>
/// WeaponHolster.cs
///
/// Управляет скрытием/показом оружия у игрока одним компонентом.
///
/// Назначение:
///  - Оружие можно "убрать" (спрятать полностью) — тогда игрок свободно
///    перемещается, а прицел и стрельба выключены. Это нужно, например,
///    в безопасных зонах/внутри магазина, где вместо стрельбы игрок
///    поднимает и раскладывает предметы.
///  - Оружие можно снова "достать" — при этом возвращается ровно то же
///    оружие, которое было активно до скрытия (а не переключится на первое).
///
/// Управление:
///  - Клавиша H (toggleKey) — переключает скрытие/показ вручную.
///  - Методы Holster() / Unholster() — публичные. Их можно вызывать из
///    других скриптов, например при входе/выходе игрока в триггерную зону
///    (крыша = можно стрелять, внутри магазина = оружие убрано).
///
/// Как это работает:
///  При скрытии запоминается индекс активного ствола (lastActiveWeaponIndex),
///  все стволы отключаются (SetActive(false)) — это выключает их Weapon.Update,
///  значит стрельба и рисование прицела OnGUI тоже прекращаются.
///  Также отключается сам WeaponSystem, чтобы во время скрытия нельзя было
///  переключить оружие (клавиши 1-9 / колесо) и случайно вытащить ствол.
///  При показе WeaponSystem снова включается, и SetActiveWeapon возвращает
///  тот же индекс, что был в руках до скрытия.
///
/// Куда вешать:
///  На тот же GameObject, где лежит WeaponSystem (в демо-префабе это объект
///  "Weapons"). Компонент сам найдёт WeaponSystem рядом.
/// </summary>
public class WeaponHolster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Система переключения оружия. Если не заполнено — ищется на этом же объекте.")]
    [SerializeField] private WeaponSystem weaponSystem;

    [Header("Settings")]
    [Tooltip("Клавиша ручного скрытия/показа оружия.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.H;

    // Скрыто ли сейчас оружие
    public bool IsHolstered { get; private set; }

    // Индекс оружия, которое было активно до скрытия (его же вернём при показе)
    private int lastActiveWeaponIndex = -1;

    private void Reset()
    {
        // Удобно: при добавлении компонента через инспектор он сразу подхватит WeaponSystem
        weaponSystem = GetComponent<WeaponSystem>();
    }

    private void Awake()
    {
        if (weaponSystem == null)
            weaponSystem = GetComponent<WeaponSystem>();

        if (weaponSystem == null)
        {
            Debug.LogError("[WeaponHolster] Компонент WeaponSystem не найден. " +
                           "Повесьте WeaponHolster на объект, где есть WeaponSystem, " +
                           "или укажите WeaponSystem вручную в инспекторе.", this);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleHolster();
    }

    /// <summary>Переключает состояние скрытия/показа.</summary>
    public void ToggleHolster()
    {
        if (IsHolstered)
            Unholster();
        else
            Holster();
    }

    /// <summary>
    /// Спрятать оружие. Публичный метод — используйте его из триггерных зон,
    /// когда игрок должен передвигаться и поднимать предметы без оружия.
    /// </summary>
    public void Holster()
    {
        if (IsHolstered)
            return;                 // уже скрыто

        if (weaponSystem == null || weaponSystem.weapons == null)
        {
            Debug.LogWarning("[WeaponHolster] WeaponSystem отсутствует — нечего скрывать.", this);
            return;
        }

        // 1. Запоминаем, какое оружие сейчас активно (чтобы вернуть то же самое)
        lastActiveWeaponIndex = FindActiveWeaponIndex();

        // 2. Полностью выключаем все стволы — оружие исчезает, стрельба и прицел гаснут
        for (int i = 0; i < weaponSystem.weapons.Length; i++)
        {
            if (weaponSystem.weapons[i] != null)
                weaponSystem.weapons[i].SetActive(false);
        }

        IsHolstered = true;

        // 3. Отключаем WeaponSystem, чтобы пока оружие убрано нельзя было
        //    переключить ствол (клавиши 1-9 / колесо) и случайно его показать.
        weaponSystem.enabled = false;
    }

    /// <summary>
    /// Достать оружие снова. Возвращается ровно то оружие, что было в руках
    /// до скрытия (или стартовое, если индекс почему-то не запомнен).
    /// Публичный метод — вызывайте из триггерных зон, где можно стрелять
    /// (например, на крыше).
    /// </summary>
    public void Unholster()
    {
        if (!IsHolstered)
            return;                 // уже показано

        if (weaponSystem == null || weaponSystem.weapons == null)
        {
            Debug.LogWarning("[WeaponHolster] WeaponSystem отсутствует — нечего доставать.", this);
            IsHolstered = false;
            return;
        }

        // 1. Включаем WeaponSystem обратно
        weaponSystem.enabled = true;

        // 2. Если оружия вообще нет (пустой массив) — просто выходим из "скрыто"
        if (weaponSystem.weapons.Length == 0)
        {
            IsHolstered = false;
            return;
        }

        // 3. Если индекс не запомнен или вышел за границы — берём стартовое оружие
        if (lastActiveWeaponIndex < 0 || lastActiveWeaponIndex >= weaponSystem.weapons.Length)
            lastActiveWeaponIndex = Mathf.Clamp(weaponSystem.startingWeaponIndex, 0, weaponSystem.weapons.Length - 1);

        // 4. Возвращаем то же оружие, которое было в руках до скрытия
        weaponSystem.SetActiveWeapon(lastActiveWeaponIndex);

        IsHolstered = false;
    }

    /// <summary>Находит индекс активного (включённого) ствола в массиве WeaponSystem.</summary>
    private int FindActiveWeaponIndex()
    {
        if (weaponSystem == null || weaponSystem.weapons == null)
            return -1;

        for (int i = 0; i < weaponSystem.weapons.Length; i++)
        {
            GameObject w = weaponSystem.weapons[i];
            if (w != null && w.activeSelf)
                return i;
        }
        return -1;
    }
}
