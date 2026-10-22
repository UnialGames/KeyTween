using System;
using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle HUD (opened from the formation). The team attacks by itself; each hit shakes the enemy ("Hit") and a damage
/// number pops up from it ("Pop" on a number from a small pool). Skills hit harder and then cool down (the radial
/// fill, "Cooldown"). The enemy's and the team's health bars ease toward their values. A won battle ends with the
/// enemy's "Die", a lost one with the page's "Lose"; then the result popup opens over this page. GIVE UP asks first (the
/// confirm popup). RETRY on the defeat popup starts the same battle again here (Restart).
/// </summary>
public sealed class RpgBattle : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public KeyTween enemy;
    public TMP_Text enemyName;
    public RectTransform enemyHp;
    public Image[] teamPortraits;
    public RectTransform[] teamHp;
    public Button[] skills;
    public Image[] skillIcons;
    public TMP_Text[] numbers;
    public Button giveUp;
    [Min(0.1f)] public float attackSeconds = 0.5f;

    const int AutoDamage = 150, SkillDamage = 400;
    float _enemyMax, _enemy, _team = 1f;
    int _next;
    bool _over;

    public bool Over => _over;

    void Awake()
    {
        for (var i = 0; i < skills.Length; i++)
        {
            var skill = i;
            skills[i].onClick.AddListener(() => Cast(skill));
        }
        giveUp.onClick.AddListener(() => RpgDemo.Instance.Confirm("GIVE UP?", "The stamina is not returned.", () => End(false)));
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        Setup();
        return KeyTweenScreen.Completed();
    }

    void Setup()
    {
        var demo = RpgDemo.Instance;
        var (chapter, stage) = demo.SelectedStage;
        var boss = !RpgDemo.Winnable(stage);
        enemyName.text = boss ? $"DRAGON  ·  {chapter + 1}-{stage + 1}" : $"FIRE WYRM  ·  {chapter + 1}-{stage + 1}";
        _enemyMax = _enemy = boss ? 99999 : 1000;
        for (var i = 0; i < teamPortraits.Length; i++)
        {
            var hero = i < demo.Team.Length ? demo.Team[i] : -1;
            teamPortraits[i].transform.parent.gameObject.SetActive(hero >= 0);
            if (hero >= 0) teamPortraits[i].sprite = demo.Sprite(RpgData.Heroes[hero].id);
        }
        var lead = RpgData.Heroes[demo.Team.Length > 0 ? demo.Team[0] : 0];
        for (var i = 0; i < skillIcons.Length; i++) skillIcons[i].sprite = demo.Sprite(lead.skills[i].icon);
        Bars(true);
    }

    /// <summary>The same battle from the start, on this page (the defeat popup's RETRY): enemy, team and skills back,
    /// and the page fades in from black again ("PushEnter").</summary>
    public void Restart()
    {
        _over = false;
        _team = 1f;
        enemy.Sample("Die", 0f);
        var anim = GetComponent<KeyTween>();
        anim.Sample("Lose", 0f);
        foreach (var skill in skills)
        {
            var cooldown = skill.GetComponent<KeyTween>();
            cooldown.Stop("Cooldown");
            cooldown.Sample("Cooldown", cooldown.Duration("Cooldown"));   // the fill is gone
            skill.interactable = true;
        }
        giveUp.interactable = true;
        Setup();
        anim.Play("PushEnter");
        Fight();
    }

    public void DidEnter(KeyTweenTransition t)
    {
        if (t.Kind == KeyTweenTransitionKind.Push) Fight();
    }

    async void Fight()
    {
        var boss = !RpgDemo.Winnable(RpgDemo.Instance.SelectedStage.stage);
        while (!_over && this != null)
        {
            await Awaitable.WaitForSecondsAsync(attackSeconds);
            if (_over || this == null) return;
            Hit(AutoDamage + UnityEngine.Random.Range(-20, 21));
            if (boss) _team -= 0.12f;   // the dragon hits back hard
            if (_enemy <= 0f) End(true);
            else if (_team <= 0f) End(false);
        }
    }

    void Cast(int skill)
    {
        if (_over) return;
        Cooldown(skills[skill]);
        Hit(SkillDamage);
        if (_enemy <= 0f) End(true);
    }

    async void Cooldown(Button skill)
    {
        skill.interactable = false;
        try { await skill.GetComponent<KeyTween>().PlayAsync("Cooldown"); }
        catch (OperationCanceledException) { return; }   // the battle page closed
        if (this != null && !_over) skill.interactable = true;
    }

    // Where damage numbers pop, in turn: neighbours are at least 190 apart sideways.
    static readonly Vector2[] Lanes = { new(-150, 60), new(130, 0), new(-60, -30), new(160, 80) };

    void Hit(int damage)
    {
        _enemy = Mathf.Max(0f, _enemy - damage);
        enemy.Play("Hit");
        var lane = _next % Lanes.Length;
        var number = numbers[_next++ % numbers.Length];
        number.text = damage.ToString("N0");
        // Numbers in a row land in different places (a random spot can cover the one still showing).
        ((RectTransform)number.transform.parent).anchoredPosition = Lanes[lane] + new Vector2(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(-10f, 10f));
        number.GetComponent<KeyTween>().Play("Pop");
    }

    async void End(bool won)
    {
        if (_over) return;
        _over = true;
        foreach (var s in skills) s.interactable = false;
        giveUp.interactable = false;
        try
        {
            if (won) await enemy.PlayAsync("Die");
            else await GetComponent<KeyTween>().PlayAsync("Lose");
        }
        catch (OperationCanceledException) { return; }
        RpgDemo.Instance.EndBattle(won);
    }

    void Update() => Bars(false);

    // Health bars ease toward their values (the fill's width, so its round ends stay round).
    void Bars(bool snap)
    {
        var k = snap ? 1f : 1f - Mathf.Exp(-10f * Time.deltaTime);
        Ease(enemyHp, _enemy / Mathf.Max(1f, _enemyMax), k);
        foreach (var hp in teamHp) Ease(hp, Mathf.Max(0f, _team), k);
    }

    static void Ease(RectTransform fill, float to, float k)
    {
        var max = fill.anchorMax;
        max.x = Mathf.Lerp(max.x, to, k);
        fill.anchorMax = max;
    }
}
