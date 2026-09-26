using UnityEngine;

/// <summary>
/// Захват курсора в игре. Раньше этим занимался сторонний FirstPersonCharacter заодно
/// с передвижением; при переходе на свой контроллер это вынесено отдельно, чтобы контроллер
/// движения занимался только движением.
///
/// Escape освобождает курсор (чтобы можно было выйти из игры или переключить окно),
/// клик по игровому окну захватывает обратно.
///
/// Выполняется ПОСЛЕ остальных скриптов кадра: клик, который возвращает захват, в этом кадре ещё
/// видится всем как "курсор свободен" — PlayerItemInteraction его не обрабатывает, а
/// EquipmentWeaponBridge блокирует выстрел до отпускания кнопки.
/// </summary>
[DefaultExecutionOrder(1000)]
public class CursorLockController : MonoBehaviour
{
    [Header("Захват курсора")]
    [Tooltip("Захватывать курсор сразу при старте сцены.")]
    public bool lockOnStart = true;

    [Tooltip("Клавиша освобождения курсора.")]
    public KeyCode releaseKey = KeyCode.Escape;

    private void Start()
    {
        if (lockOnStart) SetLocked(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(releaseKey))
        {
            SetLocked(false);
            return;
        }

        // Возврат захвата по клику — но только когда курсор уже свободен, иначе мы бы
        // перехватывали обычные игровые клики по предметам.
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            SetLocked(true);
    }

    private void OnDisable()
    {
        // Не оставляем курсор захваченным, если объект выключили (например, при выходе в меню).
        SetLocked(false);
    }

    private void SetLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
