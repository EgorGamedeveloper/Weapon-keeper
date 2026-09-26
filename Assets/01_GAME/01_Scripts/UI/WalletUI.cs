using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Счётчик денег в HUD. При изменении число плавно «докручивается» до нового значения, текст
/// «щёлкает» и коротко вспыхивает зелёным (доход) или красным (трата).
/// </summary>
public class WalletUI : MonoBehaviour
{
    [Tooltip("Источник баланса.")]
    public PlayerWallet wallet;

    [Tooltip("Текст баланса.")]
    public Text balanceText;

    [Header("Анимация")]
    [Tooltip("Длительность «докрутки» числа до нового значения.")]
    public float countDuration = 0.5f;

    public Color normalColor = Color.white;
    public Color gainColor = new Color(0.45f, 0.95f, 0.55f, 1f);
    public Color spendColor = new Color(0.95f, 0.45f, 0.35f, 1f);

    private int shown;

    private void OnEnable()
    {
        if (wallet == null) return;
        wallet.OnBalanceChanged += HandleBalanceChanged;

        shown = wallet.Balance;
        SetText(shown);
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.OnBalanceChanged -= HandleBalanceChanged;
        DOTween.Kill(this);
        if (balanceText != null) balanceText.DOKill();
    }

    private void HandleBalanceChanged(int balance)
    {
        if (balanceText == null) return;

        bool gain = balance > shown;
        DOTween.Kill(this);
        DOTween.To(() => shown, v => { shown = v; SetText(v); }, balance, countDuration)
            .SetEase(Ease.OutQuad).SetTarget(this);

        balanceText.DOKill(true);
        balanceText.color = gain ? gainColor : spendColor;
        balanceText.DOColor(normalColor, 0.6f);
        balanceText.transform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 8, 0.7f);
    }

    private void SetText(int value)
    {
        if (balanceText != null) balanceText.text = wallet.Format(value);
    }
}
