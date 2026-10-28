using System.Linq;
using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// How many to use: a wheel picker (scroll list: rows turn on a cylinder and fade away from the center, scrub state
/// "Wheel"; snaps to the center row). The centered number is the amount; USE uses that many.
/// </summary>
public sealed class RpgQuantity : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public KeyTweenScrollList wheel;
    public TMP_Text title, amount;
    public Image icon;
    public Button use, cancel;

    public int Selected => wheel.CurrentIndex + 1;

    void Awake()
    {
        cancel.onClick.AddListener(() => RpgDemo.Instance.Back());
        use.onClick.AddListener(() => RpgDemo.Instance.Use(Selected));
        wheel.CurrentIndexChanged += _ => Show();
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        var demo = RpgDemo.Instance;
        var item = demo.SelectedItem;
        title.text = item.name;
        icon.sprite = demo.Sprite(item.icon);
        var owned = demo.Owned(item);
        wheel.SetData(Enumerable.Range(1, owned).ToArray(), (RpgRow row, int n, int _) => row.title.text = n.ToString());
        wheel.ScrollTo(0, KeyTweenScrollList.Alignment.Center, 0f);
        Show();
        return KeyTweenScreen.Completed();
    }

    void Show()
    {
        amount.text = $"x{Selected}";
        amount.GetComponent<KeyTween>().Play("Tick");
    }
}
