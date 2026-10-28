using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace KeyTweening
{
    public enum KeyTweenProperty
    {
        Scale,              // Transform.localScale
        SizeDelta,          // RectTransform.sizeDelta
        AnchoredPosition,   // RectTransform.anchoredPosition
        GraphicColor,       // Graphic.color
        GraphicAlpha,       // Graphic.color.a
        FontSize,           // TMP_Text.fontSize
        TextReveal,         // TMP_Text.maxVisibleCharacters as 0..1 of the text (typewriter)
        CharScale,          // per character, around its center
        CharOffset,         // per character, local position offset
        CharRotation,       // per character, euler angles around its center
        CharColor,          // per character vertex color
        CharAlpha,          // per character vertex alpha
        // appended: values are serialized as ints, so new members go last
        FillAmount,         // Image.fillAmount
        CharacterSpacing,   // TMP_Text.characterSpacing
        Rotation,           // Transform.localEulerAngles (degrees; -180..180 when read)
        CanvasGroupAlpha,   // CanvasGroup.alpha (a whole screen or popup, children included)
        AnchorMin,          // RectTransform.anchorMin
        AnchorMax,          // RectTransform.anchorMax
        Pivot,              // RectTransform.pivot
        CameraBackground,   // Camera.backgroundColor
        CameraOrthoSize,    // Camera.orthographicSize
        Active,             // GameObject.SetActive (x >= 0.5 = active); holds each key until the next one
        // UIEffect (com.coffee.ui-effect), the same set as its own Timeline tracks. Read and written through
        // KeyTweenExternalProperties, registered by KeyTweening.UIEffect when UIEffect is installed.
        UIEffectToneIntensity,
        UIEffectColorIntensity,
        UIEffectColor,
        UIEffectSamplingIntensity,
        UIEffectTransitionRate,
        UIEffectTransitionColor,
        UIEffectEdgeShinyRate,
        UIEffectEdgeColor,
        UIEffectGradationIntensity,
        UIEffectGradationOffset,
        UIEffectGradationRotation,
        UIEffectGradationScale,
        UIEffectDetailIntensity,
        UIEffectDetailColor,
        TextNumber,         // TMP_Text: the first number in the text, counted (the rest stays, e.g. "21/100", "Lv. 3")
        Custom,             // a KeyTweenTrackType class of the project, named by KeyTweenTrack.typeName
        Position,           // Transform.localPosition (objects that are not UI)
        WorldPosition,      // Transform.position
        WorldRotation,      // Transform.eulerAngles
        OffsetMin,          // RectTransform.offsetMin
        OffsetMax,          // RectTransform.offsetMax
        RawImageUVRect,     // RawImage.uvRect as (x, y, width, height)
        ShadowColor,        // Shadow / Outline effectColor
        ShadowDistance,     // Shadow / Outline effectDistance
        RectMaskSoftness,   // RectMask2D.softness
        RectMaskPadding,    // RectMask2D.padding
        SliderValue,        // Slider.value
        ScrollPosition,     // ScrollRect.normalizedPosition
        LayoutMinSize,      // LayoutElement min width, height
        LayoutPreferredSize,
        LayoutFlexibleSize,
        TextWordSpacing,    // TMP_Text.wordSpacing
        TextLineSpacing,    // TMP_Text.lineSpacing
        TextOutlineWidth,   // TMP_Text.outlineWidth (an instance of the font material)
        TextOutlineColor,
        TextFaceDilate,     // the font material's _FaceDilate
        TextUnderlayColor,  // the font material's _UnderlayColor
        SpriteColor,        // SpriteRenderer.color
        SpriteAlpha,
        MaterialColor,      // a Renderer's shader property (KeyTweenTrack.shaderProperty), through a MaterialPropertyBlock
        MaterialFloat,
        MaterialVector,
        LightColor,         // Light
        LightIntensity,
        LightRange,
        LightSpotAngle,
        CameraFieldOfView,  // Camera
        CameraNearClip,
        CameraFarClip,
        CameraRect,
        AudioVolume,        // AudioSource
        AudioPitch,
    }

    public enum KeyTweenCurveType
    {
        Ease,       // an ease preset (KeyTweenEase)
        Curve,      // AnimationCurve over the segment: 0 = previous key's value, 1 = this key's value
        Punch,      // LMotion.Punch: oscillates around the previous value with this key's value as strength, ends back there
        Shake,      // LMotion.Shake: like Punch but each component jitters randomly every frame
    }

    /// <summary>Where a track's first key gets its value when the state starts.</summary>
    public enum KeyTweenStart
    {
        KeyValue,   // the value written in the key
        Current,    // the property's value at that moment, so an interrupted animation never jumps
        Pointer,    // Position only: where the pointer pressed (a ripple from the touch point); later keys are offsets from there
    }

    /// <summary>A value at a time. Ease/curve describe how the track arrives at this key from the previous one.</summary>
    [Serializable]
    public sealed class KeyTweenKey
    {
        public float time;
        public Vector4 value;
        public KeyTweenCurveType curveType = KeyTweenCurveType.Ease;
        public KeyTweenEase ease = KeyTweenEase.OutSine;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Punch/Shake: oscillations per second. The count over the segment is (int)(length x this), like LitMotion's frequency.")]
        public float frequency = 10f;
        [Tooltip("Punch/Shake: how fast the oscillation dies out (LitMotion dampingRatio).")]
        public float damping = 1f;

        [NonSerialized] internal Vector4 resolved;

        public bool IsVibration => curveType is KeyTweenCurveType.Punch or KeyTweenCurveType.Shake;

        internal KeyTweenKey Clone()
        {
            var copy = (KeyTweenKey)MemberwiseClone();
            copy.curve = curve != null ? new AnimationCurve(curve.keys) : null;
            return copy;
        }

        /// <summary>Oscillation count over a segment of this many seconds.</summary>
        internal int Oscillations(float seconds) => (int)(seconds * frequency + 1e-4f);

        /// <summary>
        /// Segment progress (0..1) to interpolation weight. Not clamped, so curves may overshoot.
        /// For Punch/Shake this is the (1-second) envelope, used for drawing; sampling uses the real segment length.
        /// </summary>
        public float Weight(float t01) => curveType switch
        {
            KeyTweenCurveType.Curve when curve != null => curve.Evaluate(t01),
            KeyTweenCurveType.Punch or KeyTweenCurveType.Shake => KeyTweenVibration.Envelope(Oscillations(1f), damping, KeyTweenEasing.Evaluate(t01, ease)),
            _ => KeyTweenEasing.Evaluate(t01, ease),
        };
    }

    [Serializable]
    public sealed class KeyTweenTrack
    {
        public Component target;
        public KeyTweenProperty property;
        [Tooltip("Custom tracks: the KeyTweenTrackType class (\"Namespace.Type, Assembly\").")]
        public string typeName = "";
        [Tooltip("Material tracks: the shader property it animates, e.g. _BaseColor.")]
        public string shaderProperty = "";
        [Tooltip("Where the first key's value comes from when the state starts: the key itself, the current value (no jump when interrupted), or the pointer position (Position only).")]
        [FormerlySerializedAs("startFromCurrent")]   // was a bool: false → KeyValue, true → Current
        public KeyTweenStart start = KeyTweenStart.Current;

        /// <summary>The first key is a live value (current or pointer) rather than the value written in it.</summary>
        public bool startFromCurrent
        {
            get => start != KeyTweenStart.KeyValue;
            set => start = value ? KeyTweenStart.Current : KeyTweenStart.KeyValue;
        }
        [Tooltip("Key values are added to the value the property had on Awake (e.g. -14 = 14 smaller than the original size).")]
        public bool relativeToRest;
        [Tooltip("Position / Size only: key values are multiples of the target's own size (x = 1 is one width), read when the state starts. e.g. a screen sliding in from x = 1.")]
        public bool sizeRelative;
        [Tooltip("Per-character tracks only: each character starts this many seconds after the previous one.")]
        public float stagger;
        [Tooltip("Per-character tracks only: when above 0, character i plays the keys over i x this many seconds (character 0 stays still).")]
        public float durationPerChar;
        [Tooltip("Text Reveal only: characters not yet revealed are shown as random characters (LitMotion WithScrambleChars).")]
        public KeyTweenScramble scramble = KeyTweenScramble.None;
        public List<KeyTweenKey> keys = new();      // sorted by time

        internal KeyTweenTrack Clone()
        {
            var copy = (KeyTweenTrack)MemberwiseClone();
            copy.keys = keys.ConvertAll(k => k.Clone());
            return copy;
        }

        [NonSerialized] internal Vector4 rest;          // value captured on Awake, base for relativeToRest
        [NonSerialized] internal Vector4 startValue;    // value at state start, when startFromCurrent
        [NonSerialized] internal bool released;         // another playing state took every channel of this property over
        [NonSerialized] internal int releasedChannels;  // the slot channels taken over so far (e.g. Alpha took a Color's a)
        [NonSerialized] internal int lane = -1;         // KeyTweenBatch slot while its playback runs batched, else -1
        [NonSerialized] internal bool hasEndValue;      // the last key's value was set from code (KeyTween.SetEndValue)
        [NonSerialized] internal Vector4 endValue;

        [NonSerialized] KeyTweenTrackType _type;
        [NonSerialized] KeyTweenProperty _typeProperty;
        [NonSerialized] string _typeKey;

        /// <summary>What this track animates (read every frame, so it is looked up once and again only after a change).</summary>
        public KeyTweenTrackType Type
        {
            get
            {
                var key = property == KeyTweenProperty.Custom ? typeName : shaderProperty;
                if (_type == null || _typeProperty != property || !ReferenceEquals(_typeKey, key))
                {
                    _type = KeyTweenTrackTypes.Of(this);
                    (_typeProperty, _typeKey) = (property, key);
                }
                return _type;
            }
        }

        /// <summary>The same property of the same object (how overrides and runtime end values find a track).</summary>
        public bool SameTarget(KeyTweenTrack other) => target == other.target && property == other.property
            && (property != KeyTweenProperty.Custom || typeName == other.typeName)
            && (!KeyTweenBuiltInTracks.IsMaterial(property) || shaderProperty == other.shaderProperty);

        public float End
        {
            get
            {
                if (keys.Count == 0) return 0f;
                var end = keys[keys.Count - 1].time;
                if (KeyTweenTextChars.IsCharProperty(property) && target is TMP_Text text && text.textInfo != null)
                {
                    // The last character starts (count - 1) x stagger later and, with durationPerChar, also lasts longest.
                    var last = Mathf.Max(0, text.textInfo.characterCount - 1);
                    end = last * stagger + (durationPerChar > 0f ? last * durationPerChar : end);
                }
                return end;
            }
        }

        /// <summary>
        /// Value at time t. Before the first key it holds (or comes from the start value), after the last it holds.
        /// <paramref name="timeScale"/> stretches the key times (per-character durationPerChar).
        /// </summary>
        internal Vector4 Sample(float t, float timeScale = 1f)
        {
            var first = keys[0];
            var firstTime = first.time * timeScale;
            // A single key is a "go to" track: implicit segment from the live start value to that key.
            if (startFromCurrent && keys.Count == 1 && firstTime > 0f && t < firstTime)
                return Segment(startValue, first, t, firstTime);
            if (t <= firstTime) return first.resolved;   // holds until the first key (a delay when it sits after 0)

            for (var i = 1; i < keys.Count; i++)
            {
                var key = keys[i];
                if (t >= key.time * timeScale) continue;
                var prevTime = keys[i - 1].time * timeScale;
                return Segment(keys[i - 1].resolved, key, t - prevTime, key.time * timeScale - prevTime);
            }
            return keys[keys.Count - 1].resolved;
        }

        // From a value to a key over `span` seconds, at `local` seconds into the segment.
        Vector4 Segment(Vector4 from, KeyTweenKey key, float local, float span)
        {
            var p = span > 0f ? Mathf.Clamp01(local / span) : 1f;
            if (Type.Kind == KeyTweenValueKind.Toggle) return p >= 1f ? key.resolved : from;   // on / off: no in-between
            if (!key.IsVibration) return Vector4.LerpUnclamped(from, key.resolved, key.Weight(p));

            // Punch/Shake: key.value is the strength; the motion oscillates around `from` and ends back on it.
            var envelope = KeyTweenVibration.Envelope(key.Oscillations(span), key.damping, KeyTweenEasing.Evaluate(p, key.ease));
            var strength = key.value * envelope;
            if (key.curveType == KeyTweenCurveType.Shake)
                strength = Vector4.Scale(strength, KeyTweenVibration.RandomSigned(0u, local, Type.Dimensions));
            return from + strength;
        }
    }
}
