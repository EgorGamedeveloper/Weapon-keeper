using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Применение настроек управления к объектам сцены: чувствительность, инверсия Y и сглаживание мыши —
/// в поля MouseRotator (пропатчен, см. «ПАТЧ Weapon Keeper» в нём: множители поверх rotationSpeed и
/// dampingTime из инспектора, базовые значения не затираются), покачивание камеры — в PlayerHeadBob.
/// Вызывает SettingsService при применении настроек и при загрузке каждой сцены.
/// </summary>
public static class ControlsSettingsApplier
{
    // Исходные амплитуды PlayerHeadBob (высота, раскачка, просадка при приземлении). Выключение
    // покачивания обнуляет их, а не выключает компонент: тот ещё и играет звуки прыжка и приземления.
    private static readonly Dictionary<PlayerHeadBob, Vector3> headBobAmplitudes = new Dictionary<PlayerHeadBob, Vector3>();

    public static void Apply(GameSettingsData settings)
    {
        foreach (MouseRotator rotator in Object.FindObjectsByType<MouseRotator>(FindObjectsInactive.Include))
        {
            rotator.sensitivityMultiplier = settings.mouseSensitivity;
            rotator.verticalSensitivityMultiplier = settings.EffectiveVerticalSensitivity;
            rotator.invertY = settings.invertY;
            rotator.dampingTimeOverride = settings.mouseSmoothing;
        }

        ForgetDestroyed();
        foreach (PlayerHeadBob headBob in Object.FindObjectsByType<PlayerHeadBob>(FindObjectsInactive.Include))
        {
            if (!headBobAmplitudes.TryGetValue(headBob, out Vector3 amplitudes))
            {
                amplitudes = new Vector3(headBob.bobHeight, headBob.bobSide, headBob.landDip);
                headBobAmplitudes[headBob] = amplitudes;
            }

            float scale = settings.headBob ? 1f : 0f;
            headBob.bobHeight = amplitudes.x * scale;
            headBob.bobSide = amplitudes.y * scale;
            headBob.landDip = amplitudes.z * scale;
        }
    }

    /// <summary>Забыть уничтоженные (выгруженные со сценой) компоненты.</summary>
    private static void ForgetDestroyed()
    {
        var dead = new List<PlayerHeadBob>();
        foreach (PlayerHeadBob headBob in headBobAmplitudes.Keys)
            if (headBob == null) dead.Add(headBob);
        foreach (PlayerHeadBob headBob in dead) headBobAmplitudes.Remove(headBob);
    }
}
