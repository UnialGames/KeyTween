using UnityEngine;
using UnityEngine.Rendering;

namespace KeyTweening
{
    /// <summary>A track named from code: its target and its kind (a built-in property, a material property, or a custom track class).</summary>
    internal readonly struct KeyTweenTrackRef
    {
        readonly Component _target;
        readonly KeyTweenProperty _property;
        readonly string _name;   // the shader property of a Material track, the type key of a custom track

        KeyTweenTrackRef(Component target, KeyTweenProperty property, string name)
        {
            _target = target;
            _property = property;
            _name = name ?? "";
        }

        public static KeyTweenTrackRef Of(Component target, KeyTweenProperty property) => new(target, property, null);
        public static KeyTweenTrackRef Material(Component target, string shaderProperty) => new(target, KeyTweenProperty.MaterialColor, shaderProperty);
        public static KeyTweenTrackRef Custom<TTrack>(Component target) where TTrack : KeyTweenTrackType =>
            new(target, KeyTweenProperty.Custom, KeyTweenTrackTypes.TypeKey(typeof(TTrack)));

        public bool Matches(KeyTweenTrack t)
        {
            if (t.target != _target) return false;
            if (_property == KeyTweenProperty.Custom) return t.property == KeyTweenProperty.Custom && t.typeName == _name;
            if (_name.Length > 0) return KeyTweenBuiltInTracks.IsMaterial(t.property) && t.shaderProperty == _name;
            return t.property == _property;
        }

        /// <summary>The first track of <paramref name="state"/> this names, or null.</summary>
        public KeyTweenTrack Find(KeyTweenState state)
        {
            foreach (var t in state.tracks)
                if (Matches(t)) return t;
            return null;
        }

        public override string ToString()
        {
            var what = _property == KeyTweenProperty.Custom ? _name : _name.Length > 0 ? $"Material {_name}" : _property.ToString();
            return $"{what} track on '{(_target != null ? _target.name : "null")}'";
        }

        /// <summary>The Material track type for a shader property of <paramref name="target"/>'s material (color, number or vector).</summary>
        public static KeyTweenTrackType MaterialType(Renderer target, string shaderProperty)
        {
            var shader = target != null && target.sharedMaterial != null ? target.sharedMaterial.shader : null;
            var i = shader != null ? shader.FindPropertyIndex(shaderProperty) : -1;
            if (i < 0) throw new System.ArgumentException($"[KeyTween] '{(target != null ? target.name : "null")}' has no material property '{shaderProperty}'.");
            var property = shader.GetPropertyType(i) switch
            {
                ShaderPropertyType.Color => KeyTweenProperty.MaterialColor,
                ShaderPropertyType.Vector => KeyTweenProperty.MaterialVector,
                ShaderPropertyType.Float or ShaderPropertyType.Range => KeyTweenProperty.MaterialFloat,
                _ => throw new System.ArgumentException($"[KeyTween] '{shaderProperty}' is a texture; Material tracks animate colors, numbers and vectors."),
            };
            return KeyTweenTrackTypes.OfMaterial(property, shaderProperty);
        }
    }
}
