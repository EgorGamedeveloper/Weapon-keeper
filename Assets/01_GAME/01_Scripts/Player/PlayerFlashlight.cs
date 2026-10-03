using DG.Tweening;
using UnityEngine;

/// <summary>
/// Налобный фонарик: сначала его нужно подобрать как предмет (ItemData.toolKind == Flashlight) — подбор
/// уводит его прямо в экипировку, как лом или швабру (PlayerItemInteraction.CompletePickup). Дальше
/// клавиша G (GameConfig.input.flashlightKey) включает и выключает его со щелчком — независимо от того,
/// какой инструмент сейчас активен в экипировке: фонарик не занимает руки, только числится в списке
/// (IsOwned проверяет наличие предмета в EquipmentInventory, а не ActiveToolKind).
/// Свет — Spot-источник рядом с камерой: он поворачивается за взглядом с лёгкой инерцией, а не приклеен к
/// экрану, — при резком повороте пятно света чуть догоняет взгляд. Батареи нет — просто светит.
/// Состояние включения не сохраняется (после загрузки фонарик выключен), а вот факт владения — да, через
/// обычный сейв экипировки (SaveGameData.equipment).
///
/// Работает в LateUpdate после покачивания камеры (DefaultExecutionOrder), чтобы свет не отставал на кадр.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Камера игрока: свет смотрит туда же, куда она.")]
    public Camera playerCamera;

    [Tooltip("Spot-источник фонарика. Его яркость в инспекторе — яркость включённого фонарика.")]
    public Light spotLight;

    [Tooltip("Экипировка игрока: фонарик можно включать, только если там есть предмет с toolKind Flashlight.")]
    public EquipmentInventory equipmentInventory;

    [Header("Конфиг")]
    [Tooltip("Если задан — клавиша берётся из GameConfig.input.flashlightKey при старте.")]
    public GameConfig config;

    [Header("Управление")]
    [Tooltip("Клавиша фонарика. Перекрывается GameConfig.input.flashlightKey, если задан конфиг.")]
    public KeyCode toggleKey = KeyCode.G;

    [Tooltip("Включён ли фонарик при старте сцены.")]
    public bool startOn;

    [Header("Свет")]
    [Tooltip("Смещение света от камеры в её осях, м: чуть правее и ниже — как фонарик на голове.")]
    public Vector3 offset = new Vector3(0.12f, -0.08f, 0.05f);

    [Tooltip("Как быстро свет догоняет взгляд: больше — жёстче, 0 — без инерции.")]
    [Min(0f)] public float followSharpness = 16f;

    [Tooltip("За сколько секунд свет разгорается и гаснет.")]
    [Min(0f)] public float fadeDuration = 0.07f;

    [Header("Звуки")]
    [Tooltip("Щелчок включения.")]
    public SoundCue switchOnSound;

    [Tooltip("Щелчок выключения.")]
    public SoundCue switchOffSound;

    /// <summary>Фонарик включён.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Фонарик подобран: в экипировке есть предмет с toolKind Flashlight. Пока не подобран,
    /// клавиша G не включает свет.</summary>
    public bool IsOwned => equipmentInventory != null
        && equipmentInventory.items.Exists(i => i != null && i.itemData != null && i.itemData.toolKind == ToolKind.Flashlight);

    private float onIntensity;
    private Tween fadeTween;

    private void Awake()
    {
        if (config != null) toggleKey = config.input.flashlightKey;
        if (spotLight == null) return;

        onIntensity = spotLight.intensity;
        // startOn имеет смысл, только если фонарик уже подобран (например, восстановлен из сейва
        // до Start этого компонента) — иначе им нечего включать.
        bool on = startOn && IsOwned;
        IsOn = on;
        spotLight.enabled = on;
        spotLight.intensity = on ? onIntensity : 0f;
    }

    private void OnDisable()
    {
        fadeTween?.Kill();
    }

    private void Update()
    {
        // Курсор свободен — открыто окно или пауза: клавиши игрока не работают. Без подобранного
        // фонарика клавиша ничего не делает — включать нечего.
        if (Cursor.lockState == CursorLockMode.Locked && IsOwned && Input.GetKeyDown(toggleKey)) Toggle();
    }

    private void LateUpdate()
    {
        if (spotLight == null || playerCamera == null || !spotLight.enabled) return;

        Transform cam = playerCamera.transform;
        Transform lightTransform = spotLight.transform;
        lightTransform.position = cam.TransformPoint(offset);
        lightTransform.rotation = followSharpness > 0f
            ? Quaternion.Slerp(lightTransform.rotation, cam.rotation, 1f - Mathf.Exp(-followSharpness * Time.deltaTime))
            : cam.rotation;
    }

    /// <summary>Переключить фонарик.</summary>
    public void Toggle() => SetOn(!IsOn);

    /// <summary>Включить или выключить фонарик (со щелчком и коротким разгоранием/угасанием).</summary>
    public void SetOn(bool on)
    {
        if (spotLight == null || IsOn == on) return;
        if (on && !IsOwned) return;
        IsOn = on;
        SoundPlayer.Play2D(on ? switchOnSound : switchOffSound);

        fadeTween?.Kill();
        if (on)
        {
            // Свет включается сразу туда, куда смотрит игрок, — без «доворота» от старой позы.
            if (playerCamera != null)
                spotLight.transform.SetPositionAndRotation(playerCamera.transform.TransformPoint(offset), playerCamera.transform.rotation);
            spotLight.enabled = true;
        }

        fadeTween = DOTween.To(() => spotLight.intensity, value => spotLight.intensity = value, on ? onIntensity : 0f, fadeDuration)
            .OnComplete(() =>
            {
                if (!IsOn) spotLight.enabled = false;
            });
    }
}
