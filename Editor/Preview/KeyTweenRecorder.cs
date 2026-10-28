using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Record mode: which track a change made in the Scene view or Inspector belongs to (the serialized field the
    /// Undo system reports → target + property), and the value it had before the change.
    /// </summary>
    internal static class KeyTweenRecorder
    {
        /// <summary>
        /// The track a changed field belongs to. <paramref name="obj"/> is the changed object (a component, or a
        /// GameObject for Active). <paramref name="state"/> decides alpha: an existing Alpha track takes a color's alpha.
        /// </summary>
        public static bool TryMap(Object obj, string path, KeyTweenState state, out Component target, out KeyTweenTrackType type)
        {
            var found = TryMapNew(obj, path, state, out target, out type);
            var (newTarget, newType) = (target, type);
            if (found && state.tracks.Exists(x => x.target == newTarget && x.Type == newType)) return true;
            // A track the state already has for this field takes it (World Position, Offset, Scroll Position, Number …),
            // unless it cannot hold the part that changed (an Alpha track and a color's r).
            var changed = found ? Channels(type, path) : 0;
            foreach (var track in state.tracks)
            {
                if (track.target == null || !Recordable(track.Type) || !Lists(track.Type, track.target, obj, path)) continue;
                if (found && track.Type.Slot != null && track.Type.Slot == type.Slot && (track.Type.ChannelMask & changed) == 0) continue;
                (target, type) = (track.target, track.Type);
                return true;
            }
            return found;
        }

        // Typing a text is not a reveal, and the number in it is recorded only onto a Number track the state already has.
        static bool Recordable(KeyTweenTrackType type) => !KeyTweenTextChars.IsCharProperty(type.Property) && type.Property != KeyTweenProperty.TextReveal;

        static bool Lists(KeyTweenTrackType type, Component target, Object obj, string path)
        {
            Fields.Clear();
            type.SerializedFields(target, Fields);
            return Fields.Exists(f => f.owner == obj && (f.path == path || path.StartsWith(f.path + ".")));
        }

        // The channels of a type a field path is: "m_Color.a" → alpha; a whole field → all of them.
        static int Channels(KeyTweenTrackType type, string path)
        {
            var dot = path.LastIndexOf('.');
            var i = dot < 0 ? -1 : Component(path.Substring(dot + 1));
            return i >= 0 && i < type.Dimensions ? type.ChannelBits(i) : type.ChannelMask;
        }

        // The track a change makes when the state has none for it.
        static bool TryMapNew(Object obj, string path, KeyTweenState state, out Component target, out KeyTweenTrackType type)
        {
            type = null;
            if (TryMapBuiltIn(obj, path, state, out target, out var property))
            {
                type = KeyTweenTrackTypes.Of(property);
                return true;
            }
            // Any other track: the first one offered on that component that lists the field (Light intensity, a custom track …).
            if (obj is not Component component || path.StartsWith("m_sharedMaterial") || path.StartsWith("m_fontMaterial")) return false;
            foreach (var (c, candidate) in KeyTweenPropertyPicker.Properties(component.transform))
            {
                if (c != component || !Recordable(candidate) || candidate.Property == KeyTweenProperty.TextNumber) continue;
                Fields.Clear();
                candidate.SerializedFields(component, Fields);
                foreach (var (owner, field) in Fields)
                    if (owner == component && (field == path || path.StartsWith(field + ".")))
                    {
                        (target, type) = (component, candidate);
                        return true;
                    }
            }
            return false;
        }

        static readonly List<(Object owner, string path)> Fields = new();

        // The fields recorded before track types listed their own: the special cases (rotation, a color's alpha) live here.
        static bool TryMapBuiltIn(Object obj, string path, KeyTweenState state, out Component target, out KeyTweenProperty property)
        {
            target = null;
            property = default;
            var field = path.Split('.')[0];
            switch (obj)
            {
                case GameObject go when field == "m_IsActive":
                    (target, property) = (go.transform, KeyTweenProperty.Active);
                    return true;
                case Transform t:
                    target = t;
                    switch (field)
                    {
                        case "m_LocalScale": property = KeyTweenProperty.Scale; return true;
                        case "m_LocalRotation":
                        case "m_LocalEulerAnglesHint": property = KeyTweenProperty.Rotation; return true;
                    }
                    if (t is not RectTransform) return false;   // its position is a track type of its own (found below)
                    switch (field)
                    {
                        case "m_AnchoredPosition": property = KeyTweenProperty.AnchoredPosition; return true;
                        case "m_SizeDelta": property = KeyTweenProperty.SizeDelta; return true;
                        case "m_AnchorMin": property = KeyTweenProperty.AnchorMin; return true;
                        case "m_AnchorMax": property = KeyTweenProperty.AnchorMax; return true;
                        case "m_Pivot": property = KeyTweenProperty.Pivot; return true;
                    }
                    return false;
                case CanvasGroup group when field == "m_Alpha":
                    (target, property) = (group, KeyTweenProperty.CanvasGroupAlpha);
                    return true;
                case Image image when field == "m_FillAmount":
                    (target, property) = (image, KeyTweenProperty.FillAmount);
                    return true;
                case TMP_Text text when field == "m_fontSize":
                    (target, property) = (text, KeyTweenProperty.FontSize);
                    return true;
                case TMP_Text text when field == "m_characterSpacing":
                    (target, property) = (text, KeyTweenProperty.CharacterSpacing);
                    return true;
                case Graphic graphic when field == (graphic is TMP_Text ? "m_fontColor" : "m_Color"):
                    target = graphic;
                    var alphaOnly = path.EndsWith(".a");
                    var hasAlpha = state.tracks.Exists(x => x.target == graphic && x.property == KeyTweenProperty.GraphicAlpha);
                    var hasColor = state.tracks.Exists(x => x.target == graphic && x.property == KeyTweenProperty.GraphicColor);
                    property = alphaOnly && hasAlpha && !hasColor ? KeyTweenProperty.GraphicAlpha : KeyTweenProperty.GraphicColor;
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The property's value before the change: the value now with each changed component put back to what the Undo
        /// system recorded as its previous value (path → previous, e.g. "m_LocalScale.x" → "1").
        /// </summary>
        public static Vector4 Previous(Component target, Vector4 now, KeyTweenTrackType type, IReadOnlyDictionary<string, string> previous)
        {
            var property = type.Property;
            var before = now;
            // Fields without x/y/z/w (LayoutElement's width and height …): their order in the type's list is the component.
            Fields.Clear();
            type.SerializedFields(target, Fields);
            var quaternion = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
            foreach (var (path, text) in previous)
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) continue;
                var dot = path.LastIndexOf('.');
                var field = dot < 0 ? path : path.Substring(0, dot);
                var listed = Fields.Count == type.Dimensions ? Fields.FindIndex(f => f.path == path) : -1;
                var i = listed >= 0 ? listed : dot < 0 ? 0 : Component(path.Substring(dot + 1));
                if (i < 0) continue;
                if (field == "m_LocalRotation") quaternion[i] = v;
                else if (property == KeyTweenProperty.GraphicAlpha) before.x = v;   // a color's .a, kept as the alpha value
                else before[i] = v;
            }
            // The rotate tool reports the quaternion only: turn it back into angles when all four are known.
            if (property == KeyTweenProperty.Rotation && !previous.ContainsKey("m_LocalEulerAnglesHint.x") && !float.IsNaN(quaternion.x + quaternion.y + quaternion.z + quaternion.w))
            {
                var e = new Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w).eulerAngles;
                before = new Vector4(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z), 0f);
            }
            return before;
        }

        static int Component(string c) => c switch
        {
            "x" or "r" => 0,
            "y" or "g" => 1,
            "z" or "b" or "width" => 2,
            "w" or "a" or "height" => 3,
            _ => -1,
        };
    }
}
