using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр всех навыков. Нужен в двух местах: сейв хранит строковые SkillData.skillId, и при загрузке
/// каталог превращает их обратно в ассеты; окно прокачки строит по нему дерево.
///
/// Список собирается автоматически по всем SkillData проекта — как ItemCatalog, по той же причине:
/// забытый в каталоге навык молча пропал бы из дерева и из сейва.
/// </summary>
[CreateAssetMenu(fileName = "SkillCatalog", menuName = "Progression/Skill Catalog", order = 32)]
public class SkillCatalog : ScriptableObject
{
    [Tooltip("Все SkillData проекта. Пересобирается автоматически в редакторе, руками не правится.")]
    public SkillData[] skills = System.Array.Empty<SkillData>();

    private Dictionary<string, SkillData> byId;

    /// <summary>Сбрасываем кеш при загрузке ассета и после перезагрузки домена.</summary>
    private void OnEnable() => byId = null;

    /// <summary>Найти навык по id из сейва. Возвращает null, если id неизвестен.</summary>
    public SkillData GetById(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;

        if (byId == null)
        {
            byId = new Dictionary<string, SkillData>(skills.Length);
            foreach (var skill in skills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;
                byId[skill.skillId] = skill;
            }
        }

        return byId.TryGetValue(skillId, out SkillData found) ? found : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:SkillData");
        var found = new List<SkillData>(guids.Length);
        var seen = new Dictionary<string, SkillData>(guids.Length);

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var skill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill == null) continue;
            found.Add(skill);

            if (string.IsNullOrEmpty(skill.skillId))
            {
                Debug.LogError($"[SkillCatalog] У навыка '{skill.name}' пустой skillId — сейв его не восстановит.", skill);
                continue;
            }

            if (seen.TryGetValue(skill.skillId, out SkillData clash))
                Debug.LogError($"[SkillCatalog] Одинаковый skillId '{skill.skillId}' у '{clash.name}' и '{skill.name}'.", skill);
            else
                seen[skill.skillId] = skill;
        }

        skills = found.ToArray();
        byId = null;
    }
#endif
}
