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

    /// <summary>Название вкладки на языке игры (strings.csv, ключ skillbranch.&lt;LocalizationId&gt;.name;
    /// нет строки — displayName).</summary>
    public string DisplayName => Loc.GetOr(Loc.DataKey("skillbranch", LocalizationId, "name"), displayName);

    /// <summary>Id для ключа строки: у ветки нет своего id, поэтому он строится из имени ассета
    /// (SkillBranch_Combat → skillbranch_combat). Переименовали ассет — перевыгрузите тексты (Export Data Texts).</summary>
    public string LocalizationId
    {
        get
        {
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (char c in name.ToLowerInvariant())
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            return builder.ToString();
        }
    }

    [Tooltip("Иконка вкладки.")]
    public Sprite icon;

    [Tooltip("Порядок вкладки слева направо (меньше — левее).")]
    public int order;
}
