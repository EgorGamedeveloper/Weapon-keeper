/// <summary>
/// Статистика игрока, которую считает игра (StatsEventAdapter) и отправляет платформе (Steam). Код обращается
/// к статистике по этому enum, а API-имя в Steamworks задаёт ассет StatDefinition. Значения явные и
/// только дописываются в конец: ассеты хранят число, перестановка перепутала бы статистики.
/// </summary>
public enum StatId
{
    EnemiesKilled = 0,
    ItemsShelved = 1,
    ItemsPickedUp = 2,
    WallsRepaired = 3,
    StainsCleaned = 4,
    ObjectsBroken = 5,
    QuestsCompleted = 6,
    ItemsDelivered = 7,
    MoneyEarned = 8,
    Jumps = 9,
    DistanceWalkedM = 10,
    PlayTimeMin = 11,
    MaxLevel = 12,
    RepairsCompleted = 13,
    DebrisDisposed = 14
}
