using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace KeyTweening
{
    /// <summary>
    /// A nested KeyTween's state changed inside this prefab only (e.g. the common button flashes differently in this popup).
    /// Only the changed tracks are stored, matched to the child's by target + property; every other track keeps following the
    /// child's own state, so later changes to the child prefab still show up here. Stored on the outer prefab, so the child's
    /// prefab is never touched; for the same track the outermost override wins.
    /// </summary>
    [Serializable]
    public sealed class KeyTweenOverride : ISerializationCallbackReceiver
    {
        public KeyTween child;
        public string state = "";
        public List<KeyTweenTrack> tracks = new();

        // Before per-track overrides: a whole copy of the state. Its tracks become the overridden tracks when loaded.
        [SerializeField, HideInInspector] KeyTweenState replacement;   // same name as before, so both YAML and JSON load it

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (replacement is not { tracks: { Count: > 0 } }) return;
            tracks.AddRange(replacement.tracks);
            replacement = null;
        }

        public KeyTweenTrack Find(KeyTweenTrack childTrack) => tracks.Find(t => t.SameTarget(childTrack));
    }

    /// <summary>Plays another KeyTween's state at a point in this state's time (e.g. a popup showing its buttons).</summary>
    [Serializable]
    public sealed class KeyTweenChildCall
    {
        public float time;
        public KeyTween child;
        public string state = "";

        /// <summary>The state that actually plays: the child's own, or this prefab's override of it.</summary>
        public KeyTweenState Target => child != null ? child.EffectiveState(state) : null;
    }

    [Serializable]
    public sealed class KeyTweenState : ISerializationCallbackReceiver
    {
        internal const int MaxCallDepth = 8;   // a child calling back into its parent is cut here
        [ThreadStatic] static int _durationDepth;

        public string name = "State";
        [Tooltip("Plays over and over until stopped or replaced (like an Animator clip's Loop Time): idle motion, a turning wheel, a pulsing badge. PlayAsync of a looping state completes only when it is completed or stopped.")]
        public bool loop;
        public List<KeyTweenTrack> tracks = new();
        public List<KeyTweenEventTrack> eventTracks = new();
        public List<KeyTweenSoundTrack> soundTracks = new();
        public List<KeyTweenChildCall> childCalls = new();    // sorted by time

        [Tooltip("When a play of this state starts (Play, PlayAsync, PlayReversed): after its first pose, before the events at its start.")]
        public UnityEvent onStart = new();
        [Tooltip("When a play reaches its end, or Complete skips there: after the events at the end, before PlayAsync completes.")]
        public UnityEvent onComplete = new();
        [Tooltip("When a play does not reach its end: Stop, another state took all it animates, the object was disabled, it was played again, or the PlayAsync token was canceled.")]
        public UnityEvent onCancel = new();

        // Before event/sound tracks: one flat list each. Moved into a track when loaded (see OnAfterDeserialize).
        [SerializeField, HideInInspector, FormerlySerializedAs("signals")] List<KeyTweenEventMarker> events = new();
        [SerializeField, HideInInspector] List<KeyTweenSound> sounds = new();

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (events is { Count: > 0 })
            {
                eventTracks.Add(new KeyTweenEventTrack { events = new List<KeyTweenEventMarker>(events) });
                events.Clear();
            }
            if (sounds is { Count: > 0 })
            {
                soundTracks.Add(new KeyTweenSoundTrack { sounds = new List<KeyTweenSound>(sounds) });
                sounds.Clear();
            }
        }

        /// <summary>Adds an animation event to the first event track (creating it if needed).</summary>
        public KeyTweenEventMarker AddEvent(KeyTweenEventMarker marker)
        {
            if (eventTracks.Count == 0) eventTracks.Add(new KeyTweenEventTrack());
            eventTracks[0].events.Add(marker);
            eventTracks[0].events.Sort((a, b) => a.time.CompareTo(b.time));
            return marker;
        }

        /// <summary>Adds a sound to the first sound track (creating one that plays through the KeyTween's AudioSource).</summary>
        public KeyTweenSound AddSound(KeyTweenSound sound)
        {
            if (soundTracks.Count == 0) soundTracks.Add(new KeyTweenSoundTrack());
            soundTracks[0].sounds.Add(sound);
            soundTracks[0].sounds.Sort((a, b) => a.time.CompareTo(b.time));
            return sound;
        }

        /// <summary>A deep copy whose object references (targets, children, clips) still point at the same objects.</summary>
        public KeyTweenState Clone()
        {
            var copy = (KeyTweenState)MemberwiseClone();
            copy.tracks = tracks.ConvertAll(t => t.Clone());
            copy.eventTracks = eventTracks.ConvertAll(t => new KeyTweenEventTrack
            {
                events = t.events.ConvertAll(e => new KeyTweenEventMarker { time = e.time, name = e.name, dataType = e.dataType, dataJson = e.dataJson }),
            });
            copy.soundTracks = soundTracks.ConvertAll(t => new KeyTweenSoundTrack
            {
                source = t.source, sounds = t.sounds.ConvertAll(x => new KeyTweenSound { time = x.time, clip = x.clip, volume = x.volume }),
            });
            copy.childCalls = childCalls.ConvertAll(c => new KeyTweenChildCall { time = c.time, child = c.child, state = c.state });
            // Its own events: editing the copy's callbacks must not change the original's (same targets and methods).
            copy.onStart = Copy(onStart);
            copy.onComplete = Copy(onComplete);
            copy.onCancel = Copy(onCancel);
            copy.events = new List<KeyTweenEventMarker>();
            copy.sounds = new List<KeyTweenSound>();
            return copy;
        }

        static UnityEvent Copy(UnityEvent e) => e == null ? new UnityEvent() : JsonUtility.FromJson<UnityEvent>(JsonUtility.ToJson(e));

        /// <summary>Until the last track, animation event, sound or called child state ends.</summary>
        public float Duration
        {
            get
            {
                var d = 0f;
                foreach (var track in tracks) d = Mathf.Max(d, track.End);
                foreach (var track in eventTracks)
                foreach (var e in track.events) d = Mathf.Max(d, e.time);
                foreach (var track in soundTracks)
                foreach (var sound in track.sounds) d = Mathf.Max(d, sound.End);   // e.g. Destroy after Close must not cut the sound
                if (_durationDepth >= MaxCallDepth) return d;
                _durationDepth++;
                try
                {
                    foreach (var call in childCalls) d = Mathf.Max(d, call.time + (call.Target?.Duration ?? 0f));
                }
                finally { _durationDepth--; }
                return d;
            }
        }
    }
}
