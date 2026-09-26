using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Квесты сюжетного графа: ассеты QuestData, которые создаёт и обновляет импорт графа
/// (Tools/Weapon Keeper/Story/Import Story Graph). StoryDirector находит по questId квест ноды графа и
/// регистрирует их все в QuestManager — так их найдёт и сейв. Руками этот список не правят: его
/// перезаписывает каждый импорт.
/// </summary>
[CreateAssetMenu(fileName = "StoryQuestCatalog", menuName = "Story/Story Quest Catalog", order = 60)]
public class StoryQuestCatalog : ScriptableObject
{
    [Tooltip("Квесты графа (заполняет импорт story_graph.json).")]
    public List<QuestData> quests = new List<QuestData>();

    /// <summary>Квест по id (null — нет такого).</summary>
    public QuestData Find(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        foreach (QuestData quest in quests)
            if (quest != null && quest.questId == questId) return quest;
        return null;
    }
}
