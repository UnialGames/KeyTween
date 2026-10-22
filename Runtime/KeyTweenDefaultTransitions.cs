using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// Default transitions, the same as UnityScreenNavigator's defaults: 0.3 s OutQuart. Pages slide by one screen width
    /// (Size Relative, so any resolution), modals grow from 0.3 and fade in, backdrops and sheets fade (Linear).
    /// </summary>
    public static class KeyTweenDefaultTransitions
    {
        public const float Duration = 0.3f;

        static KeyTweenTrack Track(Component target, KeyTweenProperty property, Vector4 from, Vector4 to, KeyTweenEase ease, bool sizeRelative = false) => new()
        {
            target = target, property = property, start = KeyTweenStart.KeyValue, sizeRelative = sizeRelative,
            keys = { new KeyTweenKey { time = 0f, value = from }, new KeyTweenKey { time = Duration, value = to, ease = ease } },
        };

        /// <summary>Slides along x from <paramref name="fromX"/> to <paramref name="toX"/> screen widths.</summary>
        public static KeyTweenState Slide(KeyTweenScreen screen, float fromX, float toX) => new()
        {
            tracks = { Track(screen.Rect, KeyTweenProperty.AnchoredPosition, new Vector4(fromX, 0), new Vector4(toX, 0), KeyTweenEase.OutQuart, sizeRelative: true) },
        };

        public static KeyTweenState PagePushEnter(KeyTweenScreen s) => Slide(s, 1f, 0f);
        public static KeyTweenState PagePushExit(KeyTweenScreen s) => Slide(s, 0f, -1f);
        public static KeyTweenState PagePopEnter(KeyTweenScreen s) => Slide(s, -1f, 0f);
        public static KeyTweenState PagePopExit(KeyTweenScreen s) => Slide(s, 0f, 1f);

        public static KeyTweenState ModalEnter(KeyTweenScreen s) => new()
        {
            tracks =
            {
                Track(s.Rect, KeyTweenProperty.Scale, Vector3.one * 0.3f, Vector3.one, KeyTweenEase.OutQuart),
                Track(s.Group, KeyTweenProperty.CanvasGroupAlpha, Vector4.zero, new Vector4(1, 0), KeyTweenEase.OutQuart),
            },
        };

        public static KeyTweenState ModalExit(KeyTweenScreen s) => new()
        {
            tracks =
            {
                Track(s.Rect, KeyTweenProperty.Scale, Vector3.one, Vector3.one * 0.3f, KeyTweenEase.OutQuart),
                Track(s.Group, KeyTweenProperty.CanvasGroupAlpha, new Vector4(1, 0), Vector4.zero, KeyTweenEase.OutQuart),
            },
        };

        public static KeyTweenState FadeIn(KeyTweenScreen s) => new()
        {
            tracks = { Track(s.Group, KeyTweenProperty.CanvasGroupAlpha, Vector4.zero, new Vector4(1, 0), KeyTweenEase.Linear) },
        };

        public static KeyTweenState FadeOut(KeyTweenScreen s) => new()
        {
            tracks = { Track(s.Group, KeyTweenProperty.CanvasGroupAlpha, new Vector4(1, 0), Vector4.zero, KeyTweenEase.Linear) },
        };
    }
}
