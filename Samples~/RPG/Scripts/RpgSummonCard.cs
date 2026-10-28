using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A summoned card: drops in face down ("Drop"), its back glows in the rarity's color ("Hint" / "HintLegend", looping),
/// then turns over ("Flip", or "FlipEpic" / "FlipLegend" for 4★ / 5★).
/// </summary>
public sealed class RpgSummonCard : MonoBehaviour
{
    public Image portrait, frame;
    [Tooltip("The glow behind the card back: its color tells the rarity before the card turns.")]
    public Image hint;
    public TMP_Text heroName;
    public GameObject[] stars;

    public int Stars { get; private set; }
    public KeyTween Animation => GetComponent<KeyTween>();
    public string FlipState => Stars >= 5 ? "FlipLegend" : Stars == 4 ? "FlipEpic" : "Flip";
    public string HintState => Stars >= 5 ? "HintLegend" : "Hint";

    public void Set(RpgData.Hero hero)
    {
        Stars = hero.stars;
        portrait.sprite = RpgDemo.Instance.Sprite(hero.id);
        var color = RpgData.StarColor(hero.stars);
        frame.color = color;
        hint.color = new Color(color.r, color.g, color.b, 0);   // the alpha is the "Hint" state's
        heroName.text = hero.name;
        for (var i = 0; i < stars.Length; i++) stars[i].SetActive(i < hero.stars);
    }
}
