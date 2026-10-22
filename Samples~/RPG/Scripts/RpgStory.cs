using KeyTweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Story scene: a speaker's first line slides them in from their side ("EnterLeft" / "EnterRight"); when someone already
/// on screen speaks again they only come forward ("FocusLeft" / "FocusRight"). The name plate pops and the line
/// types itself out ("Type", Text Reveal). A tap finishes the line at once (Complete), the next tap goes on. After the
/// last line two choices drop in ("Choices"); picking one closes the scene and says so (a toast).
/// </summary>
public sealed class RpgStory : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public Image left, right;
    public TMP_Text speaker, line;
    public Button next;
    public Button[] choices;

    int _line = -1;
    bool _leftShown, _rightShown;
    KeyTween _animation;

    public int Line => _line;
    public bool Typing => _animation.IsPlaying("Type");

    void Awake()
    {
        _animation = GetComponent<KeyTween>();
        next.onClick.AddListener(Next);
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = RpgData.StoryChoices[i];
            choices[i].GetComponentInChildren<TMP_Text>().text = choice;
            choices[i].onClick.AddListener(() => Choose(choice));
        }
    }

    public Awaitable WillEnter(KeyTweenTransition t)
    {
        _line = -1;
        _leftShown = _rightShown = false;
        _animation.Sample("Choices", 0f);
        line.text = "";
        speaker.text = "";
        left.color = right.color = new Color(1, 1, 1, 0);
        return KeyTweenScreen.Completed();
    }

    public void DidEnter(KeyTweenTransition t) => Next();

    void Next()
    {
        if (Typing)
        {
            _animation.Complete("Type");   // the whole line at once
            return;
        }
        if (_line + 1 >= RpgData.Story.Length) return;
        _line++;
        var (who, hero, isRight, text) = RpgData.Story[_line];
        var demo = RpgDemo.Instance;
        var portrait = isRight ? right : left;
        var sameSpeaker = _line > 0 && RpgData.Story[_line - 1].who == who;
        portrait.sprite = demo.Sprite(hero);
        speaker.text = who;
        line.text = text;
        if (!sameSpeaker)
        {
            var shown = isRight ? _rightShown : _leftShown;
            _animation.Play((shown ? "Focus" : "Enter") + (isRight ? "Right" : "Left"));
            if (isRight) _rightShown = true;
            else _leftShown = true;
        }
        _animation.Play("Type");
        if (_line == RpgData.Story.Length - 1) ShowChoices();
    }

    async void ShowChoices()
    {
        while (this != null && Typing) await Awaitable.NextFrameAsync();
        if (this != null) _animation.Play("Choices");
    }

    void Choose(string choice)
    {
        var demo = RpgDemo.Instance;
        demo.Back();
        demo.Toast($"You chose: {choice}");
    }
}
