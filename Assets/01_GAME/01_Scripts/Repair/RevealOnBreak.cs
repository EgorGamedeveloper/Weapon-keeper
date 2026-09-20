using UnityEngine;

/// <summary>
/// Пример сценарного скрипта на поломку: игрок оторвал ломом прибитую доску — за ней
/// открывается точка ремонта разбитого окна/двери, до этого скрытая. Breakable остаётся
/// общим и не знает, что именно он открывает; такой же скрипт позже включит что-то другое.
/// </summary>
public class RevealOnBreak : MonoBehaviour
{
    [Tooltip("Доска/объект, при поломке которого нужно что-то показать.")]
    public Breakable board;

    [Tooltip("Объекты, которые нужно показать после поломки (например, скрытая точка ремонта окна).")]
    public GameObject[] revealOnBreak;

    private void OnEnable()
    {
        if (board == null) return;
        board.OnBroken += HandleBroken;

        // Доска могла быть сломана до того, как мы включились (в том числе восстановлена
        // из сейва) — перечитываем состояние, а не ждём события, которого уже не будет.
        // По аналогии с LightsActivator, который так же перечитывает wiringPoint.IsRepaired.
        if (board.IsBroken) ApplyRevealed();
    }

    private void OnDisable()
    {
        if (board != null) board.OnBroken -= HandleBroken;
    }

    private void HandleBroken(Breakable broken) => ApplyRevealed();

    private void ApplyRevealed()
    {
        foreach (var go in revealOnBreak)
            if (go != null) go.SetActive(true);
    }
}
