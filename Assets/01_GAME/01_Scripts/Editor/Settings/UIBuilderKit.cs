using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кирпичики для построения интерфейса из кода (окна настроек, паузы, достижений): канвасы, панели,
/// тексты TMP с LocalizedText, кнопки, ползунки, флажки, выпадающие списки, прокрутка — в едином стиле
/// тёмных панелей проекта (цвета — из MainMenuUI и SkillTreeWindow). Русский текст элементов берётся
/// из strings.csv по ключу: префаб сразу выглядит правильно и без LocalizationService в сцене.
/// </summary>
public static class UIBuilderKit
{
    public const int UILayer = 5;
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // Палитра: тёмные панели, акцент — жёлтый окна навыков, фон кнопок — MainMenuUI.continueActiveColor.
    public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
    public static readonly Color PanelColor = new Color(0.075f, 0.075f, 0.085f, 0.98f);
    public static readonly Color BorderColor = new Color(1f, 0.82f, 0.25f, 0.3f);
    public static readonly Color RowColor = new Color(1f, 1f, 1f, 0.035f);
    public static readonly Color ControlColor = new Color(0.16f, 0.16f, 0.18f, 1f);
    public static readonly Color DropdownListColor = new Color(0.11f, 0.11f, 0.12f, 1f);
    public static readonly Color AccentColor = new Color(1f, 0.82f, 0.25f, 1f);
    public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    public static readonly Color MutedTextColor = new Color(1f, 1f, 1f, 0.55f);
    public static readonly Color DarkTextColor = new Color(0.1f, 0.08f, 0.02f, 1f);

    private const string SquareSpritePath = "Assets/01_GAME/03_Prefabs/UI/UI_Square.png";
    private const string FrameSpritePath = "Assets/01_GAME/03_Prefabs/UI/UI_Frame.png";

    private static Dictionary<string, string> russianStrings;

    // ───────────────────────── Ресурсы ─────────────────────────

    /// <summary>Сплошной белый квадрат проекта (UI_Square), иначе встроенный спрайт UI.</summary>
    public static Sprite Square
    {
        get
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            return sprite != null ? sprite : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
    }

    /// <summary>Рамка проекта (UI_Frame, 9-slice), иначе null — панель будет без рамки.</summary>
    public static Sprite Frame => AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);

    public static TMP_DefaultControls.Resources TmpResources => new TMP_DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
    };

    /// <summary>Русский текст по ключу из strings.csv (для вида префаба до перевода). Нет ключа — сам ключ.</summary>
    public static string Ru(string key)
    {
        if (russianStrings == null)
        {
            russianStrings = new Dictionary<string, string>();
            string path = Path.Combine(Directory.GetCurrentDirectory(), LocalizationEditorTools.StringsPath);
            if (File.Exists(path))
            {
                List<List<string>> table = CsvUtility.Parse(File.ReadAllText(path, Encoding.UTF8));
                int column = -1;
                if (table.Count > 0)
                    for (int i = 1; i < table[0].Count; i++)
                        if (GameLanguages.ColumnToSteamCode(table[0][i]) == GameLanguages.Russian) column = i;

                for (int r = 1; r < table.Count && column > 0; r++)
                    if (table[r].Count > column) russianStrings[table[r][0].Trim()] = table[r][column].Replace("\\n", "\n");
            }
        }

        return russianStrings.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : key;
    }

    /// <summary>Сбросить кэш строк (перед новой сборкой — CSV могли поправить).</summary>
    public static void ResetStringCache() => russianStrings = null;

    // ───────────────────────── Основа ─────────────────────────

    /// <summary>Корень окна: свой Canvas поверх HUD, масштаб под 1920×1080, свой GraphicRaycaster.</summary>
    public static GameObject CreateCanvasRoot(string name, int sortingOrder, bool interactive = true)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.layer = UILayer;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (interactive) root.AddComponent<GraphicRaycaster>();
        return root;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Растянуть по родителю с отступами (слева, сверху, справа, снизу).</summary>
    public static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Якоря по доле ширины/высоты родителя с отступами в пикселях.</summary>
    public static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY,
                              float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Блок по центру родителя фиксированного размера.</summary>
    public static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycastTarget = false)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? sprite : Square;
        image.color = color;
        image.raycastTarget = raycastTarget;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    /// <summary>Затемнение на весь экран (ловит клики, чтобы они не проходили в игру/меню под окном).</summary>
    public static Image CreateDim(Transform parent, Color color)
    {
        Image dim = CreateImage("Dim", parent, color, null, true);
        Stretch(dim.rectTransform);
        return dim;
    }

    /// <summary>Панель окна: тёмный фон и тонкая рамка акцентного цвета.</summary>
    public static RectTransform CreatePanel(Transform parent, Vector2 size)
    {
        Image background = CreateImage("Panel", parent, PanelColor, null, true);
        Center(background.rectTransform, size);

        Sprite frame = Frame;
        if (frame != null)
        {
            Image border = CreateImage("Border", background.transform, BorderColor, frame);
            Stretch(border.rectTransform);
        }
        return background.rectTransform;
    }

    /// <summary>Текст TMP. Если задан ключ — русский текст из strings.csv и LocalizedText.</summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, string key, float fontSize, Color color,
                                             TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal, string fallbackText = "")
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = string.IsNullOrEmpty(key) ? fallbackText : Ru(key);
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        if (!string.IsNullOrEmpty(key))
        {
            var localized = rect.gameObject.AddComponent<LocalizedText>();
            localized.key = key;
        }
        return text;
    }

    /// <summary>Кнопка: фон-картинка и подпись. Подсветка — множителем поверх цвета фона.</summary>
    public static Button CreateButton(string name, Transform parent, string key, Color background, Color textColor,
                                      float fontSize, out TextMeshProUGUI label)
    {
        Image image = CreateImage(name, parent, background, null, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SetTint(button);

        label = CreateText("Label", image.transform, key, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(label.rectTransform, 12f, 4f, 12f, 4f);
        return button;
    }

    /// <summary>Подсветка кнопок на тёмном фоне: множитель больше 1 осветляет цвет картинки.</summary>
    public static void SetTint(Selectable selectable)
    {
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }

    public static LayoutElement AddLayoutElement(GameObject go, float preferredHeight, float preferredWidth = -1f)
    {
        var element = go.AddComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;
        element.minHeight = preferredHeight;
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
        }
        return element;
    }

    public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, RectOffset padding = null)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing, TextAnchor alignment)
    {
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return layout;
    }

    // ───────────────────────── Контролы ─────────────────────────

    /// <summary>Ползунок: фон-дорожка, заливка акцентом, круглая ручка (как DefaultControls.CreateSlider).</summary>
    public static Slider CreateSlider(Transform parent)
    {
        RectTransform root = CreateRect("Slider", parent);

        Image background = CreateImage("Background", root, ControlColor);
        background.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        background.rectTransform.sizeDelta = Vector2.zero;

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);
        fillArea.sizeDelta = new Vector2(-20f, 0f);

        Image fill = CreateImage("Fill", fillArea, AccentColor);
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        Stretch(handleArea, 10f, 0f, 10f, 0f);

        Image handle = CreateImage("Handle", handleArea, Color.white,
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"), true);
        handle.rectTransform.sizeDelta = new Vector2(26f, 0f);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        SetTint(slider);
        return slider;
    }

    /// <summary>Флажок: квадрат фона и галочка акцентного цвета.</summary>
    public static Toggle CreateToggle(Transform parent)
    {
        Image background = CreateImage("Toggle", parent, ControlColor, null, true);
        Sprite frame = Frame;
        if (frame != null)
        {
            Image border = CreateImage("Border", background.transform, new Color(1f, 1f, 1f, 0.25f), frame);
            Stretch(border.rectTransform);
        }

        Image checkmark = CreateImage("Checkmark", background.transform, AccentColor,
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"));
        Stretch(checkmark.rectTransform, 5f, 5f, 5f, 5f);

        var toggle = background.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.isOn = true;
        SetTint(toggle);
        return toggle;
    }

    /// <summary>Выпадающий список TMP (TMP_DefaultControls) в тёмном стиле.</summary>
    public static TMP_Dropdown CreateDropdown(Transform parent, float fontSize)
    {
        GameObject root = TMP_DefaultControls.CreateDropdown(TmpResources);
        root.name = "Dropdown";
        root.transform.SetParent(parent, false);
        SetLayerRecursively(root, UILayer);

        var dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();

        var background = root.GetComponent<Image>();
        background.color = ControlColor;
        SetTint(dropdown);

        TMP_Text caption = dropdown.captionText;
        caption.color = TextColor;
        caption.fontSize = fontSize;
        ((RectTransform)caption.transform).offsetMin = new Vector2(14f, 4f);
        ((RectTransform)caption.transform).offsetMax = new Vector2(-36f, -4f);

        Transform arrow = root.transform.Find("Arrow");
        if (arrow != null)
        {
            arrow.GetComponent<Image>().color = TextColor;
            ((RectTransform)arrow).anchoredPosition = new Vector2(-18f, 0f);
        }

        RectTransform template = dropdown.template;
        template.sizeDelta = new Vector2(0f, 320f);
        template.GetComponent<Image>().color = DropdownListColor;

        Transform content = template.Find("Viewport/Content");
        if (content != null) ((RectTransform)content).sizeDelta = new Vector2(0f, 44f);

        Transform item = template.Find("Viewport/Content/Item");
        if (item != null)
        {
            ((RectTransform)item).sizeDelta = new Vector2(0f, 44f);
            Transform itemBackground = item.Find("Item Background");
            if (itemBackground != null) itemBackground.GetComponent<Image>().color = DropdownListColor;
            Transform itemCheckmark = item.Find("Item Checkmark");
            if (itemCheckmark != null) itemCheckmark.GetComponent<Image>().color = AccentColor;
            var itemToggle = item.GetComponent<Toggle>();
            if (itemToggle != null) SetTint(itemToggle);
        }

        TMP_Text itemLabel = dropdown.itemText;
        itemLabel.color = TextColor;
        itemLabel.fontSize = fontSize;
        ((RectTransform)itemLabel.transform).offsetMin = new Vector2(36f, 1f);

        Transform scrollbar = template.Find("Scrollbar");
        if (scrollbar != null) StyleScrollbar(scrollbar.GetComponent<Scrollbar>());
        return dropdown;
    }

    /// <summary>Вертикальная прокрутка: область с маской (RectMask2D) и полоса справа. Страницы кладутся в viewport.</summary>
    public static ScrollRect CreateScrollView(Transform parent, out RectTransform viewport)
    {
        RectTransform root = CreateRect("Scroll", parent);
        var scrollRect = root.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;

        // Прозрачная картинка ловит колесо и перетаскивание в пустых местах между строками.
        Image catcher = CreateImage("Viewport", root, new Color(0f, 0f, 0f, 0f), null, true);
        viewport = catcher.rectTransform;
        Stretch(viewport, 0f, 0f, 18f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        GameObject scrollbarObject = TMP_DefaultControls.CreateScrollbar(TmpResources);
        scrollbarObject.name = "Scrollbar";
        scrollbarObject.transform.SetParent(root, false);
        SetLayerRecursively(scrollbarObject, UILayer);
        var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.SetDirection(Scrollbar.Direction.BottomToTop, true);
        var scrollbarRect = (RectTransform)scrollbarObject.transform;
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.pivot = Vector2.one;
        scrollbarRect.sizeDelta = new Vector2(10f, 0f);
        StyleScrollbar(scrollbar);

        scrollRect.viewport = viewport;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 6f;
        return scrollRect;
    }

    /// <summary>Контейнер строк внутри прокрутки: растёт по высоте содержимого, верх прижат к верху.</summary>
    public static RectTransform CreatePage(string name, RectTransform viewport, float spacing)
    {
        RectTransform page = CreateRect(name, viewport);
        page.anchorMin = new Vector2(0f, 1f);
        page.anchorMax = Vector2.one;
        page.pivot = new Vector2(0.5f, 1f);
        page.sizeDelta = Vector2.zero;
        page.anchoredPosition = Vector2.zero;

        AddVerticalLayout(page.gameObject, spacing, new RectOffset(0, 8, 4, 12));
        var fitter = page.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        return page;
    }

    private static void StyleScrollbar(Scrollbar scrollbar)
    {
        if (scrollbar == null) return;
        var background = scrollbar.GetComponent<Image>();
        if (background != null) background.color = new Color(1f, 1f, 1f, 0.05f);
        if (scrollbar.targetGraphic != null) scrollbar.targetGraphic.color = new Color(1f, 1f, 1f, 0.35f);
        SetTint(scrollbar);
    }

    public static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    // ───────────────────────── Ассеты ─────────────────────────

    /// <summary>Создать папку (со всеми родителями) в Assets.</summary>
    public static void EnsureFolder(string folder)
    {
        folder = folder.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    /// <summary>Сохранить временный объект как префаб (перезаписывая существующий — GUID сохраняется,
    /// ссылки сцен на префаб не рвутся) и удалить объект.</summary>
    public static GameObject SavePrefab(GameObject root, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        Object.DestroyImmediate(root);
        if (!success) Debug.LogError($"[UI Builder] Не удалось сохранить префаб {path}.");
        return prefab;
    }

    /// <summary>Загрузить ScriptableObject или создать его, если ассета ещё нет.</summary>
    public static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Setup] Создан ассет {path}.");
        return asset;
    }
}
