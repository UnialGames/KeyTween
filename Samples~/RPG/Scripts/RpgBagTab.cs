using System.Linq;
using KeyTweening;
using UnityEngine;

/// <summary>One page of the bag: a grid (scroll list, nested in the horizontal tab pager) of one tab's items.</summary>
public sealed class RpgBagTab : MonoBehaviour
{
    public KeyTweenScrollList grid;

    public void Show(int tab) =>
        grid.SetData(RpgData.Items.Where(i => i.tab == tab).ToArray(), (RpgItemSlot slot, RpgData.Item item, int _) => slot.Set(item));
}
