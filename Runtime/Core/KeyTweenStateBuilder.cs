using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace KeyTweening
{
    /// <summary>
    /// A state made in code (<see cref="KeyTween.Build"/>): tracks and keys, events, sounds and callbacks, added to the KeyTween
    /// as a runtime state (not saved) and played by name like one made in the editor. <see cref="Add"/> adds it; the Play
    /// methods add it and play it. Calls after that keep editing the same state.
    /// </summary>
    /// <example><code>
    /// anim.Build("Pop")
    ///     .Track(button.transform, KeyTweenProperty.Scale)
    ///         .Key(0f, Vector3.one)
    ///         .Key(0.15f, Vector3.one * 1.2f, KeyTweenEase.OutBack)
    ///         .Key(0.3f, Vector3.one, KeyTweenEase.InOutSine)
    ///     .Play();
    /// </code></example>
    public sealed class KeyTweenStateBuilder
    {
        readonly KeyTween _owner;
        KeyTweenTrack _track;
        bool _added;

        internal KeyTweenStateBuilder(KeyTween owner, string stateName)
        {
            _owner = owner;
            State = new KeyTweenState { name = stateName };
        }

        /// <summary>The state being built.</summary>
        public KeyTweenState State { get; }

        /// <summary>Plays over and over until stopped or replaced (the state's Loop).</summary>
        public KeyTweenStateBuilder Loop(bool loop = true)
        {
            State.loop = loop;
            return this;
        }

        // ---------- tracks: the keys that follow go on the last one started ----------

        /// <summary>Starts a track of a built-in property on <paramref name="target"/>; its Start is Key Value (see <see cref="From"/>).</summary>
        public KeyTweenStateBuilder Track(Component target, KeyTweenProperty property) => Begin(target, KeyTweenTrackTypes.Of(property));
        /// <summary>Starts a Material track: a color, number or vector property of the renderer's material.</summary>
        public KeyTweenStateBuilder Track(Renderer target, string shaderProperty) => Begin(target, KeyTweenTrackRef.MaterialType(target, shaderProperty));
        /// <summary>Starts a custom track (<typeparamref name="TTrack"/>, see "Custom Tracks").</summary>
        public KeyTweenStateBuilder Track<TTrack>(Component target) where TTrack : KeyTweenTrackType =>
            Begin(target, KeyTweenTrackTypes.OfCustom(KeyTweenTrackTypes.TypeKey(typeof(TTrack))));

        KeyTweenStateBuilder Begin(Component target, KeyTweenTrackType type)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _track = new KeyTweenTrack { target = target, start = KeyTweenStart.KeyValue };
            KeyTweenTrackTypes.Assign(_track, type);
            State.tracks.Add(_track);
            return this;
        }

        KeyTweenTrack Current => _track ?? throw new InvalidOperationException($"[KeyTween] State '{State.name}': call Track(...) before adding keys or setting From.");

        /// <summary>Where the track starts: Key Value (its first key, the default), Current Value (the value when it plays), Pointer.</summary>
        public KeyTweenStateBuilder From(KeyTweenStart start)
        {
            Current.start = start;
            return this;
        }

        /// <summary>A key at <paramref name="time"/> seconds, arriving there with <paramref name="ease"/> (Vector3, Vector2 and Color convert to Vector4).</summary>
        public KeyTweenStateBuilder Key(float time, Vector4 value, KeyTweenEase ease = KeyTweenEase.OutSine) =>
            AddKey(new KeyTweenKey { time = time, value = value, curveType = KeyTweenCurveType.Ease, ease = ease });
        /// <summary><see cref="Key(float, Vector4, KeyTweenEase)"/> for one-number properties (x).</summary>
        public KeyTweenStateBuilder Key(float time, float value, KeyTweenEase ease = KeyTweenEase.OutSine) => Key(time, new Vector4(value, 0, 0, 0), ease);

        /// <summary>A key arriving along <paramref name="curve"/> (0 = the previous key's value, 1 = this key's).</summary>
        public KeyTweenStateBuilder KeyCurve(float time, Vector4 value, AnimationCurve curve) =>
            AddKey(new KeyTweenKey { time = time, value = value, curveType = KeyTweenCurveType.Curve, curve = curve });

        /// <summary>A Punch segment ending at <paramref name="time"/>: shakes around the previous value by <paramref name="strength"/> and returns to it.</summary>
        public KeyTweenStateBuilder KeyPunch(float time, Vector4 strength, float frequency = 10f, float damping = 1f) =>
            AddKey(new KeyTweenKey { time = time, value = strength, curveType = KeyTweenCurveType.Punch, frequency = frequency, damping = damping });

        /// <summary>A Shake segment ending at <paramref name="time"/>: like <see cref="KeyPunch"/>, random on each axis every frame.</summary>
        public KeyTweenStateBuilder KeyShake(float time, Vector4 strength, float frequency = 10f, float damping = 1f) =>
            AddKey(new KeyTweenKey { time = time, value = strength, curveType = KeyTweenCurveType.Shake, frequency = frequency, damping = damping });

        // Kept in time order, as the editor keeps them.
        KeyTweenStateBuilder AddKey(KeyTweenKey key)
        {
            var keys = Current.keys;
            var i = keys.FindIndex(k => k.time > key.time);
            keys.Insert(i < 0 ? keys.Count : i, key);
            return this;
        }

        // ---------- markers and callbacks ----------

        /// <summary>An animation event (received with EventRaised, AddListener, WaitEventAsync).</summary>
        public KeyTweenStateBuilder Event(float time, string name)
        {
            if (State.eventTracks.Count == 0) State.eventTracks.Add(new KeyTweenEventTrack());
            State.eventTracks[0].events.Add(new KeyTweenEventMarker { time = time, name = name });
            return this;
        }

        /// <summary>A sound, played through the KeyTween's Audio Source (or the shared 2D one).</summary>
        public KeyTweenStateBuilder Sound(float time, AudioClip clip, float volume = 1f)
        {
            if (State.soundTracks.Count == 0) State.soundTracks.Add(new KeyTweenSoundTrack());
            State.soundTracks[0].sounds.Add(new KeyTweenSound { time = time, clip = clip, volume = volume });
            return this;
        }

        /// <summary>The state's On Start (every play of it).</summary>
        public KeyTweenStateBuilder OnStart(UnityAction action) { State.onStart.AddListener(action); return this; }
        /// <summary>The state's On Complete (every play of it).</summary>
        public KeyTweenStateBuilder OnComplete(UnityAction action) { State.onComplete.AddListener(action); return this; }
        /// <summary>The state's On Cancel (every play of it).</summary>
        public KeyTweenStateBuilder OnCancel(UnityAction action) { State.onCancel.AddListener(action); return this; }

        // ---------- add, play ----------

        /// <summary>Adds the state to the KeyTween (once; a state of the same name is stopped and replaced). Returns the KeyTween.</summary>
        public KeyTween Add()
        {
            if (!_added) _owner.AddState(State);
            _added = true;
            return _owner;
        }

        /// <summary>Adds the state and returns a <see cref="KeyTweenPlay"/> of it, to set up a play.</summary>
        public KeyTweenPlay With()
        {
            Add();
            return _owner.With(State.name);
        }

        public void Play() => Add().Play(State.name);
        public void PlayReversed() => Add().PlayReversed(State.name);
        public Awaitable PlayAsync(CancellationToken cancellationToken = default) => Add().PlayAsync(State.name, cancellationToken);
        public Awaitable PlayReversedAsync(CancellationToken cancellationToken = default) => Add().PlayReversedAsync(State.name, cancellationToken);
    }
}
