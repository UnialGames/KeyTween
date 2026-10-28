using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace KeyTweening
{
    /// <summary>How a track's value is edited and how many of its four numbers (x, y, z, w) it uses.</summary>
    public enum KeyTweenValueKind
    {
        Float,      // x
        Vector2,    // x, y
        Vector3,    // x, y, z
        Vector4,    // x, y, z, w
        Color,      // r, g, b, a in x, y, z, w
        Toggle,     // x: 0 or 1, held from key to key (no in-between)
    }

    /// <summary>
    /// What a track animates: which component it needs, how to read and write the value, and how the editor shows it.
    /// The built-in tracks are instances of this class; a custom track derives from <see cref="KeyTweenCustomTrack{T}"/>
    /// (or <see cref="KeyTweenFloatTrack{T}"/>, <see cref="KeyTweenVector2Track{T}"/>, <see cref="KeyTweenVector3Track{T}"/>,
    /// <see cref="KeyTweenColorTrack{T}"/>) and then appears in the Add Track window for objects that have <c>T</c>.
    /// </summary>
    [RequireDerived]   // custom tracks are created from their saved type name, so the build must keep them
    public abstract class KeyTweenTrackType
    {
        /// <summary>The component a track of this type animates (Transform for the object itself).</summary>
        public abstract Type ComponentType { get; }

        /// <summary>Shown on the track and in the Add Track window, e.g. "Position".</summary>
        public abstract string Name { get; }

        public abstract KeyTweenValueKind Kind { get; }

        /// <summary>Slider range of a <see cref="KeyTweenValueKind.Float"/> (max &lt;= min: a plain number field).</summary>
        public virtual Vector2 Range => default;

        /// <summary>The track's color in the timeline.</summary>
        public virtual Color TrackColor => new Color32(144, 164, 174, 255);

        /// <summary>Whether <paramref name="target"/> can take this track (e.g. Fill Amount only on a filled Image).</summary>
        public virtual bool Offers(Component target) => true;

        public abstract Vector4 Read(Component target);

        public abstract void Write(Component target, Vector4 value);

        /// <summary>
        /// The serialized fields a write changes, for the editor preview to restore them (AnimationMode) and for recording.
        /// Paths as in the Inspector's serialized data, e.g. (transform, "m_LocalPosition.x"). Values that are not serialized
        /// (a MaterialPropertyBlock) are restored by writing back the value read before the preview.
        /// </summary>
        public virtual void SerializedFields(Component target, List<(UnityEngine.Object owner, string path)> fields) { }

        /// <summary>
        /// Tracks that write the same underlying value share a slot, e.g. Color and Alpha both write Graphic.color.
        /// A newly played state takes over the channels of a shared slot that it writes (see <see cref="ChannelBits"/>).
        /// </summary>
        public virtual object Slot => this;

        /// <summary>The slot channels component <paramref name="component"/> (0 = x … 3 = w) of the value writes, as bits.</summary>
        public virtual int ChannelBits(int component) => 1 << component;

        /// <summary>How many of x, y, z, w the value uses.</summary>
        public int Dimensions => Kind switch
        {
            KeyTweenValueKind.Vector2 => 2,
            KeyTweenValueKind.Vector3 => 3,
            KeyTweenValueKind.Vector4 or KeyTweenValueKind.Color => 4,
            _ => 1,
        };

        /// <summary>Every slot channel this type writes.</summary>
        public int ChannelMask
        {
            get
            {
                var mask = 0;
                for (var i = 0; i < Dimensions; i++) mask |= ChannelBits(i);
                return mask;
            }
        }

        // Built-in only: which enum value it is (custom tracks are KeyTweenProperty.Custom), and its extra options.
        internal virtual KeyTweenProperty Property => KeyTweenProperty.Custom;
        internal virtual bool SizeRelative => false;     // values can be multiples of the target's own size (Position, Size)
        internal virtual bool PointerStart => false;     // can start where the pointer pressed (Position)
        internal virtual string ShaderProperty => null;  // Material tracks: the shader property it animates
    }

    /// <summary>
    /// A custom track on a component of type <typeparamref name="T"/>. Override <see cref="Get"/> and <see cref="Set"/>;
    /// the value is a Vector4 (use x for one number, x/y for two …) as <see cref="Kind"/> says.
    /// <code>
    /// [Preserve] public sealed class SliderHandleTrack : KeyTweenCustomTrack&lt;Slider&gt; { … }
    /// </code>
    /// The class needs a public parameterless constructor. Mark it [UnityEngine.Scripting.Preserve] when the build strips
    /// code (IL2CPP with Managed Stripping Level above Minimal), because tracks are created from the saved class name.
    /// </summary>
    public abstract class KeyTweenCustomTrack<T> : KeyTweenTrackType where T : Component
    {
        public sealed override Type ComponentType => typeof(T);

        /// <summary>The class name, split into words without a trailing "Track" (SliderValueTrack → "Slider Value").</summary>
        public override string Name => KeyTweenTrackTypes.DefaultName(GetType());

        public override KeyTweenValueKind Kind => KeyTweenValueKind.Vector4;
        public override Color TrackColor => new Color32(171, 71, 188, 255);

        protected abstract Vector4 Get(T target);
        protected abstract void Set(T target, Vector4 value);

        public sealed override Vector4 Read(Component target) => Get((T)target);
        public sealed override void Write(Component target, Vector4 value) => Set((T)target, value);
    }

    /// <summary>A custom track of one number. e.g. <c>class SliderValueTrack : KeyTweenFloatTrack&lt;Slider&gt;</c>.</summary>
    public abstract class KeyTweenFloatTrack<T> : KeyTweenTrackType where T : Component
    {
        public sealed override Type ComponentType => typeof(T);
        public override string Name => KeyTweenTrackTypes.DefaultName(GetType());
        public sealed override KeyTweenValueKind Kind => KeyTweenValueKind.Float;
        public override Color TrackColor => new Color32(171, 71, 188, 255);

        protected abstract float Get(T target);
        protected abstract void Set(T target, float value);

        public sealed override Vector4 Read(Component target) => new(Get((T)target), 0f, 0f, 0f);
        public sealed override void Write(Component target, Vector4 value) => Set((T)target, value.x);
    }

    /// <summary>A custom track of a Vector2.</summary>
    public abstract class KeyTweenVector2Track<T> : KeyTweenTrackType where T : Component
    {
        public sealed override Type ComponentType => typeof(T);
        public override string Name => KeyTweenTrackTypes.DefaultName(GetType());
        public sealed override KeyTweenValueKind Kind => KeyTweenValueKind.Vector2;
        public override Color TrackColor => new Color32(171, 71, 188, 255);

        protected abstract Vector2 Get(T target);
        protected abstract void Set(T target, Vector2 value);

        public sealed override Vector4 Read(Component target) => Get((T)target);
        public sealed override void Write(Component target, Vector4 value) => Set((T)target, value);
    }

    /// <summary>A custom track of a Vector3.</summary>
    public abstract class KeyTweenVector3Track<T> : KeyTweenTrackType where T : Component
    {
        public sealed override Type ComponentType => typeof(T);
        public override string Name => KeyTweenTrackTypes.DefaultName(GetType());
        public sealed override KeyTweenValueKind Kind => KeyTweenValueKind.Vector3;
        public override Color TrackColor => new Color32(171, 71, 188, 255);

        protected abstract Vector3 Get(T target);
        protected abstract void Set(T target, Vector3 value);

        public sealed override Vector4 Read(Component target) => Get((T)target);
        public sealed override void Write(Component target, Vector4 value) => Set((T)target, value);
    }

    /// <summary>A custom track of a Color.</summary>
    public abstract class KeyTweenColorTrack<T> : KeyTweenTrackType where T : Component
    {
        public sealed override Type ComponentType => typeof(T);
        public override string Name => KeyTweenTrackTypes.DefaultName(GetType());
        public sealed override KeyTweenValueKind Kind => KeyTweenValueKind.Color;
        public override Color TrackColor => new Color32(171, 71, 188, 255);

        protected abstract Color Get(T target);
        protected abstract void Set(T target, Color value);

        public sealed override Vector4 Read(Component target) => Get((T)target);
        public sealed override void Write(Component target, Vector4 value) => Set((T)target, value);
    }

    /// <summary>Finds the type of a track: built-in by its <see cref="KeyTweenProperty"/>, custom by its saved class name.</summary>
    public static class KeyTweenTrackTypes
    {
        static readonly Dictionary<string, KeyTweenTrackType> Custom = new();
        static readonly Dictionary<(KeyTweenProperty, string), KeyTweenTrackType> Material = new();

        /// <summary>The built-in type of <paramref name="property"/> (not Custom, not a Material track: those need a track).</summary>
        public static KeyTweenTrackType Of(KeyTweenProperty property) => KeyTweenBuiltInTracks.Get(property);

        /// <summary>The type of <paramref name="track"/>; a missing custom class reads 0 and writes nothing.</summary>
        public static KeyTweenTrackType Of(KeyTweenTrack track)
        {
            if (track.property == KeyTweenProperty.Custom) return OfCustom(track.typeName);
            if (KeyTweenBuiltInTracks.IsMaterial(track.property)) return OfMaterial(track.property, track.shaderProperty);
            return KeyTweenBuiltInTracks.Get(track.property);
        }

        /// <summary>A custom track type by its saved name ("Namespace.Type, Assembly"), created once.</summary>
        public static KeyTweenTrackType OfCustom(string typeName)
        {
            typeName ??= "";
            if (Custom.TryGetValue(typeName, out var type)) return type;
            var found = typeName.Length == 0 ? null : Type.GetType(typeName);
            type = found != null && typeof(KeyTweenTrackType).IsAssignableFrom(found) && !found.IsAbstract
                ? (KeyTweenTrackType)Activator.CreateInstance(found)
                : new KeyTweenBuiltInTracks.Missing(typeName);
            Custom[typeName] = type;
            return type;
        }

        internal static KeyTweenTrackType OfMaterial(KeyTweenProperty property, string shaderProperty)
        {
            var key = (property, shaderProperty ?? "");
            if (!Material.TryGetValue(key, out var type)) Material[key] = type = KeyTweenBuiltInTracks.MaterialType(property, key.Item2);
            return type;
        }

        /// <summary>Points <paramref name="track"/> at <paramref name="type"/> (its property and, for custom/material types, the name).</summary>
        public static void Assign(KeyTweenTrack track, KeyTweenTrackType type)
        {
            track.property = type.Property;
            track.typeName = type.Property == KeyTweenProperty.Custom ? TypeKey(type.GetType()) : "";
            track.shaderProperty = type.ShaderProperty ?? "";
        }

        public static string TypeKey(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

        internal static string DefaultName(Type type)
        {
            var name = type.Name;
            if (name.EndsWith("Track") && name.Length > 5) name = name.Substring(0, name.Length - 5);
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }
    }
}
