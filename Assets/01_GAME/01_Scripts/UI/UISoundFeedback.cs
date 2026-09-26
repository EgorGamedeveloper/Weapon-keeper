using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Звуки интерфейса для всей игры: наведение и клик по любому активному элементу (кнопка, переключатель,
/// слайдер, список) и открытие/закрытие окон. Живёт на префабе PersistentServices (есть во всех сценах).
///
/// Кнопки не нужно размечать по одной: пока курсор свободен (меню, окна), компонент каждый кадр
/// спрашивает EventSystem, какой Selectable под курсором, — так звучат и кнопки, созданные в рантайме
/// (строки терминала, узлы навыков). Окна зовут PlayWindowOpen/PlayWindowClose сами (UiWindowAnimation,
/// TerminalWindow, SkillTreeWindow). Статический Instance — по тому же исключению, что и у
/// SettingsService: к нему обращаются статические помощники окон.
/// </summary>
[DisallowMultipleComponent]
public class UISoundFeedback : MonoBehaviour
{
    [Header("Звуки")]
    [Tooltip("Курсор навёлся на активный элемент.")]
    public SoundCue hoverSound;

    [Tooltip("Клик по активному элементу.")]
    public SoundCue clickSound;

    [Tooltip("Окно открылось.")]
    public SoundCue windowOpenSound;

    [Tooltip("Окно закрылось.")]
    public SoundCue windowCloseSound;

    /// <summary>Текущий экземпляр (на PersistentServices) или null.</summary>
    public static UISoundFeedback Instance { get; private set; }

    private readonly List<RaycastResult> results = new List<RaycastResult>();
    private PointerEventData pointer;
    private EventSystem pointerOwner;
    private Selectable hovered;

    // Без перезагрузки домена (Enter Play Mode Options) статика пережила бы прошлый запуск.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void PlayWindowOpen()
    {
        if (Instance != null) SoundPlayer.Play2D(Instance.windowOpenSound);
    }

    public static void PlayWindowClose()
    {
        if (Instance != null) SoundPlayer.Play2D(Instance.windowCloseSound);
    }

    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        // Курсор захвачен — идёт игра, интерфейса под мышью нет.
        if (eventSystem == null || Cursor.lockState == CursorLockMode.Locked)
        {
            hovered = null;
            return;
        }

        Selectable current = FindSelectableUnderPointer(eventSystem);
        if (current != hovered)
        {
            hovered = current;
            if (current != null) SoundPlayer.Play2D(hoverSound);
        }

        if (current != null && Input.GetMouseButtonDown(0)) SoundPlayer.Play2D(clickSound);
    }

    private Selectable FindSelectableUnderPointer(EventSystem eventSystem)
    {
        if (pointer == null || pointerOwner != eventSystem)
        {
            pointer = new PointerEventData(eventSystem);
            pointerOwner = eventSystem;
        }
        pointer.position = Input.mousePosition;

        results.Clear();
        eventSystem.RaycastAll(pointer, results);
        if (results.Count == 0) return null;

        // Верхний элемент под курсором: его Selectable (кнопка может состоять из картинки и текста).
        Selectable selectable = results[0].gameObject.GetComponentInParent<Selectable>();
        return selectable != null && selectable.IsInteractable() && selectable.isActiveAndEnabled ? selectable : null;
    }
}
