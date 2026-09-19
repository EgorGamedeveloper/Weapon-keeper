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
        if (board != null) board.OnBroken += HandleBroken;
    }

    private void OnDisable()
    {
        if (board != null) board.OnBroken -= HandleBroken;
    }

    private void HandleBroken(Breakable broken)
    {
        foreach (var go in revealOnBreak)
            if (go != null) go.SetActive(true);
    }
}
