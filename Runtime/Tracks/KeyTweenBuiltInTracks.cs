using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KeyTweening
{
    /// <summary>
    /// The built-in track types, one per <see cref="KeyTweenProperty"/>: what each reads and writes, its serialized fields
    /// (editor preview, recording) and which channels of a shared value it writes (takeover between states).
    /// </summary>
    internal static class KeyTweenBuiltInTracks
    {
        // Values that more than one track type writes. A track type's channel bits say which part of the slot it writes.
        static readonly object GraphicColorSlot = new(), SpriteColorSlot = new(), CharColorSlot = new(),
            RectSlot = new(), PositionSlot = new(), RotationSlot = new();
        static readonly int[] AlphaChannel = { 1 << 3 };                       // the a of r, g, b, a
        static readonly int[] PositionChannels = { 1 << 0, 1 << 1 };           // RectSlot: position x, y
        static readonly int[] SizeChannels = { 1 << 2, 1 << 3 };               // RectSlot: size x, y
        static readonly int[] OffsetChannels = { 1 << 0 | 1 << 2, 1 << 1 | 1 << 3 };   // an offset moves position and size

        // Track colors in the timeline, by kind of property.
        static readonly Color ColorTransform = new Color32(74, 144, 226, 255), ColorTurn = new Color32(92, 107, 192, 255),
            ColorSize = new Color32(126, 87, 194, 255), ColorPlace = new Color32(38, 166, 154, 255), ColorTint = new Color32(229, 115, 115, 255),
            ColorFade = new Color32(230, 160, 60, 255), ColorText = new Color32(124, 179, 66, 255), ColorFill = new Color32(255, 183, 77, 255),
            ColorScene = new Color32(144, 164, 174, 255), ColorToggle = new Color32(189, 189, 189, 255), ColorReveal = new Color32(171, 207, 92, 255),
            ColorChars = new Color32(77, 182, 172, 255), ColorCharTint = new Color32(240, 98, 146, 255), ColorLight = new Color32(255, 213, 79, 255),
            ColorSound = new Color32(100, 181, 246, 255), ColorUi = new Color32(161, 136, 127, 255);

        sealed class BuiltIn<T> : KeyTweenTrackType where T : Component
        {
            readonly KeyTweenProperty _property;
            readonly string _name;
            readonly KeyTweenValueKind _kind;
            readonly Func<T, Vector4> _read;
            readonly Action<T, Vector4> _write;

            public BuiltIn(KeyTweenProperty property, string name, KeyTweenValueKind kind, Color color, Func<T, Vector4> read, Action<T, Vector4> write)
            {
                (_property, _name, _kind, TrackColorValue, _read, _write) = (property, name, kind, color, read, write);
            }

            public Color TrackColorValue;
            public Vector2 RangeValue;
            public object SlotValue;
            public int[] Channels;                                        // bits per component, when sharing a slot
            public Func<T, bool> OffersWhen;
            public Action<T, List<(Object, string)>> Fields;
            public bool SizeRelativeValue, PointerStartValue;

            public override Type ComponentType => typeof(T);
            public override string Name => _name;
            public override KeyTweenValueKind Kind => _kind;
            public override Vector2 Range => RangeValue;
            public override Color TrackColor => TrackColorValue;
            public override object Slot => SlotValue ?? this;
            public override int ChannelBits(int component) => Channels != null && component < Channels.Length ? Channels[component] : 1 << component;
            public override bool Offers(Component target) => target is T t && (OffersWhen == null || OffersWhen(t));
            public override Vector4 Read(Component target) => _read((T)target);
            public override void Write(Component target, Vector4 value) => _write((T)target, value);
            public override void SerializedFields(Component target, List<(Object, string)> fields) => Fields?.Invoke((T)target, fields);
            internal override KeyTweenProperty Property => _property;
            internal override bool SizeRelative => SizeRelativeValue;
            internal override bool PointerStart => PointerStartValue;
        }

        /// <summary>A custom track whose class is gone, or an optional package's track without the package: reads 0, writes nothing.</summary>
        internal sealed class Missing : KeyTweenTrackType
        {
            readonly string _name;
            readonly KeyTweenProperty _property;
            public Missing(string name, KeyTweenProperty property = KeyTweenProperty.Custom) => (_name, _property) = (name, property);
            public override Type ComponentType => typeof(Component);
            public override string Name => $"{_name} (missing)";
            public override KeyTweenValueKind Kind => KeyTweenValueKind.Vector4;
            public override bool Offers(Component target) => false;
            public override Vector4 Read(Component target) => Vector4.zero;
            public override void Write(Component target, Vector4 value) { }
            internal override KeyTweenProperty Property => _property;
        }

        /// <summary>An optional package's property registered through <see cref="KeyTweenExternalProperties"/> (UIEffect …).</summary>
        internal sealed class External : KeyTweenTrackType
        {
            readonly KeyTweenExternalProperties.Entry _e;
            public External(KeyTweenExternalProperties.Entry entry) => _e = entry;
            public override Type ComponentType => _e.componentType;
            public override string Name => _e.name;
            public override KeyTweenValueKind Kind => _e.isColor ? KeyTweenValueKind.Color : KeyTweenValueKind.Float;
            public override Vector2 Range => new(_e.min, _e.max);
            public override Color TrackColor => _e.trackColor;
            public override bool Offers(Component target) => _e.componentType.IsInstanceOfType(target);
            public override Vector4 Read(Component target) => _e.read(target);
            public override void Write(Component target, Vector4 value) => _e.write(target, value);
            internal override KeyTweenProperty Property => _e.property;

            public override void SerializedFields(Component target, List<(Object, string)> fields)
            {
                if (string.IsNullOrEmpty(_e.serializedField)) return;
                if (_e.isColor) Add(fields, target, _e.serializedField, "r", "g", "b", "a");
                else fields.Add((target, _e.serializedField));
            }
        }

        /// <summary>A shader property of a Renderer's material, written through a MaterialPropertyBlock (the material asset is not changed).</summary>
        sealed class MaterialTrack : KeyTweenTrackType
        {
            readonly KeyTweenProperty _property;
            readonly string _shaderProperty;
            readonly int _id;

            public MaterialTrack(KeyTweenProperty property, string shaderProperty)
            {
                (_property, _shaderProperty) = (property, shaderProperty);
                _id = Shader.PropertyToID(shaderProperty);
            }

            public override Type ComponentType => typeof(Renderer);
            public override string Name => $"Material {_shaderProperty}";
            public override KeyTweenValueKind Kind => _property switch
            {
                KeyTweenProperty.MaterialColor => KeyTweenValueKind.Color,
                KeyTweenProperty.MaterialVector => KeyTweenValueKind.Vector4,
                _ => KeyTweenValueKind.Float,
            };
            public override Color TrackColor => ColorTint;
            public override bool Offers(Component target) => target is Renderer r && r.sharedMaterial != null && r.sharedMaterial.HasProperty(_id);
            internal override KeyTweenProperty Property => _property;
            internal override string ShaderProperty => _shaderProperty;

            public override Vector4 Read(Component target)
            {
                var r = (Renderer)target;
                r.GetPropertyBlock(Block);
                var material = r.sharedMaterial;
                switch (_property)
                {
                    case KeyTweenProperty.MaterialColor:
                        return Block.HasColor(_id) ? Block.GetColor(_id) : material != null && material.HasProperty(_id) ? material.GetColor(_id) : Color.white;
                    case KeyTweenProperty.MaterialVector:
                        return Block.HasVector(_id) ? Block.GetVector(_id) : material != null && material.HasProperty(_id) ? material.GetVector(_id) : Vector4.zero;
                    default:
                        return new Vector4(Block.HasFloat(_id) ? Block.GetFloat(_id) : material != null && material.HasProperty(_id) ? material.GetFloat(_id) : 0f, 0, 0, 0);
                }
            }

            public override void Write(Component target, Vector4 value)
            {
                var r = (Renderer)target;
                r.GetPropertyBlock(Block);
                switch (_property)
                {
                    case KeyTweenProperty.MaterialColor: Block.SetColor(_id, value); break;
                    case KeyTweenProperty.MaterialVector: Block.SetVector(_id, value); break;
                    default: Block.SetFloat(_id, value.x); break;
                }
                r.SetPropertyBlock(Block);
            }
        }

        static readonly MaterialPropertyBlock Block = new();   // reused: reading and writing a block allocates nothing

        internal static bool IsMaterial(KeyTweenProperty p) => p is KeyTweenProperty.MaterialColor or KeyTweenProperty.MaterialFloat or KeyTweenProperty.MaterialVector;

        internal static KeyTweenTrackType MaterialType(KeyTweenProperty property, string shaderProperty) =>
            string.IsNullOrEmpty(shaderProperty) ? new Missing("Material", property) : new MaterialTrack(property, shaderProperty);

        static KeyTweenTrackType[] _types;

        public static KeyTweenTrackType Get(KeyTweenProperty property)
        {
            if (KeyTweenExternalProperties.IsExternal(property))
                return KeyTweenExternalProperties.TryGet(property, out var entry) ? entry.Type : new Missing(property.ToString(), property);
            _types ??= Build();
            var i = (int)property;
            return i >= 0 && i < _types.Length && _types[i] != null ? _types[i] : new Missing(property.ToString(), property);
        }

        /// <summary>Every built-in type that can sit on an object (not Material tracks: those are listed per shader property).</summary>
        public static IEnumerable<KeyTweenTrackType> All
        {
            get
            {
                _types ??= Build();
                foreach (var type in _types)
                    if (type != null) yield return type;
            }
        }

        static KeyTweenTrackType[] Build()
        {
            var list = new List<KeyTweenTrackType>
            {
                // The object itself
                new BuiltIn<Transform>(KeyTweenProperty.Scale, "Scale", KeyTweenValueKind.Vector3, ColorTransform, t => t.localScale, (t, v) => t.localScale = v)
                    { Fields = (t, f) => Add(f, t, "m_LocalScale", "x", "y", "z") },
                new BuiltIn<Transform>(KeyTweenProperty.Rotation, "Rotation", KeyTweenValueKind.Vector3, ColorTurn, t => SignedAngles(t.localEulerAngles), (t, v) => t.localEulerAngles = v)
                    { SlotValue = RotationSlot, Fields = RotationFields },
                new BuiltIn<Transform>(KeyTweenProperty.Active, "Active", KeyTweenValueKind.Toggle, ColorToggle, t => new Vector4(t.gameObject.activeSelf ? 1f : 0f, 0, 0, 0),
                    (t, v) =>
                    {
                        var active = v.x >= 0.5f;
                        if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
                    })
                    { Fields = (t, f) => f.Add((t.gameObject, "m_IsActive")) },
                new BuiltIn<Transform>(KeyTweenProperty.Position, "Position", KeyTweenValueKind.Vector3, ColorPlace, t => t.localPosition, (t, v) => t.localPosition = v)
                    { SlotValue = PositionSlot, OffersWhen = t => t is not RectTransform, Fields = (t, f) => Add(f, t, "m_LocalPosition", "x", "y", "z") },
                new BuiltIn<Transform>(KeyTweenProperty.WorldPosition, "World Position", KeyTweenValueKind.Vector3, ColorPlace, t => t.position, (t, v) => t.position = v)
                    { SlotValue = PositionSlot, OffersWhen = t => t is not RectTransform, Fields = (t, f) => Add(f, t, "m_LocalPosition", "x", "y", "z") },
                new BuiltIn<Transform>(KeyTweenProperty.WorldRotation, "World Rotation", KeyTweenValueKind.Vector3, ColorTurn, t => SignedAngles(t.eulerAngles), (t, v) => t.eulerAngles = v)
                    { SlotValue = RotationSlot, OffersWhen = t => t is not RectTransform, Fields = RotationFields },

                // RectTransform
                new BuiltIn<RectTransform>(KeyTweenProperty.SizeDelta, "Size", KeyTweenValueKind.Vector2, ColorSize, t => t.sizeDelta, (t, v) => t.sizeDelta = v)
                    { SlotValue = RectSlot, Channels = SizeChannels, SizeRelativeValue = true, Fields = (t, f) => Add(f, t, "m_SizeDelta", "x", "y") },
                new BuiltIn<RectTransform>(KeyTweenProperty.AnchoredPosition, "Position", KeyTweenValueKind.Vector2, ColorPlace, t => t.anchoredPosition, (t, v) => t.anchoredPosition = v)
                    { SlotValue = RectSlot, Channels = PositionChannels, SizeRelativeValue = true, PointerStartValue = true, Fields = (t, f) => Add(f, t, "m_AnchoredPosition", "x", "y") },
                new BuiltIn<RectTransform>(KeyTweenProperty.AnchorMin, "Anchor Min", KeyTweenValueKind.Vector2, ColorPlace, t => t.anchorMin, (t, v) => t.anchorMin = v)
                    { Fields = (t, f) => Add(f, t, "m_AnchorMin", "x", "y") },
                new BuiltIn<RectTransform>(KeyTweenProperty.AnchorMax, "Anchor Max", KeyTweenValueKind.Vector2, ColorPlace, t => t.anchorMax, (t, v) => t.anchorMax = v)
                    { Fields = (t, f) => Add(f, t, "m_AnchorMax", "x", "y") },
                new BuiltIn<RectTransform>(KeyTweenProperty.Pivot, "Pivot", KeyTweenValueKind.Vector2, ColorPlace, t => t.pivot, (t, v) => t.pivot = v)
                    { Fields = (t, f) => Add(f, t, "m_Pivot", "x", "y") },
                new BuiltIn<RectTransform>(KeyTweenProperty.OffsetMin, "Offset Min", KeyTweenValueKind.Vector2, ColorSize, t => t.offsetMin, (t, v) => t.offsetMin = v)
                    { SlotValue = RectSlot, Channels = OffsetChannels, Fields = OffsetFields },
                new BuiltIn<RectTransform>(KeyTweenProperty.OffsetMax, "Offset Max", KeyTweenValueKind.Vector2, ColorSize, t => t.offsetMax, (t, v) => t.offsetMax = v)
                    { SlotValue = RectSlot, Channels = OffsetChannels, Fields = OffsetFields },

                // UI
                new BuiltIn<CanvasGroup>(KeyTweenProperty.CanvasGroupAlpha, "Group Alpha", KeyTweenValueKind.Float, ColorFade, g => One(g.alpha), (g, v) => g.alpha = v.x)
                    { RangeValue = new Vector2(0f, 1f), Fields = (g, f) => f.Add((g, "m_Alpha")) },
                new BuiltIn<Graphic>(KeyTweenProperty.GraphicColor, "Color", KeyTweenValueKind.Color, ColorTint, g => g.color, (g, v) => g.color = v)
                    { SlotValue = GraphicColorSlot, Fields = GraphicColorFields },
                new BuiltIn<Graphic>(KeyTweenProperty.GraphicAlpha, "Alpha", KeyTweenValueKind.Float, ColorFade, g => One(g.color.a),
                    (g, v) =>
                    {
                        var c = g.color;
                        c.a = v.x;
                        g.color = c;
                    })
                    { RangeValue = new Vector2(0f, 1f), SlotValue = GraphicColorSlot, Channels = AlphaChannel, Fields = GraphicColorFields },
                new BuiltIn<Image>(KeyTweenProperty.FillAmount, "Fill Amount", KeyTweenValueKind.Float, ColorFill, i => One(i.fillAmount), (i, v) => i.fillAmount = v.x)
                    { RangeValue = new Vector2(0f, 1f), OffersWhen = i => i.type == Image.Type.Filled, Fields = (i, f) => f.Add((i, "m_FillAmount")) },
                new BuiltIn<RawImage>(KeyTweenProperty.RawImageUVRect, "UV Rect", KeyTweenValueKind.Vector4, ColorFill,
                    r => new Vector4(r.uvRect.x, r.uvRect.y, r.uvRect.width, r.uvRect.height), (r, v) => r.uvRect = new Rect(v.x, v.y, v.z, v.w))
                    { Fields = (r, f) => Add(f, r, "m_UVRect", "x", "y", "width", "height") },
                new BuiltIn<Shadow>(KeyTweenProperty.ShadowColor, "Effect Color", KeyTweenValueKind.Color, ColorTint, s => s.effectColor, (s, v) => s.effectColor = v)
                    { Fields = (s, f) => Add(f, s, "m_EffectColor", "r", "g", "b", "a") },
                new BuiltIn<Shadow>(KeyTweenProperty.ShadowDistance, "Effect Distance", KeyTweenValueKind.Vector2, ColorPlace, s => s.effectDistance, (s, v) => s.effectDistance = v)
                    { Fields = (s, f) => Add(f, s, "m_EffectDistance", "x", "y") },
                new BuiltIn<RectMask2D>(KeyTweenProperty.RectMaskSoftness, "Softness", KeyTweenValueKind.Vector2, ColorUi, m => (Vector2)m.softness,
                    (m, v) => m.softness = new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y)))
                    { Fields = (m, f) => Add(f, m, "m_Softness", "x", "y") },
                new BuiltIn<RectMask2D>(KeyTweenProperty.RectMaskPadding, "Padding", KeyTweenValueKind.Vector4, ColorUi, m => m.padding, (m, v) => m.padding = v)
                    { Fields = (m, f) => Add(f, m, "m_Padding", "x", "y", "z", "w") },
                new BuiltIn<Slider>(KeyTweenProperty.SliderValue, "Value", KeyTweenValueKind.Float, ColorFill, s => One(s.value), (s, v) => s.value = v.x)
                    { Fields = (s, f) => f.Add((s, "m_Value")) },
                new BuiltIn<ScrollRect>(KeyTweenProperty.ScrollPosition, "Scroll Position", KeyTweenValueKind.Vector2, ColorPlace, s => s.normalizedPosition, (s, v) => s.normalizedPosition = v)
                    { Fields = (s, f) => { if (s.content != null) Add(f, s.content, "m_AnchoredPosition", "x", "y"); } },
                new BuiltIn<LayoutElement>(KeyTweenProperty.LayoutMinSize, "Min Size", KeyTweenValueKind.Vector2, ColorSize,
                    l => new Vector2(l.minWidth, l.minHeight), (l, v) => { l.minWidth = v.x; l.minHeight = v.y; })
                    { Fields = (l, f) => { f.Add((l, "m_MinWidth")); f.Add((l, "m_MinHeight")); } },
                new BuiltIn<LayoutElement>(KeyTweenProperty.LayoutPreferredSize, "Preferred Size", KeyTweenValueKind.Vector2, ColorSize,
                    l => new Vector2(l.preferredWidth, l.preferredHeight), (l, v) => { l.preferredWidth = v.x; l.preferredHeight = v.y; })
                    { Fields = (l, f) => { f.Add((l, "m_PreferredWidth")); f.Add((l, "m_PreferredHeight")); } },
                new BuiltIn<LayoutElement>(KeyTweenProperty.LayoutFlexibleSize, "Flexible Size", KeyTweenValueKind.Vector2, ColorSize,
                    l => new Vector2(l.flexibleWidth, l.flexibleHeight), (l, v) => { l.flexibleWidth = v.x; l.flexibleHeight = v.y; })
                    { Fields = (l, f) => { f.Add((l, "m_FlexibleWidth")); f.Add((l, "m_FlexibleHeight")); } },

                // TextMeshPro
                new BuiltIn<TMP_Text>(KeyTweenProperty.FontSize, "Font Size", KeyTweenValueKind.Float, ColorText, t => One(t.fontSize), (t, v) => t.fontSize = v.x)
                    { Fields = (t, f) => f.Add((t, "m_fontSize")) },
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharacterSpacing, "Character Spacing", KeyTweenValueKind.Float, ColorText, t => One(t.characterSpacing), (t, v) => t.characterSpacing = v.x)
                    { Fields = (t, f) => f.Add((t, "m_characterSpacing")) },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextWordSpacing, "Word Spacing", KeyTweenValueKind.Float, ColorText, t => One(t.wordSpacing), (t, v) => t.wordSpacing = v.x)
                    { Fields = (t, f) => f.Add((t, "m_wordSpacing")) },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextLineSpacing, "Line Spacing", KeyTweenValueKind.Float, ColorText, t => One(t.lineSpacing), (t, v) => t.lineSpacing = v.x)
                    { Fields = (t, f) => f.Add((t, "m_lineSpacing")) },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextReveal, "Reveal (Typewriter)", KeyTweenValueKind.Float, ColorReveal, t => One(RevealOf(t)),
                    (t, v) => t.maxVisibleCharacters = v.x >= 0.9999f ? 99999 : Mathf.RoundToInt(Mathf.Max(0f, v.x) * t.textInfo.characterCount))
                    { RangeValue = new Vector2(0f, 1f), Fields = (t, f) => f.Add((t, "m_text")) },   // scramble rewrites the text; the visible count is not serialized
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextNumber, "Number (Count)", KeyTweenValueKind.Float, ColorReveal, t => One(NumberOf(t)), (t, v) => WriteNumber(t, v.x))
                    { Fields = (t, f) => f.Add((t, "m_text")) },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextOutlineWidth, "Outline Width", KeyTweenValueKind.Float, ColorText, t => One(t.outlineWidth), (t, v) => t.outlineWidth = v.x)
                    { RangeValue = new Vector2(0f, 1f), OffersWhen = t => HasMaterialProperty(t, ShaderUtilities.ID_OutlineWidth), Fields = TextMaterialFields },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextOutlineColor, "Outline Color", KeyTweenValueKind.Color, ColorTint, t => (Color)t.outlineColor, (t, v) => t.outlineColor = (Color)v)
                    { OffersWhen = t => HasMaterialProperty(t, ShaderUtilities.ID_OutlineColor), Fields = TextMaterialFields },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextFaceDilate, "Face Dilate", KeyTweenValueKind.Float, ColorText,
                    t => One(MaterialFloat(t, ShaderUtilities.ID_FaceDilate)), (t, v) => SetMaterialFloat(t, ShaderUtilities.ID_FaceDilate, v.x))
                    { RangeValue = new Vector2(-1f, 1f), OffersWhen = t => HasMaterialProperty(t, ShaderUtilities.ID_FaceDilate), Fields = TextMaterialFields },
                new BuiltIn<TMP_Text>(KeyTweenProperty.TextUnderlayColor, "Underlay Color", KeyTweenValueKind.Color, ColorTint,
                    t => t.fontSharedMaterial != null ? t.fontSharedMaterial.GetColor(ShaderUtilities.ID_UnderlayColor) : Color.black,
                    (t, v) => { t.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, v); t.SetVerticesDirty(); })
                    { OffersWhen = t => HasMaterialProperty(t, ShaderUtilities.ID_UnderlayColor), Fields = TextMaterialFields },
                // Per character: applied through KeyTweenTextChars, not written as one value
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharScale, "Char Scale", KeyTweenValueKind.Vector3, ColorChars, _ => Vector3.one, (_, _) => { }),
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharOffset, "Char Position", KeyTweenValueKind.Vector3, ColorChars, _ => Vector4.zero, (_, _) => { }),
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharRotation, "Char Rotation", KeyTweenValueKind.Vector3, ColorChars, _ => Vector4.zero, (_, _) => { }),
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharColor, "Char Color", KeyTweenValueKind.Color, ColorCharTint, t => t.color, (_, _) => { })
                    { SlotValue = CharColorSlot },
                new BuiltIn<TMP_Text>(KeyTweenProperty.CharAlpha, "Char Alpha", KeyTweenValueKind.Float, ColorCharTint, _ => One(1f), (_, _) => { })
                    { RangeValue = new Vector2(0f, 1f), SlotValue = CharColorSlot, Channels = AlphaChannel },

                // 2D, 3D and the camera
                new BuiltIn<SpriteRenderer>(KeyTweenProperty.SpriteColor, "Color", KeyTweenValueKind.Color, ColorTint, s => s.color, (s, v) => s.color = v)
                    { SlotValue = SpriteColorSlot, Fields = (s, f) => Add(f, s, "m_Color", "r", "g", "b", "a") },
                new BuiltIn<SpriteRenderer>(KeyTweenProperty.SpriteAlpha, "Alpha", KeyTweenValueKind.Float, ColorFade, s => One(s.color.a),
                    (s, v) =>
                    {
                        var c = s.color;
                        c.a = v.x;
                        s.color = c;
                    })
                    { RangeValue = new Vector2(0f, 1f), SlotValue = SpriteColorSlot, Channels = AlphaChannel, Fields = (s, f) => Add(f, s, "m_Color", "r", "g", "b", "a") },
                new BuiltIn<Light>(KeyTweenProperty.LightColor, "Light Color", KeyTweenValueKind.Color, ColorLight, l => l.color, (l, v) => l.color = v)
                    { Fields = (l, f) => Add(f, l, "m_Color", "r", "g", "b", "a") },
                new BuiltIn<Light>(KeyTweenProperty.LightIntensity, "Intensity", KeyTweenValueKind.Float, ColorLight, l => One(l.intensity), (l, v) => l.intensity = v.x)
                    { Fields = (l, f) => f.Add((l, "m_Intensity")) },
                new BuiltIn<Light>(KeyTweenProperty.LightRange, "Range", KeyTweenValueKind.Float, ColorLight, l => One(l.range), (l, v) => l.range = v.x)
                    { OffersWhen = l => l.type is LightType.Point or LightType.Spot, Fields = (l, f) => f.Add((l, "m_Range")) },
                new BuiltIn<Light>(KeyTweenProperty.LightSpotAngle, "Spot Angle", KeyTweenValueKind.Float, ColorLight, l => One(l.spotAngle), (l, v) => l.spotAngle = v.x)
                    { RangeValue = new Vector2(1f, 179f), OffersWhen = l => l.type == LightType.Spot, Fields = (l, f) => f.Add((l, "m_SpotAngle")) },
                new BuiltIn<Camera>(KeyTweenProperty.CameraBackground, "Camera Background", KeyTweenValueKind.Color, ColorScene, c => c.backgroundColor, (c, v) => c.backgroundColor = v)
                    { Fields = (c, f) => Add(f, c, "m_BackGroundColor", "r", "g", "b", "a") },
                new BuiltIn<Camera>(KeyTweenProperty.CameraOrthoSize, "Camera Size", KeyTweenValueKind.Float, ColorScene, c => One(c.orthographicSize), (c, v) => c.orthographicSize = v.x)
                    { Fields = (c, f) => f.Add((c, "orthographic size")) },
                new BuiltIn<Camera>(KeyTweenProperty.CameraFieldOfView, "Field of View", KeyTweenValueKind.Float, ColorScene, c => One(c.fieldOfView), (c, v) => c.fieldOfView = v.x)
                    { RangeValue = new Vector2(1f, 179f), OffersWhen = c => !c.orthographic, Fields = (c, f) => f.Add((c, "field of view")) },
                new BuiltIn<Camera>(KeyTweenProperty.CameraNearClip, "Near Clip", KeyTweenValueKind.Float, ColorScene, c => One(c.nearClipPlane), (c, v) => c.nearClipPlane = v.x)
                    { Fields = (c, f) => f.Add((c, "near clip plane")) },
                new BuiltIn<Camera>(KeyTweenProperty.CameraFarClip, "Far Clip", KeyTweenValueKind.Float, ColorScene, c => One(c.farClipPlane), (c, v) => c.farClipPlane = v.x)
                    { Fields = (c, f) => f.Add((c, "far clip plane")) },
                new BuiltIn<Camera>(KeyTweenProperty.CameraRect, "Viewport Rect", KeyTweenValueKind.Vector4, ColorScene,
                    c => new Vector4(c.rect.x, c.rect.y, c.rect.width, c.rect.height), (c, v) => c.rect = new Rect(v.x, v.y, v.z, v.w))
                    { Fields = (c, f) => Add(f, c, "m_NormalizedViewPortRect", "x", "y", "width", "height") },
                new BuiltIn<AudioSource>(KeyTweenProperty.AudioVolume, "Volume", KeyTweenValueKind.Float, ColorSound, a => One(a.volume), (a, v) => a.volume = v.x)
                    { RangeValue = new Vector2(0f, 1f), Fields = (a, f) => f.Add((a, "m_Volume")) },
                new BuiltIn<AudioSource>(KeyTweenProperty.AudioPitch, "Pitch", KeyTweenValueKind.Float, ColorSound, a => One(a.pitch), (a, v) => a.pitch = v.x)
                    { RangeValue = new Vector2(-3f, 3f), Fields = (a, f) => f.Add((a, "m_Pitch")) },
            };

            var max = 0;
            foreach (var type in list) max = Math.Max(max, (int)type.Property);
            var table = new KeyTweenTrackType[max + 1];
            foreach (var type in list) table[(int)type.Property] = type;
            return table;
        }

        static Vector4 One(float x) => new(x, 0f, 0f, 0f);

        static void Add(List<(Object, string)> fields, Object owner, string field, params string[] components)
        {
            foreach (var c in components) fields.Add((owner, field + "." + c));
        }

        static void RotationFields(Transform t, List<(Object, string)> f)
        {
            Add(f, t, "m_LocalRotation", "x", "y", "z", "w");
            Add(f, t, "m_LocalEulerAnglesHint", "x", "y", "z");
        }

        static void OffsetFields(RectTransform t, List<(Object, string)> f)
        {
            Add(f, t, "m_AnchoredPosition", "x", "y");
            Add(f, t, "m_SizeDelta", "x", "y");
        }

        static void GraphicColorFields(Graphic g, List<(Object, string)> f)
        {
            if (g is TMP_Text)
            {
                Add(f, g, "m_fontColor", "r", "g", "b", "a");
                f.Add((g, "m_fontColor32.rgba"));
            }
            else Add(f, g, "m_Color", "r", "g", "b", "a");
        }

        // Material tracks of a text make it use an instance of its font material: the preview puts the original back.
        static void TextMaterialFields(TMP_Text t, List<(Object, string)> f)
        {
            f.Add((t, "m_sharedMaterial"));
            f.Add((t, "m_fontMaterial"));
        }

        static bool HasMaterialProperty(TMP_Text t, int id) => t.fontSharedMaterial != null && t.fontSharedMaterial.HasProperty(id);

        static float MaterialFloat(TMP_Text t, int id) => HasMaterialProperty(t, id) ? t.fontSharedMaterial.GetFloat(id) : 0f;

        static void SetMaterialFloat(TMP_Text t, int id, float value)
        {
            t.fontMaterial.SetFloat(id, value);
            t.UpdateMeshPadding();   // dilate changes how far the glyphs reach
            t.SetVerticesDirty();
        }

        // Unity reports 330° for -30°; animating from there to 0 would turn the long way round.
        internal static Vector3 SignedAngles(Vector3 e) => new(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));

        static float RevealOf(TMP_Text text)
        {
            var count = text.textInfo?.characterCount ?? 0;
            return count == 0 || text.maxVisibleCharacters >= count ? 1f : (float)text.maxVisibleCharacters / count;
        }

        // The first whole number, with or without group commas ("1,234/9,999" → 1,234).
        static readonly Regex Number = new(@"-?(\d{1,3}(,\d{3})+|\d+)", RegexOptions.Compiled);

        static float NumberOf(TMP_Text text)
        {
            var m = Number.Match(text.text ?? "");
            return m.Success && long.TryParse(m.Value.Replace(",", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : 0f;
        }

        // Replaces that number, keeping the rest of the label; a label without one becomes the number.
        // ponytail: whole numbers with "N0" commas only; add a format to the track when decimals or local separators are needed.
        static void WriteNumber(TMP_Text text, float value)
        {
            var s = text.text ?? "";
            var number = Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture);
            var m = Number.Match(s);
            var next = m.Success ? s.Substring(0, m.Index) + number + s.Substring(m.Index + m.Length) : number;
            if (next != s) text.text = next;
        }
    }
}
