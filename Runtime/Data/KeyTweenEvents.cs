using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// Registers a struct as a custom animation event type. Its public fields become the event's parameters, edited per event
    /// in the KeyTween Editor; code receives them with <c>anim.AddListener((in KeyTweenEventInfo e, in HitEvent hit) =&gt; ...)</c>.
    /// <code>[KeyTweenEvent("Hit")] public struct HitEvent { public float damage; public string sfx; }</code>
    /// Fields: numbers, bool, string, Vector2/3, Color, enums (stored as JSON, so no object references).
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
    public sealed class KeyTweenEventAttribute : Attribute
    {
        /// <summary>Shown in the editor's event menu; also the default name of a new event. Empty = the struct name.</summary>
        public string DisplayName { get; }
        public KeyTweenEventAttribute(string displayName = "") => DisplayName = displayName;
    }

    /// <summary>
    /// An animation event: a named point in a state's time, raised once when playback passes it (e.g. play a sound on impact).
    /// It can carry one custom <see cref="KeyTweenEventAttribute"/> struct, stored as JSON and converted once on first use.
    /// </summary>
    [Serializable]
    public sealed class KeyTweenEventMarker
    {
        public float time;
        public string name = "Event";
        public string dataType = "";   // "Namespace.Type, Assembly" of a [KeyTweenEvent] struct, or empty
        public string dataJson = "";

        [NonSerialized] object _data;   // boxed once; reading it copies the struct out without allocating
        [NonSerialized] bool _parsed;

        public static string TypeKey(Type type) => type == null ? "" : $"{type.FullName}, {type.Assembly.GetName().Name}";

        public Type DataType => string.IsNullOrEmpty(dataType) ? null : Type.GetType(dataType);

        internal bool TryGetData<T>(out T data) where T : struct
        {
            if (!_parsed)
            {
                _parsed = true;
                var type = DataType;
                _data = type == null ? null : JsonUtility.FromJson(string.IsNullOrEmpty(dataJson) ? "{}" : dataJson, type);
            }
            if (_data is T value)
            {
                data = value;
                return true;
            }
            data = default;
            return false;
        }

        /// <summary>Call after changing dataType/dataJson (the editor does).</summary>
        public void InvalidateData()
        {
            _parsed = false;
            _data = null;
        }
    }

    /// <summary>What an animation event handler receives (like Unity 6.5's AnimationEventInfo): passed by <c>in</c>, no allocation.</summary>
    public readonly struct KeyTweenEventInfo
    {
        public readonly KeyTween Animation;
        public readonly string State;
        public readonly string Name;
        public readonly float Time;
        readonly KeyTweenEventMarker _marker;

        internal KeyTweenEventInfo(KeyTween animation, string state, KeyTweenEventMarker marker)
        {
            Animation = animation;
            State = state;
            Name = marker.name;
            Time = marker.time;
            _marker = marker;
        }

        /// <summary>The event's custom data, if it carries a <typeparamref name="T"/>.</summary>
        public bool TryGetData<T>(out T data) where T : struct
        {
            if (_marker != null) return _marker.TryGetData(out data);
            data = default;
            return false;
        }
    }

    public delegate void KeyTweenEventHandler(in KeyTweenEventInfo e);
    public delegate void KeyTweenEventHandler<T>(in KeyTweenEventInfo e, in T data) where T : struct;

    /// <summary>A sound played once when playback passes its time, through the KeyTween's AudioSource.</summary>
    [Serializable]
    public sealed class KeyTweenSound
    {
        public float time;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        public float End => time + (clip != null ? clip.length : 0f);
    }

    /// <summary>A track of animation events (added like any other track).</summary>
    [Serializable]
    public sealed class KeyTweenEventTrack
    {
        public List<KeyTweenEventMarker> events = new();   // sorted by time
    }

    /// <summary>A track of sounds played through <see cref="source"/> (empty = the KeyTween's AudioSource).</summary>
    [Serializable]
    public sealed class KeyTweenSoundTrack
    {
        public AudioSource source;
        public List<KeyTweenSound> sounds = new();         // sorted by time
    }
}
