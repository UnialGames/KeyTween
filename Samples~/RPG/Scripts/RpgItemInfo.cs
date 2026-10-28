using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Item info popup with a shared element: the icon grows out of the tapped slot ("Enter") and goes back into it
/// ("Exit"); only the slot's end of each is filled in here.
/// </summary>
public sealed class RpgItemInfo : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public Image icon, frame;
    public TMP_Text itemName, rarity, description, count;
    public Button close, use;

    Image _slotIcon;

    void Awake()
    {
        close.onClick.AddListener(() => RpgDemo.Instance.Back());
        use.onClick.AddListener(() => RpgDemo.Instance.AskQuantity());
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        var demo = RpgDemo.Instance;
        var item = demo.SelectedItem;
        _slotIcon = demo.SelectedIcon.GetComponent<Image>();
        icon.sprite = _slotIcon.sprite;
        var color = RpgData.RarityColor(item.rarity);
        frame.color = color;
        itemName.text = item.name;
        rarity.text = item.rarity.ToString().ToUpperInvariant();
        rarity.color = color;
        description.text = item.text;
        count.text = "Owned: " + demo.Owned(item);
        KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "Enter", icon.rectTransform, demo.SelectedIcon, atStart: true);
        _slotIcon.enabled = false;
        return KeyTweenScreen.Completed();
    }

    public Awaitable WillExit(KeyTweenTransition t)
    {
        KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "Exit", icon.rectTransform, _slotIcon.rectTransform, atStart: false);
        return KeyTweenScreen.Completed();
    }

    public void DidExit(KeyTweenTransition t) => _slotIcon.enabled = true;
}
