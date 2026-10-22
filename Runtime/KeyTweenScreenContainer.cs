using System.Collections.Generic;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// Shared by the page, modal and sheet containers: taps are off on every container while any of them is in a
    /// transition (so a second tap cannot start another one), and screens get their default transitions from here.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public abstract class KeyTweenScreenContainer : MonoBehaviour
    {
        static readonly List<KeyTweenScreenContainer> Containers = new();
        static int _transitions;

        /// <summary>Some container is between screens.</summary>
        public static bool AnyInTransition => _transitions > 0;
        public bool IsInTransition { get; private set; }

        protected virtual void OnEnable()
        {
            Containers.Add(this);
            ApplyInput();
        }

        protected virtual void OnDisable() => Containers.Remove(this);

        protected void BeginTransition()
        {
            IsInTransition = true;
            _transitions++;
            foreach (var c in Containers) c.ApplyInput();
        }

        protected void EndTransition()
        {
            IsInTransition = false;
            _transitions = Mathf.Max(0, _transitions - 1);
            foreach (var c in Containers) c.ApplyInput();
        }

        void ApplyInput()
        {
            // Raycasts only: turning `interactable` off would draw every Selectable in its disabled color for the
            // length of each transition (all buttons flash grey whenever a popup opens or closes).
            GetComponent<CanvasGroup>().blocksRaycasts = _transitions == 0;
        }

        protected KeyTweenScreen Spawn(KeyTweenScreen prefab)
        {
            var screen = Instantiate(prefab, transform, false);
            screen.FillParent();
            return screen;
        }

        // Highest renderingOrder on top; equal orders keep the order they were added in.
        protected void SortScreens(IList<KeyTweenScreen> screens)
        {
            // Insertion sort: stable, so equal orders keep their order (a swap sort would reorder them).
            var ordered = new List<KeyTweenScreen>(screens);
            for (var i = 1; i < ordered.Count; i++)
            {
                var screen = ordered[i];
                var j = i - 1;
                for (; j >= 0 && ordered[j].renderingOrder > screen.renderingOrder; j--) ordered[j + 1] = ordered[j];
                ordered[j + 1] = screen;
            }
            foreach (var s in ordered) s.transform.SetAsLastSibling();
        }

        protected static async Awaitable Both(Awaitable a, Awaitable b)
        {
            if (a != null) await a;
            if (b != null) await b;
        }
    }
}
