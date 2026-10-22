using System.Collections.Generic;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// A history of pages: Push puts a page on top (the old one leaves at the same time), Pop goes back.
    /// A page pushed with stack = false is dropped from the history once the next page is in (e.g. a loading page).
    /// </summary>
    [AddComponentMenu("KeyTween/Page Container")]
    public sealed class KeyTweenPageContainer : KeyTweenScreenContainer
    {
        readonly List<KeyTweenScreen> _pages = new();
        readonly List<bool> _stacked = new();

        public IReadOnlyList<KeyTweenScreen> Pages => _pages;
        public KeyTweenScreen Current => _pages.Count > 0 ? _pages[^1] : null;

        public async Awaitable<KeyTweenScreen> Push(KeyTweenScreen prefab, bool animated = true, bool stack = true)
        {
            BeginTransition();
            try
            {
                var exit = Current;
                var exitStacked = _stacked.Count > 0 && _stacked[^1];
                var enter = Spawn(prefab);
                enter.Group.alpha = 0f;   // hidden while it gets ready
                _pages.Add(enter);
                _stacked.Add(stack);
                SortScreens(_pages);

                var enterInfo = new KeyTweenTransition(KeyTweenTransitionKind.Push, exit, false, animated);
                var exitInfo = new KeyTweenTransition(KeyTweenTransitionKind.Push, enter, false, animated);
                await Both(enter.WillEnter(enterInfo), exit != null ? exit.WillExit(exitInfo) : null);
                enter.Group.alpha = 1f;
                await Both(enter.Play("PushEnter", null, KeyTweenDefaultTransitions.PagePushEnter, animated),
                    exit != null ? exit.Play("PushExit", null, KeyTweenDefaultTransitions.PagePushExit, animated) : null);

                if (exit != null)
                {
                    exit.gameObject.SetActive(false);
                    exit.DidExit(exitInfo);
                    if (!exitStacked)
                    {
                        var index = _pages.IndexOf(exit);
                        _pages.RemoveAt(index);
                        _stacked.RemoveAt(index);
                        Destroy(exit.gameObject);
                    }
                }
                enter.DidEnter(enterInfo);
                return enter;
            }
            finally { EndTransition(); }
        }

        /// <summary>Back to the previous page; the current one is destroyed.</summary>
        public async Awaitable Pop(bool animated = true)
        {
            if (_pages.Count == 0) return;
            BeginTransition();
            try
            {
                var exit = Current;
                var enter = _pages.Count > 1 ? _pages[^2] : null;
                if (enter != null)
                {
                    enter.gameObject.SetActive(true);
                    enter.Group.alpha = 0f;
                }
                var enterInfo = new KeyTweenTransition(KeyTweenTransitionKind.Pop, exit, true, animated);
                var exitInfo = new KeyTweenTransition(KeyTweenTransitionKind.Pop, enter, true, animated);
                await Both(enter != null ? enter.WillEnter(enterInfo) : null, exit.WillExit(exitInfo));
                if (enter != null) enter.Group.alpha = 1f;
                await Both(enter != null ? enter.Play("PopEnter", null, KeyTweenDefaultTransitions.PagePopEnter, animated) : null,
                    exit.Play("PopExit", null, KeyTweenDefaultTransitions.PagePopExit, animated));

                _pages.RemoveAt(_pages.Count - 1);
                _stacked.RemoveAt(_stacked.Count - 1);
                exit.DidExit(exitInfo);
                Destroy(exit.gameObject);
                if (enter != null) enter.DidEnter(enterInfo);
            }
            finally { EndTransition(); }
        }
    }
}
