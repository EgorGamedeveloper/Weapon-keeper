using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строит из кода префаб окна достижений (03_Prefabs/UI/Achievements/AchievementsWindow) и канвас тостов
/// «Достижение получено» (дочерний объект PersistentServices). Стиль — тот же, что у окна настроек (UIBuilderKit).
/// </summary>
public static class AchievementsUIBuilder
{
    public const string PrefabFolder = "Assets/01_GAME/03_Prefabs/UI/Achievements";
    public const string WindowPath = PrefabFolder + "/AchievementsWindow.prefab";

    private const float RowHeight = 104f;

    public static GameObject BuildAchievementsWindow()
    {
        UIBuilderKit.ResetStringCache();
        GameObject root = UIBuilderKit.CreateCanvasRoot("AchievementsWindow", 110);
        var window = root.AddComponent<AchievementsWindow>();

        RectTransform windowRect = UIBuilderKit.CreateRect("Window", root.transform);
        UIBuilderKit.Stretch(windowRect);
        window.windowGroup = windowRect.gameObject.AddComponent<CanvasGroup>();
        UIBuilderKit.CreateDim(windowRect, UIBuilderKit.DimColor);

        RectTransform panel = UIBuilderKit.CreatePanel(windowRect, new Vector2(1120f, 900f));
        window.panel = panel;

        TextMeshProUGUI title = UIBuilderKit.CreateText("Title", panel, "achievements.title", 42f, UIBuilderKit.TextColor,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        UIBuilderKit.Anchor(title.rectTransform, 0f, 1f, 0.6f, 1f, 44f, -92f, 0f, 14f);

        window.summaryText = UIBuilderKit.CreateText("Summary", panel, null, 24f, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.MidlineRight, FontStyles.Normal, "Получено: 0 из 0");
        UIBuilderKit.Anchor(window.summaryText.rectTransform, 0.6f, 1f, 1f, 1f, 0f, -92f, 44f, 14f);

        RectTransform content = UIBuilderKit.CreateRect("Content", panel);
        UIBuilderKit.Stretch(content, 44f, 110f, 44f, 118f);
        window.scrollRect = UIBuilderKit.CreateScrollView(content, out RectTransform viewport);
        UIBuilderKit.Stretch((RectTransform)window.scrollRect.transform);
        window.listRoot = UIBuilderKit.CreatePage("List", viewport, 10f);
        window.scrollRect.content = window.listRoot;

        RectTransform footer = UIBuilderKit.CreateRect("Footer", panel);
        UIBuilderKit.Anchor(footer, 0f, 0f, 1f, 0f, 44f, 30f, 44f, -94f);
        window.closeButton = UIBuilderKit.CreateButton("CloseButton", footer, "common.close", UIBuilderKit.ControlColor,
            UIBuilderKit.TextColor, 24f, out _);
        UIBuilderKit.Anchor((RectTransform)window.closeButton.transform, 1f, 0f, 1f, 1f, -240f, 0f, 0f, 0f);

        RectTransform templates = UIBuilderKit.CreateRect("Templates", panel);
        UIBuilderKit.Stretch(templates);
        window.rowTemplate = BuildRow(templates);
        templates.gameObject.SetActive(false);

        return UIBuilderKit.SavePrefab(root, WindowPath);
    }

    private static AchievementRowUI BuildRow(Transform parent)
    {
        Image background = UIBuilderKit.CreateImage("AchievementRow", parent, UIBuilderKit.RowColor);
        UIBuilderKit.AddLayoutElement(background.gameObject, RowHeight);
        var row = background.gameObject.AddComponent<AchievementRowUI>();
        row.group = background.gameObject.AddComponent<CanvasGroup>();

        Image frame = UIBuilderKit.CreateImage("IconFrame", background.transform, UIBuilderKit.ControlColor);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.sizeDelta = new Vector2(84f, 84f);
        frameRect.anchoredPosition = new Vector2(12f, 0f);

        row.icon = UIBuilderKit.CreateImage("Icon", frame.transform, UIBuilderKit.AccentColor);
        UIBuilderKit.Stretch(row.icon.rectTransform, 8f, 8f, 8f, 8f);
        row.icon.preserveAspect = true;

        row.titleText = UIBuilderKit.CreateText("Title", background.transform, null, 26f, UIBuilderKit.TextColor,
            TextAlignmentOptions.BottomLeft, FontStyles.Bold, "Название");
        UIBuilderKit.Anchor(row.titleText.rectTransform, 0f, 0.52f, 0.72f, 1f, 112f, 0f, 0f, 10f);

        row.descriptionText = UIBuilderKit.CreateText("Description", background.transform, null, 20f, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.TopLeft, FontStyles.Normal, "Описание");
        row.descriptionText.textWrappingMode = TextWrappingModes.Normal;
        UIBuilderKit.Anchor(row.descriptionText.rectTransform, 0f, 0f, 0.72f, 0.5f, 112f, 8f, 0f, 0f);

        row.statusText = UIBuilderKit.CreateText("Status", background.transform, null, 22f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.MidlineRight, FontStyles.Bold, "");
        UIBuilderKit.Anchor(row.statusText.rectTransform, 0.72f, 0.5f, 1f, 1f, 0f, 0f, 20f, 8f);

        RectTransform progress = UIBuilderKit.CreateRect("Progress", background.transform);
        UIBuilderKit.Anchor(progress, 0.74f, 0.18f, 1f, 0.34f, 0f, 0f, 20f, 0f);
        row.progressRoot = progress.gameObject;
        Image track = UIBuilderKit.CreateImage("Track", progress, UIBuilderKit.ControlColor);
        UIBuilderKit.Stretch(track.rectTransform);
        row.progressFill = UIBuilderKit.CreateImage("Fill", progress, UIBuilderKit.AccentColor);
        UIBuilderKit.Stretch(row.progressFill.rectTransform);
        row.progressFill.type = Image.Type.Filled;
        row.progressFill.fillMethod = Image.FillMethod.Horizontal;
        row.progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        row.progressFill.fillAmount = 0.4f;

        row.progressText = UIBuilderKit.CreateText("ProgressText", progress, null, 18f, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.BottomRight, FontStyles.Normal, "0 / 10");
        UIBuilderKit.Anchor(row.progressText.rectTransform, 0f, 1f, 1f, 1f, 0f, 2f, 0f, -26f);
        return row;
    }

    /// <summary>Канвас тостов под корнем PersistentServices: тост в правом верхнем углу, поверх всего.</summary>
    public static AchievementToastUI BuildToastCanvas(Transform servicesRoot)
    {
        GameObject canvasObject = UIBuilderKit.CreateCanvasRoot("AchievementToasts", 500, false);
        canvasObject.transform.SetParent(servicesRoot, false);
        var toast = canvasObject.AddComponent<AchievementToastUI>();

        Image background = UIBuilderKit.CreateImage("Toast", canvasObject.transform, UIBuilderKit.PanelColor);
        RectTransform panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = Vector2.one;
        panel.pivot = Vector2.one;
        panel.sizeDelta = new Vector2(500f, 112f);
        panel.anchoredPosition = new Vector2(-28f, -28f);
        toast.panel = panel;
        toast.group = background.gameObject.AddComponent<CanvasGroup>();

        Sprite frameSprite = UIBuilderKit.Frame;
        if (frameSprite != null)
        {
            Image border = UIBuilderKit.CreateImage("Border", panel, new Color(1f, 0.82f, 0.25f, 0.6f), frameSprite);
            UIBuilderKit.Stretch(border.rectTransform);
        }

        Image iconFrame = UIBuilderKit.CreateImage("IconFrame", panel, UIBuilderKit.ControlColor);
        RectTransform iconFrameRect = iconFrame.rectTransform;
        iconFrameRect.anchorMin = iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
        iconFrameRect.pivot = new Vector2(0f, 0.5f);
        iconFrameRect.sizeDelta = new Vector2(80f, 80f);
        iconFrameRect.anchoredPosition = new Vector2(16f, 0f);
        toast.icon = UIBuilderKit.CreateImage("Icon", iconFrame.transform, Color.white);
        UIBuilderKit.Stretch(toast.icon.rectTransform, 6f, 6f, 6f, 6f);
        toast.icon.preserveAspect = true;

        TextMeshProUGUI header = UIBuilderKit.CreateText("Header", panel, "toast.achievement", 20f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.UpperCase);
        UIBuilderKit.Anchor(header.rectTransform, 0f, 0.52f, 1f, 1f, 112f, 0f, 16f, 12f);

        toast.titleText = UIBuilderKit.CreateText("Title", panel, null, 26f, UIBuilderKit.TextColor,
            TextAlignmentOptions.TopLeft, FontStyles.Bold, "Достижение");
        UIBuilderKit.Anchor(toast.titleText.rectTransform, 0f, 0f, 1f, 0.5f, 112f, 12f, 16f, 0f);
        return toast;
    }
}
