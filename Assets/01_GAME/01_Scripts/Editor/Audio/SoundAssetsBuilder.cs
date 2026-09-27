using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Стандартный набор звуков игры из CC0-паков Kenney (02_ART/Audio/Kenney): ассеты SoundCue
/// (04_Data/Audio/Cues), типы поверхностей (04_Data/Audio/Surfaces) и звуки предметов в ItemData.
/// Меню Tools/Weapon Keeper/Audio/Create Sound Assets пересобирает всё по таблице ниже: вручную
/// подкрученные громкости в этих ассетах затираются — правьте таблицу, если нужно навсегда.
/// Звуки объектов сцены (пятна, доски, игрок, интерфейс) расставляются в сцене и префабах отдельно.
/// </summary>
public static class SoundAssetsBuilder
{
    public const string CuesFolder = "Assets/01_GAME/04_Data/Audio/Cues";
    public const string SurfacesFolder = "Assets/01_GAME/04_Data/Audio/Surfaces";
    private const string KenneyFolder = "Assets/02_ART/Audio/Kenney";

    private const string Impact = "impact-sounds";
    private const string Rpg = "rpg-audio";
    private const string Ui = "interface-sounds";

    private struct CueSpec
    {
        public string name, pack;
        public string[] clips;
        public float volume, pitchMin, pitchMax, minInterval;
        public bool spatial;
        public AudioChannel channel;
    }

    private static readonly List<CueSpec> Specs = new List<CueSpec>();

    private static void Add(string name, string pack, string[] clips, float volume, float pitchMin, float pitchMax,
                            bool spatial = true, AudioChannel channel = AudioChannel.SFX, float minInterval = 0.03f)
    {
        Specs.Add(new CueSpec
        {
            name = name, pack = pack, clips = clips, volume = volume, pitchMin = pitchMin, pitchMax = pitchMax,
            spatial = spatial, channel = channel, minInterval = minInterval,
        });
    }

    /// <summary>Пять вариантов Kenney: prefix_000..prefix_004.</summary>
    private static string[] Five(string prefix) =>
        new[] { prefix + "_000", prefix + "_001", prefix + "_002", prefix + "_003", prefix + "_004" };

    private static void DefineSpecs()
    {
        Specs.Clear();

        // Шаги и приземление — звуки самого игрока, 2D.
        Add("Step_Concrete", Impact, Five("footstep_concrete"), 0.5f, 0.92f, 1.06f, false);
        Add("Step_Wood", Impact, Five("footstep_wood"), 0.5f, 0.92f, 1.06f, false);
        Add("Step_Metal", Impact, Five("impactPlate_light"), 0.22f, 1.05f, 1.2f, false);
        Add("Step_Gravel", Impact, Five("footstep_snow"), 0.45f, 0.92f, 1.06f, false);
        Add("Land_Concrete", Impact, Five("footstep_concrete"), 0.9f, 0.75f, 0.85f, false);
        Add("Land_Wood", Impact, Five("footstep_wood"), 0.9f, 0.75f, 0.85f, false);
        Add("Land_Metal", Impact, Five("impactPlate_medium"), 0.45f, 0.9f, 1f, false);
        Add("Land_Gravel", Impact, Five("footstep_snow"), 0.9f, 0.75f, 0.85f, false);
        Add("Jump", Rpg, new[] { "cloth1", "cloth2", "cloth3", "cloth4" }, 0.35f, 0.95f, 1.1f, false);

        // Предметы: подбор (долетел до руки), установка (встал на место), удар о поверхность.
        Add("Pickup_Generic", Rpg, new[] { "handleSmallLeather", "handleSmallLeather2" }, 0.6f, 0.95f, 1.08f);
        Add("Pickup_Weapon", Rpg, new[] { "metalClick", "metalLatch" }, 0.55f, 0.95f, 1.05f);
        Add("Pickup_Ammo", Rpg, new[] { "handleCoins", "handleCoins2" }, 0.45f, 0.95f, 1.08f);
        Add("Pickup_Wood", Impact, Five("impactWood_light"), 0.3f, 1.1f, 1.25f);
        Add("Pickup_Stone", Impact, Five("impactGeneric_light"), 0.35f, 0.85f, 1f);
        Add("Pickup_Metal", Impact, Five("impactMetal_light"), 0.3f, 1.05f, 1.2f);
        Add("Place_Weapon", Impact, Five("impactMetal_light"), 0.5f, 0.95f, 1.05f);
        Add("Place_Ammo", Impact, Five("impactTin_medium"), 0.45f, 0.95f, 1.08f);
        Add("Place_Wood", Impact, Five("impactPlank_medium"), 0.45f, 0.95f, 1.1f);
        Add("Place_Brick", Impact, Five("impactMining"), 0.45f, 0.95f, 1.1f);
        Add("Place_Box", Impact, Five("impactSoft_medium"), 0.6f, 0.95f, 1.08f);
        Add("Place_Metal", Impact, Five("impactMetal_medium"), 0.45f, 0.9f, 1.05f);
        Add("Impact_Weapon", Impact, Five("impactMetal_light"), 0.8f, 0.9f, 1.1f, minInterval: 0.06f);
        Add("Impact_Ammo", Impact, Five("impactTin_medium"), 0.7f, 0.9f, 1.1f, minInterval: 0.06f);
        Add("Impact_Wood", Impact, Five("impactPlank_medium"), 0.8f, 0.9f, 1.1f, minInterval: 0.06f);
        Add("Impact_Brick", Impact, Five("impactMining"), 0.7f, 0.85f, 1f, minInterval: 0.06f);
        Add("Impact_Box", Impact, Five("impactSoft_heavy"), 0.8f, 0.9f, 1.1f, minInterval: 0.06f);
        Add("Impact_MetalHeavy", Impact, Five("impactMetal_heavy"), 0.8f, 0.9f, 1.05f, minInterval: 0.06f);

        // Работа руками и инструментами.
        Add("Repair_Complete", Ui, new[] { "confirmation_002" }, 0.6f, 1f, 1f, false);
        Add("Stain_Scrub", Rpg, new[] { "cloth1", "cloth2", "cloth3", "cloth4" }, 0.45f, 1.15f, 1.4f, minInterval: 0.05f);
        Add("Stain_Cleaned", Ui, new[] { "pluck_002" }, 0.5f, 1f, 1f, false);
        Add("Pry_Creak", Rpg, new[] { "creak1", "creak2", "creak3" }, 0.7f, 0.9f, 1.1f);
        Add("Break_Wood", Impact, Five("impactWood_heavy"), 0.9f, 0.9f, 1.05f);
        Add("Hit_Wood", Impact, Five("impactWood_medium"), 0.8f, 0.9f, 1.05f);
        Add("Hit_Brick", Impact, Five("impactMining"), 0.9f, 0.8f, 0.9f);
        Add("Break_Brick", Impact, Five("impactMining"), 1f, 0.65f, 0.75f);
        Add("Sledge_Swing", Rpg, new[] { "clothBelt", "clothBelt2" }, 0.5f, 0.7f, 0.8f, false);
        Add("Sledge_HitEnemy", Impact, Five("impactPunch_heavy"), 0.9f, 0.9f, 1.05f);
        Add("Sledge_HitSurface", Impact, Five("impactMining"), 0.85f, 0.75f, 0.85f);
        Add("Mop_Scrub", Rpg, new[] { "cloth1", "cloth2", "cloth3", "cloth4" }, 0.55f, 0.7f, 0.85f, minInterval: 0.05f);
        Add("Wire_Plug", Rpg, new[] { "metalClick" }, 0.6f, 1.15f, 1.25f);
        Add("Wire_Cancel", Ui, new[] { "drop_002" }, 0.45f, 0.95f, 1.05f);
        Add("Wire_Tension", Rpg, new[] { "creak1", "creak2", "creak3" }, 0.5f, 1.25f, 1.4f);
        Add("Wire_Tick", Ui, new[] { "tick_004" }, 0.2f, 0.95f, 1.05f, false);
        Add("Flashlight_On", Ui, new[] { "switch_006" }, 0.5f, 1f, 1.05f, false);
        Add("Flashlight_Off", Ui, new[] { "switch_007" }, 0.45f, 0.95f, 1f, false);

        // Игрок: способности, инвентарь, клик по объекту.
        Add("Vision_On", Ui, new[] { "maximize_006" }, 0.5f, 1f, 1f, false);
        Add("Vision_Off", Ui, new[] { "minimize_006" }, 0.4f, 1f, 1f, false);
        Add("Inventory_Switch", Rpg, new[] { "handleSmallLeather", "handleSmallLeather2" }, 0.35f, 1.05f, 1.2f, false);
        Add("Mode_Switch", Rpg, new[] { "beltHandle1", "beltHandle2" }, 0.45f, 0.95f, 1.05f, false);
        Add("Equip", Rpg, new[] { "metalLatch", "metalClick" }, 0.5f, 0.95f, 1.05f, false);
        Add("Interact", Ui, new[] { "switch_002", "switch_003" }, 0.55f, 0.95f, 1.05f);

        // Интерфейс — канал UI, звучит и на паузе.
        Add("UI_Hover", Ui, new[] { "tick_001", "tick_002" }, 0.25f, 0.95f, 1.05f, false, AudioChannel.UI);
        Add("UI_Click", Ui, new[] { "click_002", "click_003" }, 0.5f, 0.97f, 1.03f, false, AudioChannel.UI);
        Add("UI_Open", Ui, new[] { "open_001", "open_002" }, 0.5f, 1f, 1f, false, AudioChannel.UI);
        Add("UI_Close", Ui, new[] { "close_001", "close_002" }, 0.45f, 1f, 1f, false, AudioChannel.UI);
        Add("XP_Gain", Ui, new[] { "pluck_001" }, 0.4f, 1f, 1.08f, false, AudioChannel.UI, 0.08f);
        Add("Level_Up", Ui, new[] { "confirmation_001" }, 0.6f, 1f, 1f, false, AudioChannel.UI);
    }

    /// <summary>Какие звуки получает предмет: (подбор, установка, удар) по имени ассета ItemData.</summary>
    private static readonly Dictionary<string, (string pickup, string place, string impact)> ItemSounds =
        new Dictionary<string, (string, string, string)>
        {
            { "Item_AmmoBox_556", ("Pickup_Ammo", "Place_Ammo", "Impact_Ammo") },
            { "Item_BoardDebris", ("Pickup_Wood", "Place_Wood", "Impact_Wood") },
            { "Item_Brick", ("Pickup_Stone", "Place_Brick", "Impact_Brick") },
            { "Item_Crowbar", ("Pickup_Metal", "Place_Metal", "Impact_Weapon") },
            { "Item_Sledgehammer", ("Pickup_Metal", "Place_Metal", "Impact_MetalHeavy") },
            { "Item_ElevatorMotor", ("Pickup_Metal", "Place_Metal", "Impact_MetalHeavy") },
            { "Item_GeneratorParts", ("Pickup_Metal", "Place_Metal", "Impact_MetalHeavy") },
            { "Item_Gun", ("Pickup_Weapon", "Place_Weapon", "Impact_Weapon") },
            { "Item_M16", ("Pickup_Weapon", "Place_Weapon", "Impact_Weapon") },
            { "Item_ShippingBox", ("Pickup_Generic", "Place_Box", "Impact_Box") },
            { "Item_Mop", ("Pickup_Wood", "Place_Wood", "Impact_Wood") },
            { "Item_PressureWasher", ("Pickup_Metal", "Place_Metal", "Impact_MetalHeavy") },
            { "Item_WireSpool", ("Pickup_Generic", "Place_Box", "Impact_Box") },
        };

    [MenuItem("Tools/Weapon Keeper/Audio/Create Sound Assets")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    /// <summary>Пересобрать звуки, поверхности и звуки предметов. Возвращает отчёт.</summary>
    public static string Build()
    {
        DefineSpecs();
        EnsureFolder(CuesFolder);
        EnsureFolder(SurfacesFolder);

        var report = new System.Text.StringBuilder();
        foreach (CueSpec spec in Specs)
        {
            SoundCue cue = LoadOrCreate<SoundCue>($"{CuesFolder}/Sound_{spec.name}.asset");
            var clips = new List<AudioClip>();
            foreach (string clipName in spec.clips)
            {
                string path = $"{KenneyFolder}/{spec.pack}/{clipName}.ogg";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
                else report.AppendLine("нет клипа " + path);
            }

            cue.clips = clips.ToArray();
            cue.volume = spec.volume;
            cue.volumeVariation = 0.12f;
            cue.pitchRange = new Vector2(spec.pitchMin, spec.pitchMax);
            cue.spatial = spec.spatial;
            cue.channel = spec.channel;
            cue.minDistance = 1f;
            cue.maxDistance = 20f;
            cue.minInterval = spec.minInterval;
            EditorUtility.SetDirty(cue);
        }

        BuildSurface("Concrete");
        BuildSurface("Wood");
        BuildSurface("Metal");
        BuildSurface("Gravel");

        foreach (var pair in ItemSounds)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/01_GAME/04_Data/Items/{pair.Key}.asset");
            if (item == null) { report.AppendLine("нет предмета " + pair.Key); continue; }
            item.pickupSound = Cue(pair.Value.pickup);
            item.placeSound = Cue(pair.Value.place);
            item.impactSound = Cue(pair.Value.impact);
            EditorUtility.SetDirty(item);
        }

        AssetDatabase.SaveAssets();
        report.AppendLine($"звуков: {Specs.Count}, поверхностей: 4, предметов: {ItemSounds.Count}");
        return report.ToString();
    }

    /// <summary>Готовый звук по короткому имени (Sound_&lt;name&gt;), для расстановки в сцене.</summary>
    public static SoundCue Cue(string name) => AssetDatabase.LoadAssetAtPath<SoundCue>($"{CuesFolder}/Sound_{name}.asset");

    /// <summary>Тип поверхности по имени (Surface_&lt;name&gt;).</summary>
    public static SurfaceType Surface(string name) => AssetDatabase.LoadAssetAtPath<SurfaceType>($"{SurfacesFolder}/Surface_{name}.asset");

    private static void BuildSurface(string name)
    {
        SurfaceType surface = LoadOrCreate<SurfaceType>($"{SurfacesFolder}/Surface_{name}.asset");
        surface.footsteps = Cue("Step_" + name);
        surface.landing = Cue("Land_" + name);
        EditorUtility.SetDirty(surface);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
