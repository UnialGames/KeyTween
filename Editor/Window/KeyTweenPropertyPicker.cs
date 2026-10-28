using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Graphic = UnityEngine.UI.Graphic;

namespace KeyTweening.Editor
{
    internal enum KeyTweenPickKind { Property, Event, Sound }

    /// <summary>
    /// Dropdown for choosing tracks: the animated object's hierarchy on the left, what the selected object can get as cards
    /// on the right (properties with their current value, animation events and sounds on the root).
    /// Adding: click cards to pick several (across objects), then "Add N"; double-click adds right away.
    /// Retargeting an existing track (<c>current</c> set): one click picks.
    /// </summary>
    internal sealed class KeyTweenPropertyPicker : EditorWindow
    {
        KeyTween _root;
        HashSet<(Component, KeyTweenTrackType)> _used;
        (Component, KeyTweenTrackType)? _current;
        Action<List<(KeyTweenPickKind kind, Component component, KeyTweenTrackType type)>> _onPick;
        readonly List<(KeyTweenPickKind kind, Component component, KeyTweenTrackType type)> _picked = new();
        Button _addButton;

        Transform _selected;
        string _search = "";
        VisualElement _tree, _cards;
        Label _path;

        public static void Show(EditorWindow owner, Rect activatorWorldBound, KeyTween root,
            HashSet<(Component, KeyTweenTrackType)> used, (Component, KeyTweenTrackType)? current,
            Action<List<(KeyTweenPickKind kind, Component component, KeyTweenTrackType type)>> onPick)
        {
            var picker = CreateInstance<KeyTweenPropertyPicker>();
            picker._root = root;
            picker._used = used;
            picker._current = current;
            picker._onPick = onPick;
            picker._selected = current?.Item1 != null ? current.Value.Item1.transform : root.transform;

            var screen = new Rect(owner.position.position + activatorWorldBound.position, activatorWorldBound.size);
            picker.ShowAsDropDown(screen, new Vector2(560, 420));
        }

        /// <summary>
        /// The tracks an object can get, with the component each one drives, in the order of its components: the built-in
        /// ones, optional packages' (UIEffect …), the project's custom tracks, and a Renderer's material properties.
        /// </summary>
        public static IEnumerable<(Component component, KeyTweenTrackType type)> Properties(Transform tr)
        {
            foreach (var component in tr.GetComponents<Component>())
            {
                // A missing script, or (in the editor) a fake-null component that pattern matching lets through.
                if (component == null) continue;
                foreach (var type in Types)
                    if (type.ComponentType.IsInstanceOfType(component) && Offered(type, component)) yield return (component, type);
                if (component is Renderer renderer)
                    foreach (var type in MaterialTypes(renderer)) yield return (renderer, type);
            }
        }

        // Reading a type's condition must not stop the list (e.g. a custom track that throws on an unexpected object).
        static bool Offered(KeyTweenTrackType type, Component component)
        {
            try { return type.Offers(component); }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        static List<KeyTweenTrackType> _types;

        /// <summary>Every track type but material ones: built in, from optional packages, and the project's custom classes.</summary>
        static IEnumerable<KeyTweenTrackType> Types
        {
            get
            {
                foreach (var type in KeyTweenBuiltInTracks.All) yield return type;
                foreach (var external in KeyTweenExternalProperties.All) yield return KeyTweenTrackTypes.Of(external.property);
                if (_types == null)
                {
                    _types = new List<KeyTweenTrackType>();
                    var own = typeof(KeyTweenTrackType).Assembly;
                    foreach (var type in TypeCache.GetTypesDerivedFrom<KeyTweenTrackType>())
                        if (type.Assembly != own && !type.IsAbstract && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null)
                            _types.Add(KeyTweenTrackTypes.OfCustom(KeyTweenTrackTypes.TypeKey(type)));
                }
                foreach (var type in _types) yield return type;
            }
        }

        // One track per color, number and vector property of the renderer's materials (textures and hidden ones are left out).
        static IEnumerable<KeyTweenTrackType> MaterialTypes(Renderer renderer)
        {
            var seen = new HashSet<string>();
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null) continue;
                var shader = material.shader;
                for (var i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if ((shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0) continue;
                    var property = shader.GetPropertyType(i) switch
                    {
                        UnityEngine.Rendering.ShaderPropertyType.Color => KeyTweenProperty.MaterialColor,
                        UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range => KeyTweenProperty.MaterialFloat,
                        UnityEngine.Rendering.ShaderPropertyType.Vector => KeyTweenProperty.MaterialVector,
                        _ => (KeyTweenProperty?)null,
                    };
                    var name = shader.GetPropertyName(i);
                    if (property != null && seen.Add(name)) yield return KeyTweenTrackTypes.OfMaterial(property.Value, name);
                }
            }
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("kt-root");
            root.AddToClassList("kt-picker");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("kt-light");
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(dir ?? "", "KeyTweenEditor.uss").Replace('\\', '/'));
            if (sheet != null) root.styleSheets.Add(sheet);
            if (_root == null) { Close(); return; }   // lost after a domain reload

            var header = new VisualElement();
            header.AddToClassList("kt-picker-header");
            header.Add(new Label(_current.HasValue ? "Change Track Target" : "Add Tracks (multi-select)") { name = "title" });
            var search = new TextField { value = _search };
            search.AddToClassList("kt-picker-search");
            search.RegisterValueChangedCallback(e => { _search = e.newValue; Refresh(); });
            header.Add(search);
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("kt-picker-body");
            _tree = new ScrollView();
            _tree.AddToClassList("kt-picker-tree");
            body.Add(_tree);

            var right = new VisualElement { style = { flexGrow = 1 } };
            _path = new Label();
            _path.AddToClassList("kt-picker-path");
            right.Add(_path);
            _cards = new ScrollView();
            _cards.AddToClassList("kt-picker-cards");
            right.Add(_cards);
            body.Add(right);
            root.Add(body);

            if (!_current.HasValue)
            {
                var footer = new VisualElement();
                footer.AddToClassList("kt-picker-footer");
                footer.Add(new Label("Click: select · Double-click: add now") { style = { flexGrow = 1 } });
                footer.Add(new Button(Close) { text = "Cancel" });
                _addButton = new Button(Commit);
                _addButton.AddToClassList("kt-picker-add");
                footer.Add(_addButton);
                root.Add(footer);
                UpdateAddButton();
            }

            Refresh();
            search.schedule.Execute(() => search.Focus());
        }

        void Refresh()
        {
            _tree.Clear();
            var all = _root.GetComponentsInChildren<Transform>(true);
            var matches = all.Where(Matches).ToList();
            if (!matches.Contains(_selected) && matches.Count > 0) _selected = matches[0];

            foreach (var tr in all)
            {
                if (!Matches(tr)) continue;
                var depth = Depth(tr);
                var row = new VisualElement { style = { paddingLeft = 8 + depth * 14 } };
                row.AddToClassList("kt-picker-row");
                row.EnableInClassList("kt-picker-row--selected", tr == _selected);
                if (!tr.gameObject.activeInHierarchy) row.AddToClassList("kt-picker-row--inactive");

                var icon = new Image { image = IconFor(tr), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("kt-picker-icon");
                row.Add(icon);
                row.Add(new Label(tr.name) { style = { flexGrow = 1 } });
                var count = Properties(tr).Count(p => !_used.Contains(p)) + Specials(tr).Count();
                var badge = new Label(count.ToString());
                badge.AddToClassList("kt-picker-badge");
                row.Add(badge);

                var target = tr;
                row.RegisterCallback<PointerDownEvent>(_ => { _selected = target; Refresh(); });
                _tree.Add(row);
            }
            RefreshCards();
        }

        void RefreshCards()
        {
            _cards.Clear();
            if (_selected == null) { _path.text = ""; return; }
            _path.text = _selected == _root.transform ? _root.name : $"{_root.name}/{AnimationUtility.CalculateTransformPath(_selected, _root.transform)}";

            // Cards under a title per component (like the Inspector), so Image's Color isn't mistaken for a RectTransform card.
            Component group = null;
            void Group(Component component)
            {
                if (component == group) return;
                group = component;
                var title = new VisualElement();
                title.AddToClassList("kt-picker-group");
                var icon = new Image { image = EditorGUIUtility.ObjectContent(component, component.GetType()).image, scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("kt-picker-icon");
                title.Add(icon);
                title.Add(new Label(component.GetType().Name));
                _cards.Add(title);
            }

            foreach (var (component, type) in Properties(_selected))
            {
                if (!MatchesProperty(type)) continue;
                Group(component);
                var isCurrent = _current.HasValue && _current.Value.Item1 == component && _current.Value.Item2 == type;
                var taken = _used.Contains((component, type)) && !isCurrent;

                var card = new VisualElement();
                card.AddToClassList("kt-picker-card");
                card.EnableInClassList("kt-picker-card--taken", taken);
                card.EnableInClassList("kt-picker-card--current", isCurrent);

                var stripe = new VisualElement { style = { backgroundColor = type.TrackColor } };
                stripe.AddToClassList("kt-picker-card-stripe");
                card.Add(stripe);

                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(type.Name) { name = "prop" });
                card.Add(text);
                card.Add(ValuePreview(component, type));

                if (taken)
                {
                    var tag = new Label("Added");
                    tag.AddToClassList("kt-picker-tag");
                    card.Add(tag);
                }
                else Pickable(card, (KeyTweenPickKind.Property, component, type));
                _cards.Add(card);
            }

            if (_current.HasValue) return;
            foreach (var (kind, component) in Specials(_selected))
            {
                Group(component);
                var card = new VisualElement();
                card.AddToClassList("kt-picker-card");
                var stripe = new VisualElement { style = { backgroundColor = kind == KeyTweenPickKind.Event ? KeyTweenTimelineView.EventColor : KeyTweenTimelineView.SoundColor } };
                stripe.AddToClassList("kt-picker-card-stripe");
                card.Add(stripe);
                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(kind == KeyTweenPickKind.Event ? "Animation Events" : "Sound") { name = "prop" });
                text.Add(new Label(kind == KeyTweenPickKind.Event ? "Named points in time · [KeyTweenEvent] data"
                    : "2D, no AudioSource needed") { name = "type" });
                card.Add(text);
                Pickable(card, (kind, component, null));
                _cards.Add(card);
            }
        }

        // Event and sound tracks, on the root only (a sound track can still pick its AudioSource in its inspector).
        IEnumerable<(KeyTweenPickKind kind, Component component)> Specials(Transform tr)
        {
            if (tr != _root.transform) yield break;
            yield return (KeyTweenPickKind.Event, _root);
            yield return (KeyTweenPickKind.Sound, _root);
        }

        void Pickable(VisualElement card, (KeyTweenPickKind, Component, KeyTweenTrackType) pick)
        {
            card.EnableInClassList("kt-picker-card--picked", _picked.Contains(pick));
            card.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_current.HasValue || e.clickCount == 2)
                {
                    if (!_picked.Contains(pick)) _picked.Add(pick);
                    Commit();
                    return;
                }
                if (!_picked.Remove(pick)) _picked.Add(pick);
                card.EnableInClassList("kt-picker-card--picked", _picked.Contains(pick));
                UpdateAddButton();
            });
        }

        void UpdateAddButton()
        {
            if (_addButton == null) return;
            _addButton.text = _picked.Count > 0 ? $"Add {_picked.Count}" : "Add";
            _addButton.SetEnabled(_picked.Count > 0);
        }

        void Commit()
        {
            if (_picked.Count == 0) return;
            var picks = new List<(KeyTweenPickKind, Component, KeyTweenTrackType)>(_picked);
            Close();
            _onPick?.Invoke(picks);
        }

        static VisualElement ValuePreview(Component component, KeyTweenTrackType type)
        {
            Vector4 v;
            try { v = type.Read(component); }
            catch (Exception) { v = Vector4.zero; }   // a custom track that cannot read this object yet
            if (type.Kind == KeyTweenValueKind.Color)
            {
                var swatch = new VisualElement { style = { backgroundColor = (Color)v } };
                swatch.AddToClassList("kt-picker-swatch");
                return swatch;
            }
            var text = type.Kind switch
            {
                KeyTweenValueKind.Vector3 when type.Property == KeyTweenProperty.Scale && Mathf.Approximately(v.x, v.y) && Mathf.Approximately(v.y, v.z) => $"{v.x:0.##}",
                KeyTweenValueKind.Vector3 => $"{v.x:0.##}, {v.y:0.##}, {v.z:0.##}",
                KeyTweenValueKind.Vector2 when type.Property == KeyTweenProperty.SizeDelta => $"{v.x:0.#} × {v.y:0.#}",
                KeyTweenValueKind.Vector2 => $"{v.x:0.##}, {v.y:0.##}",
                KeyTweenValueKind.Vector4 => $"{v.x:0.##}, {v.y:0.##}, {v.z:0.##}, {v.w:0.##}",
                KeyTweenValueKind.Toggle => v.x >= 0.5f ? "On" : "Off",
                _ => $"{v.x:0.##}",
            };
            var label = new Label(text);
            label.AddToClassList("kt-picker-value");
            return label;
        }

        bool Matches(Transform tr)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return tr.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                   || Properties(tr).Any(p => MatchesProperty(p.type));
        }

        bool MatchesProperty(KeyTweenTrackType type) =>
            string.IsNullOrWhiteSpace(_search)
            || type.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
            || (_selected != null && _selected.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);

        int Depth(Transform tr)
        {
            var d = 0;
            for (var t = tr; t != _root.transform && t != null; t = t.parent) d++;
            return d;
        }

        static Texture IconFor(Transform tr)
        {
            var graphic = tr.GetComponent<Graphic>();
            Component main = graphic != null ? graphic : tr;
            return EditorGUIUtility.ObjectContent(main, main.GetType()).image;
        }
    }
}
