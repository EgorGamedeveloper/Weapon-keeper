using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Строит из кода всё, что нужно разговору по рации:
/// - префаб 03_Prefabs/UI/Story/RadioCallUI — плашка «Входящий вызов» вверху экрана и строка субтитров внизу;
/// - простые звуки рации (писк вызова, щелчок ответа и отбоя) — WAV в 08_Story/Audio, пока нет настоящих;
/// - модель рации в руке из примитивов (дочерний объект камеры) — пока нет настоящей модели;
/// - префаб 03_Prefabs/UI/Story/StoryCutscenePlayer — показ кат-сцен: чёрные полосы и подсказка пропуска.
/// Всё это заменяемо: префаб можно перестроить или поправить руками, звуки и модель — назначить свои.
/// </summary>
public static class StoryUIBuilder
{
    public const string PrefabFolder = "Assets/01_GAME/03_Prefabs/UI/Story";
    public const string RadioPath = PrefabFolder + "/RadioCallUI.prefab";
    public const string CutscenePath = PrefabFolder + "/StoryCutscenePlayer.prefab";
    public const string AudioFolder = StoryEditorTools.StoryFolder + "/Audio";
    private const string MaterialFolder = "Assets/01_GAME/06_Materials";

    public static GameObject BuildRadioUI()
    {
        UIBuilderKit.ResetStringCache();
        GameObject root = UIBuilderKit.CreateCanvasRoot("RadioCallUI", 90, false);
        var radio = root.AddComponent<RadioCallUI>();

        // ── Входящий вызов: плашка вверху по центру ──
        RectTransform incoming = UIBuilderKit.CreateRect("Incoming", root.transform);
        incoming.anchorMin = incoming.anchorMax = new Vector2(0.5f, 1f);
        incoming.pivot = new Vector2(0.5f, 1f);
        incoming.sizeDelta = new Vector2(620f, 100f);
        incoming.anchoredPosition = new Vector2(0f, -110f);
        radio.incomingGroup = incoming.gameObject.AddComponent<CanvasGroup>();

        Image incomingBg = UIBuilderKit.CreateImage("Panel", incoming, UIBuilderKit.PanelColor);
        UIBuilderKit.Stretch(incomingBg.rectTransform);
        radio.incomingPanel = incomingBg.rectTransform;
        if (UIBuilderKit.Frame != null)
        {
            Image border = UIBuilderKit.CreateImage("Border", incomingBg.transform, new Color(1f, 0.82f, 0.25f, 0.75f), UIBuilderKit.Frame);
            UIBuilderKit.Stretch(border.rectTransform);
        }

        Image lamp = UIBuilderKit.CreateImage("Lamp", incomingBg.transform, UIBuilderKit.AccentColor);
        lamp.rectTransform.anchorMin = lamp.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        lamp.rectTransform.sizeDelta = new Vector2(14f, 14f);
        lamp.rectTransform.anchoredPosition = new Vector2(34f, 0f);

        radio.incomingTitle = UIBuilderKit.CreateText("Title", incomingBg.transform, null, 28f, UIBuilderKit.TextColor,
            TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.UpperCase, UIBuilderKit.Ru("radio.incoming"));
        UIBuilderKit.Anchor(radio.incomingTitle.rectTransform, 0f, 0.5f, 1f, 1f, 62f, 0f, 20f, 12f);
        radio.incomingHint = UIBuilderKit.CreateText("Hint", incomingBg.transform, null, 22f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.TopLeft, FontStyles.Bold, "[E] Ответить");
        UIBuilderKit.Anchor(radio.incomingHint.rectTransform, 0f, 0f, 1f, 0.5f, 62f, 12f, 20f, 2f);

        // ── Разговор: строка субтитров внизу ──
        RectTransform talk = UIBuilderKit.CreateRect("Subtitles", root.transform);
        talk.anchorMin = talk.anchorMax = new Vector2(0.5f, 0f);
        talk.pivot = new Vector2(0.5f, 0f);
        talk.sizeDelta = new Vector2(1240f, 170f);
        talk.anchoredPosition = new Vector2(0f, 60f);
        radio.talkGroup = talk.gameObject.AddComponent<CanvasGroup>();

        Image talkBg = UIBuilderKit.CreateImage("Panel", talk, new Color(0f, 0f, 0f, 0.62f));
        UIBuilderKit.Stretch(talkBg.rectTransform);

        radio.portraitImage = UIBuilderKit.CreateImage("Portrait", talkBg.transform, Color.white);
        RectTransform portrait = radio.portraitImage.rectTransform;
        portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 0.5f);
        portrait.pivot = new Vector2(0f, 0.5f);
        portrait.sizeDelta = new Vector2(130f, 130f);
        portrait.anchoredPosition = new Vector2(20f, 0f);
        radio.portraitImage.preserveAspect = true;
        radio.portraitImage.gameObject.SetActive(false);

        radio.speakerText = UIBuilderKit.CreateText("Speaker", talkBg.transform, null, 24f, UIBuilderKit.AccentColor,
            TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.UpperCase, "Диспетчер");
        UIBuilderKit.Anchor(radio.speakerText.rectTransform, 0f, 1f, 1f, 1f, 170f, -52f, 30f, 16f);

        radio.lineText = UIBuilderKit.CreateText("Line", talkBg.transform, null, 30f, UIBuilderKit.TextColor,
            TextAlignmentOptions.TopLeft, FontStyles.Normal, "Приём, это диспетчер. Слышишь меня?");
        radio.lineText.textWrappingMode = TextWrappingModes.Normal;
        radio.lineText.overflowMode = TextOverflowModes.Overflow;
        UIBuilderKit.Anchor(radio.lineText.rectTransform, 0f, 0f, 1f, 1f, 170f, 34f, 30f, 54f);

        radio.skipHint = UIBuilderKit.CreateText("SkipHint", talkBg.transform, null, 18f, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.BottomRight, FontStyles.Normal, "[E] Дальше");
        UIBuilderKit.Anchor(radio.skipHint.rectTransform, 0.5f, 0f, 1f, 0f, 0f, 10f, 24f, -34f);

        // ── Звук ──
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        var channel = root.AddComponent<AudioChannelVolume>();
        channel.channel = AudioChannel.UI;

        EnsureSounds();
        radio.audioSource = audio;
        radio.ringClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/radio_ring.wav");
        radio.answerClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/radio_answer.wav");
        radio.hangUpClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/radio_hangup.wav");

        return UIBuilderKit.SavePrefab(root, RadioPath);
    }

    // ───────────────────────── Кат-сцены ─────────────────────────

    /// <summary>Экран кат-сцены: чёрные полосы сверху и снизу и подсказка «Удерживайте [E] — пропустить» с полоской.</summary>
    public static GameObject BuildCutsceneUI()
    {
        UIBuilderKit.ResetStringCache();
        GameObject root = UIBuilderKit.CreateCanvasRoot("StoryCutscenePlayer", 95, false);
        var player = root.AddComponent<StoryCutscenePlayer>();

        RectTransform bars = UIBuilderKit.CreateRect("Letterbox", root.transform);
        UIBuilderKit.Stretch(bars);
        player.letterbox = bars.gameObject.AddComponent<CanvasGroup>();
        Image top = UIBuilderKit.CreateImage("Top", bars, Color.black);
        UIBuilderKit.Anchor(top.rectTransform, 0f, 1f, 1f, 1f, 0f, -120f, 0f, 0f);
        Image bottom = UIBuilderKit.CreateImage("Bottom", bars, Color.black);
        UIBuilderKit.Anchor(bottom.rectTransform, 0f, 0f, 1f, 0f, 0f, 0f, 0f, -120f);

        RectTransform skip = UIBuilderKit.CreateRect("Skip", root.transform);
        skip.anchorMin = skip.anchorMax = new Vector2(1f, 0f);
        skip.pivot = new Vector2(1f, 0f);
        skip.sizeDelta = new Vector2(460f, 60f);
        skip.anchoredPosition = new Vector2(-40f, 30f);
        player.skipGroup = skip.gameObject.AddComponent<CanvasGroup>();

        player.skipText = UIBuilderKit.CreateText("Hint", skip, null, 22f, UIBuilderKit.MutedTextColor,
            TextAlignmentOptions.BottomRight, FontStyles.Normal, UIBuilderKit.Ru("cutscene.skip").Replace("{0}", "E"));
        UIBuilderKit.Anchor(player.skipText.rectTransform, 0f, 0f, 1f, 1f, 0f, 14f, 0f, 0f);

        Image track = UIBuilderKit.CreateImage("Track", skip, new Color(1f, 1f, 1f, 0.15f));
        UIBuilderKit.Anchor(track.rectTransform, 0f, 0f, 1f, 0f, 160f, 0f, 0f, -4f);
        player.skipFill = UIBuilderKit.CreateImage("Fill", track.transform, UIBuilderKit.AccentColor, UIBuilderKit.Square);
        UIBuilderKit.Stretch(player.skipFill.rectTransform);
        player.skipFill.type = Image.Type.Filled;
        player.skipFill.fillMethod = Image.FillMethod.Horizontal;
        player.skipFill.fillAmount = 0f;

        return UIBuilderKit.SavePrefab(root, CutscenePath);
    }

    // ───────────────────────── Модель рации ─────────────────────────

    /// <summary>Простая рация из примитивов дочерним объектом камеры (выключена до разговора).</summary>
    public static Transform BuildRadioModel(Transform cameraTransform)
    {
        var root = new GameObject("RadioModel");
        Undo.RegisterCreatedObjectUndo(root, "Radio model");
        root.transform.SetParent(cameraTransform, false);
        root.transform.localRotation = Quaternion.Euler(-8f, -18f, 6f);
        root.layer = cameraTransform.gameObject.layer;

        Material body = LoadOrCreateMaterial("M_Radio", new Color(0.12f, 0.13f, 0.12f), Color.black);
        Material screen = LoadOrCreateMaterial("M_RadioScreen", new Color(0.9f, 0.62f, 0.12f), new Color(1f, 0.6f, 0.1f) * 1.6f);

        Part(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.13f, 0.035f), body);
        Part(root.transform, PrimitiveType.Cylinder, "Antenna", new Vector3(-0.022f, 0.1f, 0f), new Vector3(0.008f, 0.045f, 0.008f), body);
        Part(root.transform, PrimitiveType.Cube, "Screen", new Vector3(0f, 0.035f, -0.0182f), new Vector3(0.045f, 0.025f, 0.002f), screen);
        Part(root.transform, PrimitiveType.Cube, "Grill", new Vector3(0f, -0.03f, -0.0182f), new Vector3(0.05f, 0.045f, 0.002f),
            LoadOrCreateMaterial("M_RadioGrill", new Color(0.06f, 0.06f, 0.06f), Color.black));
        Part(root.transform, PrimitiveType.Cylinder, "Knob", new Vector3(0.022f, 0.072f, 0f), new Vector3(0.012f, 0.008f, 0.012f), body);

        root.SetActive(false);
        return root.transform;
    }

    private static void Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.layer = parent.gameObject.layer;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material LoadOrCreateMaterial(string name, Color color, Color emission)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        if (emission.maxColorComponent > 0.01f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        UIBuilderKit.EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ───────────────────────── Звуки ─────────────────────────

    /// <summary>Сгенерировать простые звуки рации, если их ещё нет (свои — просто положите поверх или назначьте в RadioCallUI).</summary>
    public static void EnsureSounds()
    {
        UIBuilderKit.EnsureFolder(AudioFolder);
        const int rate = 22050;

        // Писк вызова: два коротких тона.
        WriteIfMissing("radio_ring.wav", rate, t =>
        {
            bool beep = t < 0.11f || (t > 0.19f && t < 0.30f);
            return beep ? 0.35f * Mathf.Sin(2f * Mathf.PI * 1250f * t) * Envelope(t, 0.3f) : 0f;
        }, 0.34f);

        // Ответ: щелчок и короткий треск.
        var noise = new System.Random(7);
        WriteIfMissing("radio_answer.wav", rate, t =>
        {
            float click = t < 0.006f ? 0.8f : 0f;
            float hiss = (float)(noise.NextDouble() * 2.0 - 1.0) * 0.18f * Mathf.Clamp01(1f - t / 0.22f);
            return click + hiss;
        }, 0.24f);

        // Отбой: щелчок вниз по тону.
        WriteIfMissing("radio_hangup.wav", rate, t =>
            0.3f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 400f, t / 0.12f) * t) * Envelope(t, 0.12f), 0.12f);

        AssetDatabase.Refresh();
    }

    private static float Envelope(float t, float length) => Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((length - t) / 0.02f);

    private static void WriteIfMissing(string fileName, int sampleRate, Func<float, float> wave, float seconds)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), AudioFolder, fileName);
        if (File.Exists(path)) return;

        int samples = Mathf.CeilToInt(sampleRate * seconds);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + samples * 2);
        writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);          // PCM
        writer.Write((short)1);          // моно
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);    // байт в секунду
        writer.Write((short)2);          // байт на отсчёт
        writer.Write((short)16);         // бит
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            float value = Mathf.Clamp(wave((float)i / sampleRate), -1f, 1f);
            writer.Write((short)(value * short.MaxValue));
        }
        writer.Flush();
        File.WriteAllBytes(path, stream.ToArray());
    }
}
