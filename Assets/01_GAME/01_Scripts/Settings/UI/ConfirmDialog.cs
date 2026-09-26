using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Диалог подтверждения поверх окна: «Сохранить изменения?», «Выйти в меню?», подтверждение нового
/// разрешения с откатом по таймеру. До трёх кнопок: подтвердить, альтернатива (необязательная), отмена.
///
/// Esc диалог сам не ловит: его закрывает окно-владелец (Cancel), иначе одно нажатие закрывало бы и
/// диалог, и окно под ним — порядок Update разных объектов не гарантирован.
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
    [Header("Окно")]
    [Tooltip("Корень диалога (затемнение + панель). На нём же этот компонент — скрытый диалог не тратит Update.")]
    public CanvasGroup group;

    [Tooltip("Панель диалога — «выпрыгивает» при показе.")]
    public RectTransform panel;

    [Header("Тексты")]
    [Tooltip("Заголовок.")]
    public TMP_Text titleText;

    [Tooltip("Текст вопроса. С таймером в него подставляются оставшиеся секунды ({0}).")]
    public TMP_Text messageText;

    [Header("Кнопки")]
    [Tooltip("Подтвердить.")]
    public Button confirmButton;
    [Tooltip("Подпись кнопки подтверждения.")]
    public TMP_Text confirmLabel;

    [Tooltip("Альтернатива (например «Не сохранять»). Скрывается, если не нужна.")]
    public Button alternativeButton;
    [Tooltip("Подпись альтернативной кнопки.")]
    public TMP_Text alternativeLabel;

    [Tooltip("Отмена (и действие по истечении таймера).")]
    public Button cancelButton;
    [Tooltip("Подпись кнопки отмены.")]
    public TMP_Text cancelLabel;

    public bool IsOpen { get; private set; }

    private Action onConfirm;
    private Action onAlternative;
    private Action onCancel;
    private string messageFormat;
    private bool hasTimeout;
    private float deadline;
    private bool listenersAdded;

    private void Awake()
    {
        AddListeners();
        // Диалог в префабе может быть включён — прячем, если его ещё не показали.
        if (!IsOpen) UiWindowAnimation.HideImmediate(group);
    }

    private void Update()
    {
        if (!IsOpen || !hasTimeout) return;

        UpdateMessage();
        if (Time.unscaledTime >= deadline) Cancel();
    }

    /// <summary>
    /// Показать диалог. timeoutSeconds больше нуля — обратный отсчёт в реальном времени: оставшиеся секунды
    /// подставляются в message вместо {0}, по истечении срабатывает отмена.
    /// </summary>
    public void Show(string title, string message, string confirmText, Action confirm,
                     string cancelText, Action cancel = null,
                     string alternativeText = null, Action alternative = null,
                     float timeoutSeconds = 0f)
    {
        AddListeners();

        onConfirm = confirm;
        onCancel = cancel;
        onAlternative = alternative;
        messageFormat = message ?? "";
        hasTimeout = timeoutSeconds > 0f;
        deadline = Time.unscaledTime + timeoutSeconds;

        if (titleText != null) titleText.text = title;
        if (confirmLabel != null) confirmLabel.text = confirmText;
        if (cancelLabel != null) cancelLabel.text = cancelText;

        bool showAlternative = alternativeButton != null && !string.IsNullOrEmpty(alternativeText);
        if (alternativeButton != null) alternativeButton.gameObject.SetActive(showAlternative);
        if (showAlternative && alternativeLabel != null) alternativeLabel.text = alternativeText;

        UpdateMessage();

        IsOpen = true;
        transform.SetAsLastSibling();
        UiWindowAnimation.Show(group, panel);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>Закрыть как «Отмена» (кнопка, Esc у владельца, истёкший таймер).</summary>
    public void Cancel() => Close(onCancel);

    private void Close(Action callback)
    {
        if (!IsOpen) return;

        IsOpen = false;
        onConfirm = null;
        onAlternative = null;
        onCancel = null;
        UiWindowAnimation.Hide(group, panel);

        // Колбэк — последним: он может сразу показать этот же диалог снова (цепочка вопросов).
        callback?.Invoke();
    }

    private void AddListeners()
    {
        if (listenersAdded) return;
        listenersAdded = true;
        if (confirmButton != null) confirmButton.onClick.AddListener(() => Close(onConfirm));
        if (alternativeButton != null) alternativeButton.onClick.AddListener(() => Close(onAlternative));
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    private void UpdateMessage()
    {
        if (messageText == null) return;
        if (!hasTimeout)
        {
            messageText.text = messageFormat;
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(deadline - Time.unscaledTime));
        try
        {
            messageText.text = string.Format(messageFormat, seconds);
        }
        catch (FormatException)
        {
            messageText.text = messageFormat + " (" + seconds + ")";
        }
    }
}
