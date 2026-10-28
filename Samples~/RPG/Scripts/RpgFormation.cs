using System;
using System.Linq;
using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Formation before a stage (opened by START on the adventure page): tap a hero to put them in the next free slot
/// (the slot's "In": the portrait drops in), tap a slot to take them out (the same state reversed). The team power counts.
/// BATTLE plays the page's "Battle" (the team charges off, the screen fades to black) and opens the battle.
/// </summary>
public sealed class RpgFormation : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public Image[] slotPortraits;
    public Button[] slots, roster;
    public TMP_Text power, stage;
    public Button battle;

    readonly int[] _team = { -1, -1, -1, -1, -1 };
    bool _charging;

    public int TeamSize => _team.Count(h => h >= 0);

    void Awake()
    {
        for (var i = 0; i < roster.Length; i++)
        {
            var hero = i;
            roster[i].onClick.AddListener(() => Add(hero));
        }
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = i;
            slots[i].onClick.AddListener(() => Remove(slot));
        }
        battle.onClick.AddListener(Battle);
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        var demo = RpgDemo.Instance;
        var (chapter, s) = demo.SelectedStage;
        stage.text = $"STAGE {chapter + 1}-{s + 1}";   // also back from the battle: CONTINUE moves on to the next stage
        if (t.Kind != KeyTweenTransitionKind.Push) return KeyTweenScreen.Completed();
        for (var i = 0; i < roster.Length; i++) roster[i].transform.Find("Portrait").GetComponent<Image>().sprite = demo.Sprite(RpgData.Heroes[i].id);
        // The strongest three start in the team.
        for (var i = 0; i < _team.Length; i++) _team[i] = i < 3 ? i : -1;
        for (var i = 0; i < slots.Length; i++)
        {
            if (_team[i] >= 0) slotPortraits[i].sprite = demo.Sprite(RpgData.Heroes[_team[i]].id);
            slots[i].GetComponent<KeyTween>().Sample("In", _team[i] >= 0 ? 1f : 0f);
        }
        power.text = Power().ToString("N0");
        RefreshRoster();
        return KeyTweenScreen.Completed();
    }

    int Power()
    {
        var demo = RpgDemo.Instance;
        return _team.Where(h => h >= 0).Sum(h =>
        {
            var hero = RpgData.Heroes[h];
            return Mathf.RoundToInt((hero.atk + hero.def + hero.hp / 10f + hero.spd) * demo.Level(h) / hero.level);
        });
    }

    void RefreshRoster()
    {
        for (var i = 0; i < roster.Length; i++)
        {
            var inTeam = _team.Contains(i);
            roster[i].interactable = !inTeam;
            roster[i].GetComponent<CanvasGroup>().alpha = inTeam ? 0.35f : 1f;
        }
        battle.interactable = TeamSize > 0;
        // The power counts to the new total (the label's "Count").
        var count = power.GetComponent<KeyTween>();
        count.SetEndValue("Count", power, KeyTweenProperty.TextNumber, Power());
        count.Play("Count");
    }

    void Add(int hero)
    {
        var slot = System.Array.IndexOf(_team, -1);
        if (slot < 0 || _team.Contains(hero) || _charging) return;
        _team[slot] = hero;
        slotPortraits[slot].sprite = RpgDemo.Instance.Sprite(RpgData.Heroes[hero].id);
        slots[slot].GetComponent<KeyTween>().Play("In");
        RefreshRoster();
    }

    void Remove(int slot)
    {
        if (_team[slot] < 0 || _charging) return;
        _team[slot] = -1;
        slots[slot].GetComponent<KeyTween>().PlayReversed("In");
        RefreshRoster();
    }

    async void Battle()
    {
        if (_charging || KeyTweenScreenContainer.AnyInTransition) return;
        var demo = RpgDemo.Instance;
        if (demo.Stamina < RpgDemo.StageStamina)
        {
            battle.GetComponent<KeyTween>().Play("Deny");
            return;
        }
        _charging = true;
        var anim = GetComponent<KeyTween>();
        try { await anim.PlayAsync("Battle"); }
        catch (OperationCanceledException)
        {
            if (this != null) anim.Sample("Battle", 0f);
            _charging = false;
            return;
        }
        var (chapter, s) = demo.SelectedStage;
        demo.Team = _team.Where(h => h >= 0).ToArray();
        if (!demo.BeginBattle(chapter, s)) anim.Sample("Battle", 0f);   // did not open: back from black
        _charging = false;
    }

    // Hidden under the battle now: the team back in place and the black gone, for the result popup over this page.
    // (Not right after BeginBattle: the battle page fades in over this one, which would show the team back early.)
    public void DidExit(KeyTweenTransition t)
    {
        if (t.Kind == KeyTweenTransitionKind.Push) GetComponent<KeyTween>().Sample("Battle", 0f);
    }
}
