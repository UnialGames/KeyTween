using System;
using KeyTweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Loading: the bar fills ("Progress", its width) and the percentage follows the bar, the rune spins (a looping state), then the
/// lobby opens. There is nothing to load in this sample, so the progress is the state's own time.
/// </summary>
public sealed class RpgLoading : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public TMP_Text tip, percent;
    public RectTransform fill;

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        tip.text = RpgData.Tips[UnityEngine.Random.Range(0, RpgData.Tips.Length)];
        return KeyTweenScreen.Completed();
    }

    public async void DidEnter(KeyTweenTransition t)
    {
        try { await GetComponent<KeyTween>().PlayAsync("Progress"); }
        catch (OperationCanceledException) { return; }
        RpgDemo.Instance.EnterLobby();
    }

    void Update() => percent.text = $"{Mathf.RoundToInt(fill.anchorMax.x * 100f)}%";
}
