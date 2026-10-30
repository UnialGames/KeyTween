using System.Collections.Generic;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>Why a screen is entering or leaving.</summary>
    public enum KeyTweenTransitionKind
    {
        Push,   // a page pushed, a modal opened
        Pop,    // a page popped, a modal closed
        Show,   // a sheet shown
        Hide,   // a sheet hidden
    }

    /// <summary>What a screen gets in its lifecycle calls.</summary>
    public readonly struct KeyTweenTransition
    {
        public readonly KeyTweenTransitionKind Kind;
        /// <summary>The screen leaving while this one enters, or entering while this one leaves (null if none).</summary>
        public readonly KeyTweenScreen Partner;
        /// <summary>Sheets: moving to a lower index (e.g. tab 2 → tab 0). Pages: a pop.</summary>
        public readonly bool Backward;
        public readonly bool Animated;

        public KeyTweenTransition(KeyTweenTransitionKind kind, KeyTweenScreen partner, bool backward, bool animated)
        {
            Kind = kind;
            Partner = partner;
            Backward = backward;
            Animated = animated;
        }
    }

    /// <summary>
    /// Implement on any component of a screen's root to run code around its transitions. The Will* calls are awaited
    /// before the animation starts (load data, preload the next screen); the Did* calls come after it ends.
    /// </summary>
    public interface IKeyTweenScreenLifecycle
    {
        Awaitable WillEnter(KeyTweenTransition transition) => KeyTweenScreen.Completed();
        void DidEnter(KeyTweenTransition transition) { }
        Awaitable WillExit(KeyTweenTransition transition) => KeyTweenScreen.Completed();
        void DidExit(KeyTweenTransition transition) { }
    }

    /// <summary>
    /// A page, modal or sheet shown by a screen container. Its transitions are states of its KeyTween, by name:
    /// pages "PushEnter", "PushExit", "PopEnter", "PopExit"; modals "Enter", "Exit"; sheets "Enter", "Exit"
    /// (and optionally "EnterBackward", "ExitBackward"). A missing one uses the container's default.
    /// </summary>
    [AddComponentMenu("KeyTween/Screen")]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(KeyTween))]
    [DisallowMultipleComponent]
    public sealed class KeyTweenScreen : MonoBehaviour
    {
        [Tooltip("Higher draws on top among the container's screens (e.g. a loading page that stays above the page pushed under it).")]
        public int renderingOrder;

        KeyTween _animation;
        CanvasGroup _group;

        public KeyTween Animation => _animation != null ? _animation : _animation = GetComponent<KeyTween>();
        public CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>An already finished Awaitable, for lifecycle calls that have nothing to wait for.</summary>
        public static Awaitable Completed()
        {
            var source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }

        readonly List<IKeyTweenScreenLifecycle> _lifecycles = new();

        List<IKeyTweenScreenLifecycle> Lifecycles()
        {
            _lifecycles.Clear();
            GetComponents(_lifecycles);
            return _lifecycles;
        }

        internal async Awaitable WillEnter(KeyTweenTransition t)
        {
            foreach (var l in Lifecycles().ToArray()) await l.WillEnter(t);
        }

        internal async Awaitable WillExit(KeyTweenTransition t)
        {
            foreach (var l in Lifecycles().ToArray()) await l.WillExit(t);
        }

        internal void DidEnter(KeyTweenTransition t)
        {
            foreach (var l in Lifecycles().ToArray()) l.DidEnter(t);
        }

        internal void DidExit(KeyTweenTransition t)
        {
            foreach (var l in Lifecycles().ToArray()) l.DidExit(t);
        }

        /// <summary>Fills the container (USN's FillParent): a screen is laid out over the whole container.</summary>
        internal void FillParent()
        {
            var rect = Rect;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        /// <summary>Plays one of the screen's transition states, or the default when the screen has none of that name.</summary>
        internal Awaitable Play(string state, string fallbackState, System.Func<KeyTweenScreen, KeyTweenState> makeDefault, bool animated)
        {
            var anim = Animation;
            if (!anim.HasState(state))
            {
                if (fallbackState != null && anim.HasState(fallbackState)) state = fallbackState;
                else anim.AddState(Rename(makeDefault(this), state));
            }
            if (animated) return anim.PlayAsync(state);
            anim.Stop(state, float.MaxValue);   // no animation: straight to the end pose
            return Completed();
        }

        static KeyTweenState Rename(KeyTweenState s, string name)
        {
            s.name = name;
            return s;
        }
    }
}
