using System.Linq;
using KeyTweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Summon results: the circle flares up ("Enter"), the cards drop in one after another and their backs glow in their
/// rarity's color. Then they turn over one after another — a 4★ glows — and the 5★ come last, one by one: a pause,
/// a shake, a pillar of light, a flash. OK closes when all are shown.
/// Rates: 5★ 10 %, 4★ 30 %, 3★ the rest; ten summons always include a 5★ (the last card, when none came before).
/// </summary>
public sealed class RpgSummonResult : MonoBehaviour, IKeyTweenScreenLifecycle
{
    [Tooltip("The ten cards of a x10 summon.")]
    public RpgSummonCard[] cards;
    [Tooltip("The card of a x1 summon, in the center.")]
    public RpgSummonCard single;
    public Button ok;
    [Min(0)] public float dropInterval = 0.08f;
    [Tooltip("How long the backs glow before the first card turns.")]
    [Min(0)] public float hintSeconds = 0.6f;
    [Tooltip("Between two cards turning (not 5★).")]
    [Min(0)] public float flipInterval = 0.12f;
    [Tooltip("The wait before each 5★ turns.")]
    [Min(0)] public float legendPause = 0.5f;

    /// <summary>Every card is face up (tests).</summary>
    public bool Done { get; private set; }

    void Awake() => ok.onClick.AddListener(() => RpgDemo.Instance.Back());

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        var ten = RpgDemo.Instance.SummonCount == 10;
        single.gameObject.SetActive(!ten);
        if (!ten) single.Set(Roll());
        var legend = false;
        for (var i = 0; i < cards.Length; i++)
        {
            cards[i].gameObject.SetActive(ten);
            if (!ten) continue;
            var hero = Roll(minStars: i == cards.Length - 1 && !legend ? 5 : 3);
            legend |= hero.stars >= 5;
            cards[i].Set(hero);
        }
        ok.interactable = false;
        return KeyTweenScreen.Completed();
    }

    public async void DidEnter(KeyTweenTransition t)
    {
        var shown = single.gameObject.activeSelf ? new[] { single } : cards;
        foreach (var card in shown)
        {
            card.Animation.Play("Drop");
            await Awaitable.WaitForSecondsAsync(dropInterval);
        }
        await Awaitable.WaitForSecondsAsync(0.4f);
        if (this == null) return;
        foreach (var card in shown) card.Animation.Play(card.HintState);
        await Awaitable.WaitForSecondsAsync(hintSeconds);
        // The others turn one after another; the 5★ wait, then turn one by one, each with its own moment.
        var others = shown.Where(c => c.Stars < 5).ToArray();
        for (var i = 0; i < others.Length; i++)
        {
            if (this == null) return;
            var card = others[i];
            card.Animation.Stop(card.HintState);
            if (i == others.Length - 1) await card.Animation.PlayAsync(card.FlipState);
            else
            {
                card.Animation.Play(card.FlipState);
                await Awaitable.WaitForSecondsAsync(flipInterval);
            }
        }
        foreach (var card in shown.Where(c => c.Stars >= 5))
        {
            await Awaitable.WaitForSecondsAsync(legendPause);
            if (this == null) return;
            card.Animation.Stop(card.HintState);
            await card.Animation.PlayAsync(card.FlipState);
        }
        ok.interactable = true;
        GetComponent<KeyTween>().Play("Done");
        Done = true;
    }

    static RpgData.Hero Roll(int minStars = 3)
    {
        var r = Random.value;
        var stars = Mathf.Max(minStars, r < 0.1f ? 5 : r < 0.4f ? 4 : 3);
        var pool = RpgData.Heroes.Where(h => h.stars == stars).ToArray();
        return pool[Random.Range(0, pool.Length)];
    }
}
