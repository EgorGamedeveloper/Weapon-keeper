using UnityEngine;

/// <summary>
/// Ветка дерева навыков (вкладка в окне прокачки): «Уборщик», «Выживание», «Оружейник», «Каталог».
/// Создаётся через Assets > Create > Progression > Skill Branch. Сами навыки ссылаются на ветку
/// через SkillData.branch — ветка их не перечисляет, чтобы новый навык добавлялся одним ассетом.
/// </summary>
[CreateAssetMenu(fileName = "NewSkillBranch", menuName = "Progression/Skill Branch", order = 30)]
public class SkillBranch : ScriptableObject
{
    [Header("Отображение")]
    [Tooltip("Название вкладки в окне прокачки.")]
    public string displayName = "Новая ветка";

    [Tooltip("Иконка вкладки.")]
    public Sprite icon;

    [Tooltip("Порядок вкладки слева направо (меньше — левее).")]
    public int order;
}
