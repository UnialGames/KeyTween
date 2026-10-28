using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KeyTweening
{
    /// <summary>Which clock a KeyTween plays on, like Animator's Update Mode.</summary>
    public enum KeyTweenTimeMode
    {
        Scaled,     // game time: follows Time.timeScale, so pausing the game (timeScale 0) pauses the animation
        Unscaled,   // real time: keeps playing while the game is paused (a pause menu, a loading screen)
    }

    /// <summary>
    /// Holds the animation states of one prefab and plays them.
    /// States can run at the same time; a newly played state takes over only the properties it animates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyTween : MonoBehaviour
    {
        public List<KeyTweenState> states = new();
        [Tooltip("Scaled: follows Time.timeScale, so pausing the game (timeScale 0) pauses these animations, like Animator's Normal update mode. Unscaled: real time, they keep playing while the game is paused (a pause menu). Applies from the next play.")]
        public KeyTweenTimeMode timeMode = KeyTweenTimeMode.Scaled;
        [Tooltip("Optional. Plays sound tracks without their own AudioSource. Empty = a shared 2D AudioSource (PlayOneShot), so no component is needed.")]
        public AudioSource audioSource;
        [Tooltip("Nested KeyTweens' states replaced inside this prefab only (edited from the KeyTween Editor).")]
        public List<KeyTweenOverride> overrides = new();
        // Written by the editor on every prefab import: "<prefab asset GUID>/<path from its root>". Runtime copies
        // (Instantiate) keep it, so edits made to one in Play mode can be saved back to the prefab.
        [SerializeField, HideInInspector] internal string source;

        internal sealed class Playback
        {
            public readonly KeyTween owner;
            public readonly KeyTweenState state;
            public KeyTweenTimerHandle handle;
            public bool active;
            public bool resolved;                                 // key values resolved at least once (Sample reuses them)
            public bool reversed;                                 // playing from the end back to the start
            public float lastTime;                                // events in (lastTime, t] are raised on the next step
            public float duration;                                // state.Duration when it started (not recounted every frame)
            public bool hasMarkers;                               // any sound, child call or event to raise
            public AwaitableCompletionSource completion;           // made on the first PlayAsync, reused after
            public bool awaited;                                  // a PlayAsync is waiting on completion
            public CancellationTokenRegistration cancelRegistration;
            public int group = -1;                                // KeyTweenBatch group while the timer runs, else -1

            public Playback(KeyTween owner, KeyTweenState state)
            {
                this.owner = owner;
                this.state = state;
            }
        }

        /// <summary>
        /// Raised when playback passes an animation event. Skipping to the end (Complete) raises the skipped ones in order;
        /// a canceled play does not. Lambdas need the parameter type: <c>anim.EventRaised += (in KeyTweenEventInfo e) =&gt; ...</c>
        /// </summary>
        public event KeyTweenEventHandler EventRaised;

        /// <summary>Calls <paramref name="handler"/> for events carrying a <typeparamref name="T"/>; dispose the result to stop.</summary>
        public IDisposable AddListener<T>(KeyTweenEventHandler<T> handler) where T : struct
        {
            void OnEvent(in KeyTweenEventInfo e)
            {
                if (e.TryGetData(out T data)) handler(in e, in data);
            }
            EventRaised += OnEvent;
            return new Unsubscriber(this, OnEvent);
        }

        sealed class Unsubscriber : IDisposable
        {
            KeyTween _owner;
            readonly KeyTweenEventHandler _handler;
            public Unsubscriber(KeyTween owner, KeyTweenEventHandler handler) => (_owner, _handler) = (owner, handler);

            public void Dispose()
            {
                if (_owner != null) _owner.EventRaised -= _handler;
                _owner = null;
            }
        }

        /// <summary>Completes with the event the next time any playing state raises <paramref name="eventName"/>.</summary>
        public async Awaitable<KeyTweenEventInfo> WaitEventAsync(string eventName, CancellationToken cancellationToken = default)
        {
            var source = new AwaitableCompletionSource<KeyTweenEventInfo>();
            void OnEvent(in KeyTweenEventInfo e)
            {
                if (e.Name == eventName) source.TrySetResult(e);
            }
            EventRaised += OnEvent;
            using var registration = cancellationToken.Register(() => source.TrySetCanceled());
            try { return await source.Awaitable; }
            finally { EventRaised -= OnEvent; }
        }

        readonly List<Playback> _playbacks = new();
        bool _initialized;

        void PlaySound(KeyTweenSoundTrack track, KeyTweenSound sound)
        {
            var source = track.source != null ? track.source : audioSource != null ? audioSource : SharedAudio;
            source.PlayOneShot(sound.clip, sound.volume);
            SoundPlayed?.Invoke(source, sound);
        }

        internal static Action<AudioSource, KeyTweenSound> SoundPlayed;   // tests: which source played what

        // One hidden 2D AudioSource for every sound without its own source: no AudioSource component per UI object.
        // ponytail: plays on the default mixer route; assign an AudioSource (track or KeyTween) for a mixer group or 3D sound.
        static AudioSource _sharedAudio;

        static AudioSource SharedAudio
        {
            get
            {
                if (_sharedAudio != null) return _sharedAudio;
                var go = new GameObject("KeyTween Shared Audio") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _sharedAudio = go.AddComponent<AudioSource>();
                _sharedAudio.playOnAwake = false;
                _sharedAudio.spatialBlend = 0f;
                return _sharedAudio;
            }
        }

        internal KeyTweenState FindState(string stateName)
        {
            foreach (var s in states)
                if (s.name == stateName) return s;
            return null;
        }

        public bool IsPlaying(string stateName)
        {
            var p = FindPlayback(stateName);
            return p != null && p.active;
        }

        void Awake() => EnsureInitialized();

        void OnDisable()
        {
            foreach (var p in _playbacks) Stop(p);
        }

        /// <summary>
        /// Editor, Play mode: the states were replaced or added (edits saved from another copy). Stops what is playing and
        /// rebuilds the playbacks, keeping the rest values captured on Awake.
        /// </summary>
        internal void Reinitialize()
        {
            var rest = new List<KeyTweenTrack>();
            foreach (var p in _playbacks)
            {
                if (p.active) Stop(p);
                foreach (var t in p.state.tracks)
                    if (t.target != null) rest.Add(t);
            }
            _initialized = false;
            EnsureInitialized();
            foreach (var p in _playbacks)
            foreach (var t in p.state.tracks)
                if (t.target != null && rest.Find(r => r.SameTarget(t)) is { } before) t.rest = before.rest;
        }

        /// <summary>A state has no playback yet (added or renamed after the first play).</summary>
        internal bool HasUnplayableState => _initialized && states.Exists(s => FindPlayback(s.name) == null);

        internal void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _playbacks.Clear();
            foreach (var state in states) _playbacks.Add(new Playback(this, EffectiveState(state.name) ?? state));
            CaptureRest();
        }

        /// <summary>
        /// This state as it plays here: its own tracks, each replaced by an ancestor's override of that track (outermost wins),
        /// plus overridden tracks the state no longer has. Without overrides it is the state itself.
        /// </summary>
        // ponytail: builds a new merged state per call (Playback keeps one; a child call's Duration builds one per play).
        internal KeyTweenState EffectiveState(string stateName)
        {
            var own = FindState(stateName);
            List<KeyTweenTrack> overridden = null;
            for (var t = transform.parent; t != null; t = t.parent)
                foreach (var ancestor in t.GetComponents<KeyTween>())
                foreach (var o in ancestor.overrides)
                {
                    if (o.child != this || o.state != stateName) continue;
                    overridden ??= new List<KeyTweenTrack>();
                    foreach (var track in o.tracks)   // keep climbing: an outer override replaces an inner one of the same track
                    {
                        var i = overridden.FindIndex(x => x.SameTarget(track));
                        if (i >= 0) overridden[i] = track;
                        else overridden.Add(track);
                    }
                }
            if (overridden == null || overridden.Count == 0 || own == null) return own;

            var merged = new KeyTweenState
            {
                name = own.name, eventTracks = own.eventTracks, soundTracks = own.soundTracks, childCalls = own.childCalls,
                tracks = own.tracks.ConvertAll(t => overridden.Find(o => o.SameTarget(t)) ?? t),
            };
            foreach (var track in overridden)
                if (!merged.tracks.Contains(track)) merged.tracks.Add(track);   // the child dropped this track; the override keeps it
            return merged;
        }

        /// <summary>Records the current values as the base for relative tracks. The editor preview calls this on every start.</summary>
        internal void CaptureRest()
        {
            foreach (var state in states)
            {
                CaptureRest(state);
                var effective = EffectiveState(state.name);
                if (effective != state) CaptureRest(effective);
            }
        }

        internal static void CaptureRest(KeyTweenState state)
        {
            foreach (var track in state.tracks)
            {
                track.released = false;
                track.releasedChannels = 0;
                if (track.target != null) track.rest = track.Type.Read(track.target);
            }
        }

        /// <summary>
        /// Plays a state from its end back to its start: values, events, sounds and child calls in reverse order
        /// (child states play reversed too). Key values started from "Current" keep what the last forward play captured,
        /// so Back returns exactly where Open started. Also listed for UnityEvents, e.g. a Back button's OnClick.
        /// </summary>
        public void PlayReversed(string stateName)
        {
            var playback = Prepare(stateName, null, reversed: true);
            if (playback != null) StartMotion(playback);
        }

        /// <summary><see cref="PlayReversed"/> that completes or is canceled like <see cref="PlayAsync"/>.</summary>
        public Awaitable PlayReversedAsync(string stateName, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Done(canceled: true);
            var playback = Prepare(stateName, null, reversed: true);
            if (playback == null) return Done(canceled: false);
            var task = Await(playback, cancellationToken);
            StartMotion(playback);
            return task;
        }

        /// <summary>
        /// Plays a state; the first pose (0 s) is written at once. States already playing give it the properties it
        /// animates and stop there (to finish one first, call <see cref="Complete"/> on it). Also listed for UnityEvents,
        /// e.g. a Button's OnClick.
        /// </summary>
        public void Play(string stateName) => Play(stateName, null);

        /// <summary><see cref="Play(string)"/>; <paramref name="pointer"/> is where tracks that start from the pointer begin.</summary>
        public void Play(string stateName, PointerEventData pointer)
        {
            var playback = Prepare(stateName, pointer);
            if (playback != null) StartMotion(playback);
        }

        /// <summary>
        /// Plays a state and completes when it reaches its end (or <see cref="Complete"/> skips there).
        /// Canceled (OperationCanceledException) when the state does not finish: another state took all of its
        /// properties, it was played again, the object was disabled, or <paramref name="cancellationToken"/> fired.
        /// e.g. <c>await anim.PlayAsync("Close"); Destroy(gameObject);</c> never destroys a popup that was reopened mid-close.
        /// </summary>
        /// <remarks>Unity's <see cref="Awaitable"/>: await it once. With UniTask installed, PlayUniTaskAsync returns a UniTask.</remarks>
        public Awaitable PlayAsync(string stateName, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Done(canceled: true);
            var playback = Prepare(stateName, null);
            if (playback == null) return Done(canceled: false);
            var task = Await(playback, cancellationToken);
            StartMotion(playback);
            return task;
        }

        /// <summary>
        /// Shows a state's pose at <paramref name="time"/> without playing it: no motion, events or sounds. For effects driven
        /// by something other than time, e.g. a scroll list item scaled by its position in the viewport.
        /// The live start values (Start = Current) are taken the first time, then reused.
        /// </summary>
        public void Sample(string stateName, float time)
        {
            EnsureInitialized();
            var p = FindPlayback(stateName);
            if (p == null) return;
            if (p.active) Stop(p);
            if (!p.resolved)
            {
                Resolve(p.state);
                p.resolved = true;
            }
            Evaluate(p.state, Mathf.Clamp(time, 0f, p.state.Duration));
        }

        /// <summary>Length of a state in seconds as it plays here (0 if there is no such state).</summary>
        public float Duration(string stateName)
        {
            EnsureInitialized();
            return FindPlayback(stateName)?.state.Duration ?? 0f;
        }

        /// <summary>Stops a playing state where it is (e.g. a looping one); a pending PlayAsync is canceled.</summary>
        public void Stop(string stateName)
        {
            var playback = FindPlayback(stateName);
            if (playback != null && playback.active) Stop(playback);
        }

        /// <summary>Jumps a playing state to its end; a pending PlayAsync completes normally (e.g. skipping a typewriter).</summary>
        public void Complete(string stateName)
        {
            var playback = FindPlayback(stateName);
            if (playback == null || !playback.active) return;
            playback.handle.TryCancel();
            Evaluate(playback.state, playback.reversed ? 0f : playback.state.Duration);
            Finish(playback);
        }

        internal static void StartMotion(Playback playback)
        {
            var duration = playback.duration;
            if (duration <= 0f)
            {
                Finish(playback);
                return;
            }

            if (playback.state.loop)
            {
                // No end: each lap runs the same resolved values from 0 again, without a gap.
                playback.handle = KeyTweenTimer.Start(float.PositiveInfinity, playback,
                    static (p, elapsed) => Lap((Playback)p, elapsed), scaled: playback.owner.timeMode == KeyTweenTimeMode.Scaled);
                KeyTweenBatch.Register(playback);
                return;
            }

            playback.handle = KeyTweenTimer.Start(duration, playback,
                static (p, elapsed) =>
                {
                    var playback = (Playback)p;
                    Step(playback, playback.reversed ? playback.duration - elapsed : elapsed);
                },
                static p => Finish((Playback)p), scaled: playback.owner.timeMode == KeyTweenTimeMode.Scaled);
            KeyTweenBatch.Register(playback);
        }

        // A looping state at elapsed seconds. Passing the end finishes the lap (its remaining events, sounds and child calls)
        // and starts the next one from the start, so markers at 0 fire again.
        internal static void Lap(Playback p, float elapsed)
        {
            var duration = p.duration;
            var t = elapsed % duration;
            if (p.reversed) t = duration - t;
            var wrapped = p.reversed ? t > p.lastTime : t < p.lastTime;
            if (wrapped)
            {
                RaiseEvents(p, p.reversed ? 0f : duration);
                p.lastTime = p.reversed ? float.MaxValue : -1f;
                p.hasMarkers = p.state.soundTracks.Count > 0 || p.state.childCalls.Count > 0 || p.state.eventTracks.Count > 0;
            }
            Step(p, t);
        }

        /// <summary>One frame of a playback: values, then the child calls and animation events passed since the last frame. Internal so tests can drive time.</summary>
        internal static void Step(Playback p, float t)
        {
            Evaluate(p.state, t, KeyTweenBatch.Ready(p, t));
            RaiseEvents(p, t);
        }

        [ThreadStatic] static int _callDepth;   // parent and child calling each other at the same time stop here

        static void RaiseEvents(Playback p, float t, bool playSounds = true)
        {
            var from = p.lastTime;
            p.lastTime = t;
            if (!p.hasMarkers) return;   // most states: nothing to raise, skip the scans (and the thread-static depth)
            var reversed = p.reversed;
            // Forward: times in (from, t]. Reversed: times in [t, from), so each marker fires once either way.
            bool Passed(float time) => reversed ? time >= t && time < from : time > from && time <= t;
            if (playSounds)
                foreach (var track in p.state.soundTracks)
                foreach (var sound in track.sounds)
                    if (Passed(sound.time) && sound.clip != null) p.owner.PlaySound(track, sound);
            if (_callDepth < KeyTweenState.MaxCallDepth)
            {
                _callDepth++;
                try
                {
                    foreach (var call in p.state.childCalls)
                        if (Passed(call.time) && call.child != null)
                        {
                            if (reversed) call.child.PlayReversed(call.state);
                            else call.child.Play(call.state);
                        }
                }
                finally { _callDepth--; }
            }
            var handler = p.owner.EventRaised;
            if (handler == null) return;
            foreach (var track in p.state.eventTracks)
            foreach (var marker in track.events)
                if (Passed(marker.time)) handler(new KeyTweenEventInfo(p.owner, p.state.name, marker));
        }

        /// <summary>The task for this play; split out so tests can drive time.</summary>
        internal static Awaitable Await(Playback playback, CancellationToken cancellationToken)
        {
            playback.completion ??= new AwaitableCompletionSource();
            playback.completion.Reset();   // the last one was set (Prepare stopped it), so it can be reused
            playback.awaited = true;
            if (cancellationToken.CanBeCanceled)
                playback.cancelRegistration = cancellationToken.Register(
                    static p => Stop((Playback)p), playback);
            return playback.completion.Awaitable;
        }

        // Reached the end: the state stays on its last values.
        internal static void Finish(Playback p)
        {
            // Skipped to the end: the remaining calls and events still happen, in order; skipped sounds stay silent.
            RaiseEvents(p, p.reversed ? float.MinValue : float.MaxValue, playSounds: false);
            foreach (var call in p.state.childCalls)
                if (call.child != null && call.child.IsPlaying(call.state)) call.child.Complete(call.state);
            p.active = false;
            KeyTweenBatch.Unregister(p);
            Release(p)?.TrySetResult();
        }

        // Did not reach the end.
        static void Stop(Playback p)
        {
            p.handle.TryCancel();
            p.active = false;
            KeyTweenBatch.Unregister(p);
            Release(p)?.TrySetCanceled();
        }

        static Awaitable Done(bool canceled)
        {
            var source = new AwaitableCompletionSource();
            if (canceled) source.SetCanceled();
            else source.SetResult();
            return source.Awaitable;
        }

        // Hands the completion source out exactly once per PlayAsync (it is set once, then reused by the next one).
        static AwaitableCompletionSource Release(Playback p)
        {
            if (!p.awaited) return null;
            p.awaited = false;
            var completion = p.completion;
            p.cancelRegistration.Dispose();
            p.cancelRegistration = default;
            return completion;
        }

        /// <summary>Everything Play does except starting the motion. Split out so tests can drive time.</summary>
        internal Playback Prepare(string stateName, PointerEventData pointer, bool reversed = false)
        {
            EnsureInitialized();
            var playback = FindPlayback(stateName);
            if (playback == null)
            {
                Debug.LogWarning($"[KeyTween] '{name}' has no state '{stateName}'.", this);
                return null;
            }

            Stop(playback);
            TakeOver(playback.state);

            foreach (var track in playback.state.tracks)
            {
                track.released = false;
                track.releasedChannels = 0;
            }
            // Reversed keeps the values the last forward play resolved ("Current" = where Open started); first time, resolve now.
            if (!reversed || !playback.resolved) Resolve(playback.state, pointer);
            playback.resolved = true;
            playback.reversed = reversed;
            playback.duration = playback.state.Duration;
            playback.hasMarkers = playback.state.soundTracks.Count > 0 || playback.state.childCalls.Count > 0 || playback.state.eventTracks.Count > 0;
            // Show the first pose right away: e.g. a typewriter after a text change must not show the whole new line for a frame.
            playback.lastTime = reversed ? float.MaxValue : -1f;   // a marker at the starting end fires on this first step
            Step(playback, reversed ? playback.duration : 0f);

            playback.active = true;
            return playback;
        }

        // Other playing states give up what this state animates, where they are: whole properties, or the channels of a shared
        // value (an Alpha state takes the a of a playing Color track, which keeps writing r, g, b). A state left with nothing stops.
        void TakeOver(KeyTweenState incoming)
        {
            foreach (var other in _playbacks)
            {
                if (!other.active || other.state == incoming) continue;

                var changed = false;
                var anyLeft = false;
                foreach (var track in other.state.tracks)
                {
                    if (!track.released && track.target != null)
                    {
                        var taken = TakenChannels(incoming, track);
                        if ((taken & ~track.releasedChannels) != 0)
                        {
                            changed = true;
                            track.releasedChannels |= taken;
                            track.released = (track.Type.ChannelMask & ~track.releasedChannels) == 0;
                        }
                    }
                    anyLeft |= !track.released;
                }
                if (changed && !anyLeft) Stop(other);
            }
        }

        // The channels of other's value that the tracks of state write: all of them for the same property, the shared ones for
        // another property of the same slot (Color and Alpha, Position and Size of a RectTransform …).
        static int TakenChannels(KeyTweenState state, KeyTweenTrack other)
        {
            var otherType = other.Type;
            var taken = 0;
            foreach (var track in state.tracks)
            {
                if (track.target != other.target) continue;
                if (track.SameTarget(other)) taken |= otherType.ChannelMask;
                else if (track.Type.Slot == otherType.Slot) taken |= track.Type.ChannelMask & otherType.ChannelMask;
            }
            return taken;
        }

        /// <summary>
        /// Sets where a track ends from code, for values known only at runtime: e.g. a "Count" state counting a gold label
        /// (Text Number, Start = Current) from what it shows to the new amount. Replaces the last key's value (read the
        /// same way, so relative tracks stay relative) on every play from now on. Call before playing the state.
        /// </summary>
        public void SetEndValue(string stateName, Component target, KeyTweenProperty property, Vector4 value)
        {
            EnsureInitialized();
            var track = FindPlayback(stateName)?.state.tracks.Find(t => t.target == target && t.property == property);
            if (track == null)
            {
                Debug.LogWarning($"[KeyTween] '{name}' has no {property} track on '{(target != null ? target.name : "null")}' in state '{stateName}'.", this);
                return;
            }
            track.hasEndValue = true;
            track.endValue = value;
        }

        /// <summary><see cref="SetEndValue(string, Component, KeyTweenProperty, Vector4)"/> for one-number properties (x).</summary>
        public void SetEndValue(string stateName, Component target, KeyTweenProperty property, float value) =>
            SetEndValue(stateName, target, property, new Vector4(value, 0, 0, 0));

        /// <summary>Whether this animation has a state of that name.</summary>
        public bool HasState(string stateName) => states.Exists(s => s.name == stateName);

        /// <summary>
        /// Adds a state at runtime, e.g. a screen container's default transition for a screen that does not define one.
        /// A state with the same name is replaced. Call it before playing the state.
        /// </summary>
        public void AddState(KeyTweenState state)
        {
            EnsureInitialized();
            states.RemoveAll(s => s.name == state.name);
            _playbacks.RemoveAll(p => p.state.name == state.name);
            states.Add(state);
            var effective = EffectiveState(state.name) ?? state;
            CaptureRest(state);
            if (effective != state) CaptureRest(effective);
            _playbacks.Add(new Playback(this, effective));
        }

        Playback FindPlayback(string stateName)
        {
            foreach (var p in _playbacks)
                if (p.state.name == stateName) return p;
            return null;
        }

        /// <summary>Resolves key values (relative/absolute) and the live start value. Call once right before evaluating from t = 0.</summary>
        internal static void Resolve(KeyTweenState state, PointerEventData pointer = null)
        {
            foreach (var track in state.tracks)
            {
                if (track.target == null || track.keys.Count == 0) continue;
                if (IsText(track.property)) ((TMP_Text)track.target).ForceMeshUpdate();   // character count for duration/stagger
                if (KeyTweenTextChars.IsCharProperty(track.property))
                {
                    // Character values are absolute per character (scale 1 = unchanged); there is no live/rest value per character.
                    foreach (var key in track.keys) key.resolved = key.value;
                    HoldThroughVibrations(track);
                    continue;
                }
                if (track.property == KeyTweenProperty.TextReveal && track.scramble != KeyTweenScramble.None)
                    KeyTweenTextChars.For((TMP_Text)track.target).CaptureSource();
                track.startValue = track.start == KeyTweenStart.Pointer && PointerPosition(track, pointer, out var pressed)
                    ? pressed : track.Type.Read(track.target);
                // Pointer: keys are offsets from the press point (0 = stay there), the only base known for them.
                var size = track.sizeRelative && track.Type.SizeRelative ? ((RectTransform)track.target.transform).rect.size : Vector2.one;
                var last = track.keys[track.keys.Count - 1];
                foreach (var key in track.keys)
                {
                    var raw = key == last && track.hasEndValue ? track.endValue : key.value;
                    var value = new Vector4(raw.x * size.x, raw.y * size.y, raw.z, raw.w);
                    key.resolved = track.start == KeyTweenStart.Pointer ? track.startValue + value
                        : track.relativeToRest ? track.rest + value : value;
                }
                // The first key becomes the live value, so the track starts where the property is. With 2+ keys it holds
                // until its time (= a delay); a lone key keeps its value and is reached through the implicit segment.
                if (track.startFromCurrent && (track.keys.Count > 1 || track.keys[0].time <= 0f)) track.keys[0].resolved = track.startValue;
                HoldThroughVibrations(track);
            }
        }

        // A punch/shake key's value is a strength, not a destination: after it the track is back where it started.
        static void HoldThroughVibrations(KeyTweenTrack track)
        {
            for (var i = 1; i < track.keys.Count; i++)
                if (track.keys[i].IsVibration) track.keys[i].resolved = track.keys[i - 1].resolved;
        }

        /// <summary>Editor preview: resolves a state and every child state it calls.</summary>
        internal static void ResolveTree(KeyTweenState state, int depth = 0)
        {
            Resolve(state);
            if (depth >= KeyTweenState.MaxCallDepth) return;
            foreach (var call in state.childCalls)
                if (call.Target is { } child) ResolveTree(child, depth + 1);
        }

        /// <summary>Editor preview: the state at t, with each called child state at its own time since the call.</summary>
        internal static void EvaluateTree(KeyTweenState state, float t, int depth = 0)
        {
            Evaluate(state, t);
            if (depth >= KeyTweenState.MaxCallDepth) return;
            foreach (var call in state.childCalls)
                if (t >= call.time && call.Target is { } child) EvaluateTree(child, t - call.time, depth + 1);
        }

        /// <summary>
        /// Writes the state at time t. Shared by runtime playback, tests and the editor preview.
        /// <paramref name="batched"/>: this frame's KeyTweenBatch already computed batched tracks at t.
        /// </summary>
        internal static void Evaluate(KeyTweenState state, float t, bool batched = false)
        {
            foreach (var track in state.tracks)
            {
                if (track.released || track.target == null || track.keys.Count == 0) continue;
                if (batched && track.lane >= 0) Write(track, KeyTweenBatch.Value(track.lane));
                else if (KeyTweenTextChars.IsCharProperty(track.property)) KeyTweenTextChars.For((TMP_Text)track.target).Accumulate(track, t);
                else if (track.property == KeyTweenProperty.TextReveal && track.scramble != KeyTweenScramble.None)
                    KeyTweenTextChars.For((TMP_Text)track.target).Scramble(track.Sample(t).x, track.scramble, t);
                else Write(track, track.Sample(t));
            }
            // After every plain write (a reveal changes which characters exist), so character effects land on the final mesh.
            KeyTweenTextChars.FlushAll();
        }

        internal static bool SizeRelativeApplies(KeyTweenTrack track) => track.Type.SizeRelative;

        static bool IsText(KeyTweenProperty p) => p == KeyTweenProperty.TextReveal || KeyTweenTextChars.IsCharProperty(p);

        // The anchored position that puts the track's target under the pointer. Without a pointer (code, OnEnable) it is the current one.
        static bool PointerPosition(KeyTweenTrack track, PointerEventData pointer, out Vector4 position)
        {
            position = default;
            if (pointer == null || !track.Type.PointerStart) return false;
            var rect = (RectTransform)track.target.transform;
            if (rect.parent is not RectTransform space || !RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    space, pointer.position, pointer.pressEventCamera, out var world)) return false;
            var before = rect.anchoredPosition;
            rect.position = world;   // let Unity convert world → anchored (anchors, pivot), then put it back
            position = rect.anchoredPosition;
            rect.anchoredPosition = before;
            return true;
        }

        /// <summary>Reads a built-in property the way a track does (one-number properties in x). Material and custom tracks: use the track's Type.</summary>
        public static Vector4 Read(Component target, KeyTweenProperty property) => KeyTweenTrackTypes.Of(property).Read(target);

        /// <summary>Writes a built-in property the way a track does (one-number properties in x).</summary>
        public static void Write(Component target, KeyTweenProperty property, Vector4 value) => KeyTweenTrackTypes.Of(property).Write(target, value);

        // A track's value; the channels another state took over keep the value they have now.
        internal static void Write(KeyTweenTrack track, Vector4 value)
        {
            var type = track.Type;
            if (track.releasedChannels != 0)
            {
                var now = type.Read(track.target);
                for (var i = 0; i < type.Dimensions; i++)
                    if ((type.ChannelBits(i) & track.releasedChannels) != 0) value[i] = now[i];
            }
            type.Write(track.target, value);
        }
    }
}
