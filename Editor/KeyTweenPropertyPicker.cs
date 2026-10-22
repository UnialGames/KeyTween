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
    /// on the right (properties with their current value, animation events on the root, sounds on objects with an AudioSource).
    /// Adding: click cards to pick several (across objects), then "Add N"; double-click adds right away.
    /// Retargeting an existing track (<c>current</c> set): one click picks.
    /// </summary>
    internal sealed class KeyTweenPropertyPicker : EditorWindow
    {
        KeyTween _root;
        HashSet<(Component, KeyTweenProperty)> _used;
        (Component, KeyTweenProperty)? _current;
        Action<List<(KeyTweenPickKind kind, Component component, KeyTweenProperty property)>> _onPick;
        readonly List<(KeyTweenPickKind kind, Component component, KeyTweenProperty property)> _picked = new();
        Button _addButton;

        Transform _selected;
        string _search = "";
        VisualElement _tree, _cards;
        Label _path;

        public static void Show(EditorWindow owner, Rect activatorWorldBound, KeyTween root,
            HashSet<(Component, KeyTweenProperty)> used, (Component, KeyTweenProperty)? current,
            Action<List<(KeyTweenPickKind kind, Component component, KeyTweenProperty property)>> onPick)
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

        /// <summary>Properties an object can animate, with the component each one drives.</summary>
        public static IEnumerable<(Component component, KeyTweenProperty property)> Properties(Transform tr)
        {
            yield return (tr, KeyTweenProperty.Scale);
            yield return (tr, KeyTweenProperty.Rotation);
            yield return (tr, KeyTweenProperty.Active);
            if (tr is RectTransform)
            {
                yield return (tr, KeyTweenProperty.SizeDelta);
                yield return (tr, KeyTweenProperty.AnchoredPosition);
                yield return (tr, KeyTweenProperty.AnchorMin);
                yield return (tr, KeyTweenProperty.AnchorMax);
                yield return (tr, KeyTweenProperty.Pivot);
            }
            // TryGetComponent, not GetComponent + `is { }`: in the editor a missing component comes back as a fake-null object
            // that pattern matching lets through, and reading it throws (the cards after it were never drawn).
            if (tr.TryGetComponent<CanvasGroup>(out var group)) yield return (group, KeyTweenProperty.CanvasGroupAlpha);
            if (tr.TryGetComponent<Camera>(out var camera))
            {
                yield return (camera, KeyTweenProperty.CameraBackground);
                yield return (camera, KeyTweenProperty.CameraOrthoSize);
            }
            if (tr.TryGetComponent<Graphic>(out var graphic))
            {
                yield return (graphic, KeyTweenProperty.GraphicColor);
                yield return (graphic, KeyTweenProperty.GraphicAlpha);
            }
            if (graphic is UnityEngine.UI.Image { type: UnityEngine.UI.Image.Type.Filled } image) yield return (image, KeyTweenProperty.FillAmount);
            var text = tr.GetComponent<TMP_Text>();
            if (text != null)
            {
                yield return (text, KeyTweenProperty.FontSize);
                yield return (text, KeyTweenProperty.CharacterSpacing);
                yield return (text, KeyTweenProperty.TextReveal);
                yield return (text, KeyTweenProperty.TextNumber);
                yield return (text, KeyTweenProperty.CharScale);
                yield return (text, KeyTweenProperty.CharOffset);
                yield return (text, KeyTweenProperty.CharRotation);
                yield return (text, KeyTweenProperty.CharColor);
                yield return (text, KeyTweenProperty.CharAlpha);
            }
            // Optional packages' components (UIEffect …), when their integration is installed.
            foreach (var external in KeyTweenExternalProperties.All)
                if (tr.TryGetComponent(external.componentType, out var component)) yield return (component, external.property);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("uia-root");
            root.AddToClassList("uia-picker");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("uia-light");
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(dir ?? "", "KeyTweenEditor.uss").Replace('\\', '/'));
            if (sheet != null) root.styleSheets.Add(sheet);
            if (_root == null) { Close(); return; }   // lost after a domain reload

            var header = new VisualElement();
            header.AddToClassList("uia-picker-header");
            header.Add(new Label(_current.HasValue ? "Change Track Target" : "Add Tracks (multi-select)") { name = "title" });
            var search = new TextField { value = _search };
            search.AddToClassList("uia-picker-search");
            search.RegisterValueChangedCallback(e => { _search = e.newValue; Refresh(); });
            header.Add(search);
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("uia-picker-body");
            _tree = new ScrollView();
            _tree.AddToClassList("uia-picker-tree");
            body.Add(_tree);

            var right = new VisualElement { style = { flexGrow = 1 } };
            _path = new Label();
            _path.AddToClassList("uia-picker-path");
            right.Add(_path);
            _cards = new ScrollView();
            _cards.AddToClassList("uia-picker-cards");
            right.Add(_cards);
            body.Add(right);
            root.Add(body);

            if (!_current.HasValue)
            {
                var footer = new VisualElement();
                footer.AddToClassList("uia-picker-footer");
                footer.Add(new Label("Click: select · Double-click: add now") { style = { flexGrow = 1 } });
                footer.Add(new Button(Close) { text = "Cancel" });
                _addButton = new Button(Commit);
                _addButton.AddToClassList("uia-picker-add");
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
                row.AddToClassList("uia-picker-row");
                row.EnableInClassList("uia-picker-row--selected", tr == _selected);
                if (!tr.gameObject.activeInHierarchy) row.AddToClassList("uia-picker-row--inactive");

                var icon = new Image { image = IconFor(tr), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("uia-picker-icon");
                row.Add(icon);
                row.Add(new Label(tr.name) { style = { flexGrow = 1 } });
                var count = Properties(tr).Count(p => !_used.Contains(p)) + Specials(tr).Count();
                var badge = new Label(count.ToString());
                badge.AddToClassList("uia-picker-badge");
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
                title.AddToClassList("uia-picker-group");
                var icon = new Image { image = EditorGUIUtility.ObjectContent(component, component.GetType()).image, scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("uia-picker-icon");
                title.Add(icon);
                title.Add(new Label(component.GetType().Name));
                _cards.Add(title);
            }

            foreach (var (component, property) in Properties(_selected))
            {
                if (!MatchesProperty(property)) continue;
                Group(component);
                var isCurrent = _current.HasValue && _current.Value.Item1 == component && _current.Value.Item2 == property;
                var taken = _used.Contains((component, property)) && !isCurrent;

                var card = new VisualElement();
                card.AddToClassList("uia-picker-card");
                card.EnableInClassList("uia-picker-card--taken", taken);
                card.EnableInClassList("uia-picker-card--current", isCurrent);

                var stripe = new VisualElement { style = { backgroundColor = KeyTweenTimelineView.PropertyColor(property) } };
                stripe.AddToClassList("uia-picker-card-stripe");
                card.Add(stripe);

                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(KeyTweenTimelineView.PropertyName(property)) { name = "prop" });
                card.Add(text);
                card.Add(ValuePreview(component, property));

                if (taken)
                {
                    var tag = new Label("Added");
                    tag.AddToClassList("uia-picker-tag");
                    card.Add(tag);
                }
                else Pickable(card, (KeyTweenPickKind.Property, component, property));
                _cards.Add(card);
            }

            if (_current.HasValue) return;
            foreach (var (kind, component) in Specials(_selected))
            {
                Group(component);
                var card = new VisualElement();
                card.AddToClassList("uia-picker-card");
                var stripe = new VisualElement { style = { backgroundColor = kind == KeyTweenPickKind.Event ? KeyTweenTimelineView.EventColor : KeyTweenTimelineView.SoundColor } };
                stripe.AddToClassList("uia-picker-card-stripe");
                card.Add(stripe);
                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(kind == KeyTweenPickKind.Event ? "Animation Events" : "Sound") { name = "prop" });
                text.Add(new Label(kind == KeyTweenPickKind.Event ? "Named points in time · [KeyTweenEvent] data"
                    : component is AudioSource ? "Plays through this AudioSource" : "2D, no AudioSource needed") { name = "type" });
                card.Add(text);
                Pickable(card, (kind, component, default));
                _cards.Add(card);
            }
        }

        // Event tracks on the root; sound tracks on the root (KeyTween's source) and on every object with its own AudioSource.
        IEnumerable<(KeyTweenPickKind kind, Component component)> Specials(Transform tr)
        {
            if (tr == _root.transform)
            {
                yield return (KeyTweenPickKind.Event, _root);
                yield return (KeyTweenPickKind.Sound, _root);
            }
            var source = tr.GetComponent<AudioSource>();
            if (source != null && source != _root.audioSource) yield return (KeyTweenPickKind.Sound, source);
        }

        void Pickable(VisualElement card, (KeyTweenPickKind, Component, KeyTweenProperty) pick)
        {
            card.EnableInClassList("uia-picker-card--picked", _picked.Contains(pick));
            card.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_current.HasValue || e.clickCount == 2)
                {
                    if (!_picked.Contains(pick)) _picked.Add(pick);
                    Commit();
                    return;
                }
                if (!_picked.Remove(pick)) _picked.Add(pick);
                card.EnableInClassList("uia-picker-card--picked", _picked.Contains(pick));
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
            var picks = new List<(KeyTweenPickKind, Component, KeyTweenProperty)>(_picked);
            Close();
            _onPick?.Invoke(picks);
        }

        static VisualElement ValuePreview(Component component, KeyTweenProperty property)
        {
            var v = KeyTween.Read(component, property);
            if (property == KeyTweenProperty.GraphicColor)
            {
                var swatch = new VisualElement { style = { backgroundColor = (Color)v } };
                swatch.AddToClassList("uia-picker-swatch");
                return swatch;
            }
            var text = property switch
            {
                KeyTweenProperty.Scale => Mathf.Approximately(v.x, v.y) && Mathf.Approximately(v.y, v.z) ? $"{v.x:0.##}" : $"{v.x:0.##}, {v.y:0.##}, {v.z:0.##}",
                KeyTweenProperty.Rotation => $"{v.x:0.#}, {v.y:0.#}, {v.z:0.#}",
                KeyTweenProperty.SizeDelta => $"{v.x:0.#} × {v.y:0.#}",
                KeyTweenProperty.AnchoredPosition => $"{v.x:0.#}, {v.y:0.#}",
                _ => $"{v.x:0.##}",
            };
            var label = new Label(text);
            label.AddToClassList("uia-picker-value");
            return label;
        }

        bool Matches(Transform tr)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return tr.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                   || Properties(tr).Any(p => MatchesProperty(p.property));
        }

        bool MatchesProperty(KeyTweenProperty property) =>
            string.IsNullOrWhiteSpace(_search)
            || KeyTweenTimelineView.PropertyName(property).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
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
