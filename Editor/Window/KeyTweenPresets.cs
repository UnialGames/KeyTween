using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Ready-made states to start from instead of an empty timeline (the window's "Presets" button): the common UI motions,
    /// made of ordinary tracks and keys on the KeyTween's own object, so they are edited like any other state afterwards.
    /// Scale and position are relative to the object's rest value, so a preset fits an object that is not at scale 1.
    /// </summary>
    internal static class KeyTweenPresets
    {
        internal sealed class Preset
        {
            public string name;                               // in the menu; no "/" (that would make a submenu)
            public Func<KeyTween, string> unavailable;        // why this object cannot take it (null = it can)
            public Func<KeyTween, List<(KeyTweenState state, KeyTweenEvent? playOn)>> make;
        }

        public static readonly Preset[] All =
        {
            new()
            {
                name = "Hover + Unhover (grow under the pointer)",
                make = a => new()
                {
                    (State("Hover", Scale(a, KeyTweenStart.Current, (0.15f, Uniform(0.08f), KeyTweenEase.OutBack))), KeyTweenEvent.PointerEnter),
                    (State("Unhover", Scale(a, KeyTweenStart.Current, (0.12f, Vector4.zero, KeyTweenEase.OutSine))), KeyTweenEvent.PointerExit),
                },
            },
            new()
            {
                name = "Press + Release (shrink while pressed)",
                make = a => new()
                {
                    (State("Press", Scale(a, KeyTweenStart.Current, (0.08f, Uniform(-0.06f), KeyTweenEase.OutCubic))), KeyTweenEvent.PointerDown),
                    (State("Release", Scale(a, KeyTweenStart.Current, (0.15f, Vector4.zero, KeyTweenEase.OutBack))), KeyTweenEvent.PointerUp),
                },
            },
            new()
            {
                name = "Open + Close (popup: scale and fade)",
                make = a =>
                {
                    var open = State("Open", Scale(a, KeyTweenStart.KeyValue, (0f, Uniform(-0.2f), KeyTweenEase.OutSine), (0.25f, Vector4.zero, KeyTweenEase.OutBack)));
                    var close = State("Close", Scale(a, KeyTweenStart.Current, (0.15f, Uniform(-0.2f), KeyTweenEase.InCubic)));
                    if (Fade(a) is { } fade)
                    {
                        open.tracks.Add(Track(fade.target, fade.property, KeyTweenStart.KeyValue, false, (0f, Vector4.zero, KeyTweenEase.OutSine), (0.15f, One, KeyTweenEase.OutSine)));
                        close.tracks.Add(Track(fade.target, fade.property, KeyTweenStart.Current, false, (0.15f, Vector4.zero, KeyTweenEase.InSine)));
                    }
                    return new() { (open, null), (close, null) };
                },
            },
            new()
            {
                name = "Show + Hide (fade)",
                unavailable = a => Fade(a) == null ? "needs an Image, a Text, a SpriteRenderer or a CanvasGroup" : null,
                make = a =>
                {
                    var fade = Fade(a).Value;
                    return new()
                    {
                        (State("Show", Track(fade.target, fade.property, KeyTweenStart.KeyValue, false, (0f, Vector4.zero, KeyTweenEase.OutSine), (0.2f, One, KeyTweenEase.OutSine))), null),
                        (State("Hide", Track(fade.target, fade.property, KeyTweenStart.Current, false, (0.15f, Vector4.zero, KeyTweenEase.InSine))), null),
                    };
                },
            },
            new()
            {
                name = "Slide In + Slide Out (from below, by its own height)",
                unavailable = a => a.transform is RectTransform ? null : "needs a RectTransform",
                make = a =>
                {
                    var below = new Vector4(0f, -1f);
                    var slideIn = Track(a.transform, KeyTweenProperty.AnchoredPosition, KeyTweenStart.KeyValue, true, (0f, below, KeyTweenEase.OutSine), (0.3f, Vector4.zero, KeyTweenEase.OutCubic));
                    var slideOut = Track(a.transform, KeyTweenProperty.AnchoredPosition, KeyTweenStart.Current, true, (0.25f, below, KeyTweenEase.InCubic));
                    slideIn.sizeRelative = slideOut.sizeRelative = true;
                    return new() { (State("SlideIn", slideIn), null), (State("SlideOut", slideOut), null) };
                },
            },
            new()
            {
                name = "Pulse (loop while enabled)",
                make = a =>
                {
                    var state = State("Pulse", Scale(a, KeyTweenStart.KeyValue,
                        (0f, Vector4.zero, KeyTweenEase.InOutSine), (0.5f, Uniform(0.06f), KeyTweenEase.InOutSine), (1f, Vector4.zero, KeyTweenEase.InOutSine)));
                    state.loop = true;
                    return new() { (state, KeyTweenEvent.OnEnable) };
                },
            },
            new()
            {
                name = "Punch (a quick bump for attention)",
                make = a => new() { (State("Punch", Vibrate(Scale(a, KeyTweenStart.KeyValue, (0f, Vector4.zero, KeyTweenEase.OutSine)), KeyTweenCurveType.Punch, 0.3f, Uniform(0.15f), 6f)), null) },
            },
            new()
            {
                name = "Shake (wrong input)",
                make = a =>
                {
                    var ui = a.transform is RectTransform;   // pixels for UI, units for an object in the world
                    var position = Track(a.transform, ui ? KeyTweenProperty.AnchoredPosition : KeyTweenProperty.Position, KeyTweenStart.KeyValue, true, (0f, Vector4.zero, KeyTweenEase.OutSine));
                    return new() { (State("Shake", Vibrate(position, KeyTweenCurveType.Shake, 0.35f, ui ? new Vector4(12f, 0f) : new Vector4(0.1f, 0f), 12f)), null) };
                },
            },
        };

        static readonly Vector4 One = new(1f, 0f, 0f, 0f);

        static Vector4 Uniform(float xy) => new(xy, xy, 0f, 0f);   // x and y (a UI object's depth stays)

        static KeyTweenState State(string name, params KeyTweenTrack[] tracks)
        {
            var state = new KeyTweenState { name = name };
            state.tracks.AddRange(tracks);
            return state;
        }

        static KeyTweenTrack Scale(KeyTween anim, KeyTweenStart start, params (float time, Vector4 value, KeyTweenEase ease)[] keys) =>
            Track(anim.transform, KeyTweenProperty.Scale, start, true, keys);

        static KeyTweenTrack Track(Component target, KeyTweenProperty property, KeyTweenStart start, bool relativeToRest, params (float time, Vector4 value, KeyTweenEase ease)[] keys)
        {
            var track = new KeyTweenTrack { target = target, property = property, start = start, relativeToRest = relativeToRest };
            foreach (var (time, value, ease) in keys) track.keys.Add(new KeyTweenKey { time = time, value = value, ease = ease });
            return track;
        }

        // A punch or shake segment after the track's keys: the value is its strength, and the track ends back where it was.
        static KeyTweenTrack Vibrate(KeyTweenTrack track, KeyTweenCurveType type, float time, Vector4 strength, float frequency)
        {
            track.keys.Add(new KeyTweenKey { time = time, value = strength, curveType = type, frequency = frequency, damping = 1f });
            return track;
        }

        // What fades the object: the whole group when it has one, else its own graphic.
        static (Component target, KeyTweenProperty property)? Fade(KeyTween anim)
        {
            if (anim.TryGetComponent<CanvasGroup>(out var group)) return (group, KeyTweenProperty.CanvasGroupAlpha);
            if (anim.TryGetComponent<Graphic>(out var graphic)) return (graphic, KeyTweenProperty.GraphicAlpha);
            if (anim.TryGetComponent<SpriteRenderer>(out var sprite)) return (sprite, KeyTweenProperty.SpriteAlpha);
            return null;
        }
    }
}
