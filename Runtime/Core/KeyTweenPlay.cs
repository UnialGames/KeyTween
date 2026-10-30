using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// A play of one state, set up in code before it starts (<see cref="KeyTween.With"/>): a track's end value or arrival,
    /// where it starts, callbacks. Nothing changes until it plays; the changes hold for that play only (put back when it
    /// completes or is canceled), and the same object can be played again and again, like a LitMotion builder you keep.
    /// </summary>
    /// <example><code>
    /// var open = popup.With("Open").WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack).WithOnComplete(OnOpened);
    /// await open.PlayAsync(destroyCancellationToken);
    /// </code></example>
    public sealed class KeyTweenPlay
    {
        enum Kind { EndValue, Ease, Curve, Punch, Shake }

        struct Change
        {
            public KeyTweenTrackRef track;
            public Kind kind;
            public Vector4 value;
            public KeyTweenEase ease;
            public AnimationCurve curve;
            public float frequency, damping;
        }

        // What a change replaced on the track's last key, put back when the play ends.
        struct Saved
        {
            public KeyTweenTrack track;
            public KeyTweenKey key;
            public KeyTweenCurveType curveType;
            public KeyTweenEase ease;
            public AnimationCurve curve;
            public float frequency, damping;
            public bool hasEndValue;
            public Vector4 endValue;
        }

        readonly KeyTween _owner;
        readonly List<Change> _changes = new();
        readonly List<Saved> _saved = new();
        internal Action onStart, onComplete, onCancel, onLoop;

        internal KeyTweenPlay(KeyTween owner, string stateName)
        {
            _owner = owner;
            StateName = stateName;
        }

        /// <summary>The state this plays.</summary>
        public string StateName { get; }
        internal float? Time { get; private set; }
        internal bool EventsBefore { get; private set; }

        KeyTweenPlay Add(KeyTweenTrackRef track, Kind kind, Vector4 value = default, KeyTweenEase ease = default, AnimationCurve curve = null,
            float frequency = 0f, float damping = 0f)
        {
            _changes.Add(new Change { track = track, kind = kind, value = value, ease = ease, curve = curve, frequency = frequency, damping = damping });
            return this;
        }

        // ---------- the track's last key: the end value and how the track arrives there ----------

        /// <summary>The value of the track's last key, for this play (<see cref="KeyTween.SetEndValue(string, Component, KeyTweenProperty, Vector4)"/> for one play).</summary>
        public KeyTweenPlay WithEndValue(Component target, KeyTweenProperty property, Vector4 value) => Add(KeyTweenTrackRef.Of(target, property), Kind.EndValue, value);
        /// <summary><see cref="WithEndValue(Component, KeyTweenProperty, Vector4)"/> for one-number properties (x).</summary>
        public KeyTweenPlay WithEndValue(Component target, KeyTweenProperty property, float value) => WithEndValue(target, property, new Vector4(value, 0, 0, 0));
        /// <summary>The end value of a Material track (the shader property of the renderer's material).</summary>
        public KeyTweenPlay WithEndValue(Renderer target, string shaderProperty, Vector4 value) => Add(KeyTweenTrackRef.Material(target, shaderProperty), Kind.EndValue, value);
        /// <summary>The end value of a custom track (<typeparamref name="TTrack"/>, see "Custom Tracks").</summary>
        public KeyTweenPlay WithEndValue<TTrack>(Component target, Vector4 value) where TTrack : KeyTweenTrackType => Add(KeyTweenTrackRef.Custom<TTrack>(target), Kind.EndValue, value);

        /// <summary>The last key arrives with <paramref name="ease"/> (Arrival Type = Ease), for this play.</summary>
        public KeyTweenPlay WithEase(Component target, KeyTweenProperty property, KeyTweenEase ease) => Add(KeyTweenTrackRef.Of(target, property), Kind.Ease, ease: ease);
        public KeyTweenPlay WithEase(Renderer target, string shaderProperty, KeyTweenEase ease) => Add(KeyTweenTrackRef.Material(target, shaderProperty), Kind.Ease, ease: ease);
        public KeyTweenPlay WithEase<TTrack>(Component target, KeyTweenEase ease) where TTrack : KeyTweenTrackType => Add(KeyTweenTrackRef.Custom<TTrack>(target), Kind.Ease, ease: ease);

        /// <summary>The last key arrives along <paramref name="curve"/> (Arrival Type = Curve: 0 = the previous key's value, 1 = this key's), for this play.</summary>
        public KeyTweenPlay WithCurve(Component target, KeyTweenProperty property, AnimationCurve curve) => Add(KeyTweenTrackRef.Of(target, property), Kind.Curve, curve: curve);
        public KeyTweenPlay WithCurve(Renderer target, string shaderProperty, AnimationCurve curve) => Add(KeyTweenTrackRef.Material(target, shaderProperty), Kind.Curve, curve: curve);
        public KeyTweenPlay WithCurve<TTrack>(Component target, AnimationCurve curve) where TTrack : KeyTweenTrackType => Add(KeyTweenTrackRef.Custom<TTrack>(target), Kind.Curve, curve: curve);

        /// <summary>The last key's segment punches (Arrival Type = Punch; the key's value is the strength), for this play.</summary>
        public KeyTweenPlay WithPunch(Component target, KeyTweenProperty property, float frequency = 10f, float damping = 1f) =>
            Add(KeyTweenTrackRef.Of(target, property), Kind.Punch, frequency: frequency, damping: damping);
        public KeyTweenPlay WithPunch(Renderer target, string shaderProperty, float frequency = 10f, float damping = 1f) =>
            Add(KeyTweenTrackRef.Material(target, shaderProperty), Kind.Punch, frequency: frequency, damping: damping);
        public KeyTweenPlay WithPunch<TTrack>(Component target, float frequency = 10f, float damping = 1f) where TTrack : KeyTweenTrackType =>
            Add(KeyTweenTrackRef.Custom<TTrack>(target), Kind.Punch, frequency: frequency, damping: damping);

        /// <summary>The last key's segment shakes (Arrival Type = Shake; the key's value is the strength), for this play.</summary>
        public KeyTweenPlay WithShake(Component target, KeyTweenProperty property, float frequency = 10f, float damping = 1f) =>
            Add(KeyTweenTrackRef.Of(target, property), Kind.Shake, frequency: frequency, damping: damping);
        public KeyTweenPlay WithShake(Renderer target, string shaderProperty, float frequency = 10f, float damping = 1f) =>
            Add(KeyTweenTrackRef.Material(target, shaderProperty), Kind.Shake, frequency: frequency, damping: damping);
        public KeyTweenPlay WithShake<TTrack>(Component target, float frequency = 10f, float damping = 1f) where TTrack : KeyTweenTrackType =>
            Add(KeyTweenTrackRef.Custom<TTrack>(target), Kind.Shake, frequency: frequency, damping: damping);

        // ---------- how it plays ----------

        /// <summary>Starts at <paramref name="time"/> (seconds), as <see cref="KeyTween.Play(string, float)"/>.</summary>
        public KeyTweenPlay WithTime(float time)
        {
            Time = time;
            return this;
        }

        /// <summary>With <see cref="WithTime"/>: the animation events before that time are raised too, in order, at the start
        /// (like DOTween's GotoWithCallbacks). Sounds and child calls before it are still skipped.</summary>
        public KeyTweenPlay WithEventsBefore(bool raise = true)
        {
            EventsBefore = raise;
            return this;
        }

        /// <summary>Called when this play starts, after the state's On Start.</summary>
        public KeyTweenPlay WithOnStart(Action action) { onStart += action; return this; }
        /// <summary>Called when this play reaches its end or is completed, after the state's On Complete.</summary>
        public KeyTweenPlay WithOnComplete(Action action) { onComplete += action; return this; }
        /// <summary>Called when this play does not reach its end, after the state's On Cancel.</summary>
        public KeyTweenPlay WithOnCancel(Action action) { onCancel += action; return this; }
        /// <summary>A looping state: called each time a lap ends and the next one starts.</summary>
        public KeyTweenPlay WithOnLoop(Action action) { onLoop += action; return this; }

        // ---------- play ----------

        public void Play() => _owner.PlayWith(this, reversed: false);
        public void PlayReversed() => _owner.PlayWith(this, reversed: true);
        public Awaitable PlayAsync(CancellationToken cancellationToken = default) => _owner.PlayAsyncWith(this, reversed: false, cancellationToken);
        public Awaitable PlayReversedAsync(CancellationToken cancellationToken = default) => _owner.PlayAsyncWith(this, reversed: true, cancellationToken);

        // Puts the changes into the state's keys for this play (a track that is not there is logged and skipped).
        internal void Apply(KeyTweenState state)
        {
            _saved.Clear();
            foreach (var change in _changes)
            {
                var track = change.track.Find(state);
                if (track == null || track.keys.Count == 0)
                {
                    Debug.LogWarning($"[KeyTween] '{_owner.name}' has no {change.track} in state '{StateName}'.", _owner);
                    continue;
                }
                var key = track.keys[track.keys.Count - 1];
                _saved.Add(new Saved
                {
                    track = track, key = key, curveType = key.curveType, ease = key.ease, curve = key.curve,
                    frequency = key.frequency, damping = key.damping, hasEndValue = track.hasEndValue, endValue = track.endValue,
                });
                switch (change.kind)
                {
                    case Kind.EndValue:
                        track.hasEndValue = true;
                        track.endValue = change.value;
                        break;
                    case Kind.Ease:
                        key.curveType = KeyTweenCurveType.Ease;
                        key.ease = change.ease;
                        break;
                    case Kind.Curve:
                        key.curveType = KeyTweenCurveType.Curve;
                        key.curve = change.curve;
                        break;
                    default:
                        key.curveType = change.kind == Kind.Punch ? KeyTweenCurveType.Punch : KeyTweenCurveType.Shake;
                        key.frequency = change.frequency;
                        key.damping = change.damping;
                        break;
                }
            }
        }

        // A reversed play keeps the values the last forward play resolved; the end values changed here still apply to it.
        internal void ResolveEnds()
        {
            foreach (var s in _saved) KeyTween.ResolveEnd(s.track);
        }

        // The keys as they were, newest change first (two changes of one key end with its own values).
        internal void Restore()
        {
            for (var i = _saved.Count - 1; i >= 0; i--)
            {
                var s = _saved[i];
                s.key.curveType = s.curveType;
                s.key.ease = s.ease;
                s.key.curve = s.curve;
                s.key.frequency = s.frequency;
                s.key.damping = s.damping;
                s.track.hasEndValue = s.hasEndValue;
                s.track.endValue = s.endValue;
            }
            _saved.Clear();
        }
    }
}
