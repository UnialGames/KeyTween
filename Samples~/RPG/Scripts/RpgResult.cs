using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Victory or defeat popup over the battle page: everything it shows is its "Enter" state. Victory's CONTINUE goes to the
/// formation for the next stage, defeat's RETRY fights the same stage again, FORMATION goes back to the formation.
/// </summary>
public sealed class RpgResult : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public Button primary, formation;
    [Tooltip("Victory: the gold reward (counted by \"Enter\"). Defeat: the stage.")]
    public TMP_Text detail;
    public bool victory;

    void Awake()
    {
        primary.onClick.AddListener(() =>
        {
            if (victory) RpgDemo.Instance.NextStage();
            else RpgDemo.Instance.Retry();
        });
        formation.onClick.AddListener(() => RpgDemo.Instance.ToFormation());
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        var (chapter, stage) = RpgDemo.Instance.SelectedStage;
        // Victory: "Enter" counts the gold up from +0; only the amount comes from here.
        if (victory) GetComponent<KeyTween>().SetEndValue("Enter", detail, KeyTweenProperty.TextNumber, 250 + chapter * 100);
        else detail.text = $"STAGE {chapter + 1}-{stage + 1}";
        return KeyTweenScreen.Completed();
    }
}
