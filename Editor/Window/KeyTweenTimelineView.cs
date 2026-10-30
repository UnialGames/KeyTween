using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Dopesheet-style timeline: ruler, then every track grouped under its object's hierarchy (foldable): property tracks,
    /// animation event tracks, sound tracks, and the states called on child KeyTweens (editable in place once overridden).
    /// Selection works like Unity's Animation window: click, Shift/Ctrl+click, box select, drag selected keys, Delete.
    /// The window owns the data, undo and the selection set; this view only reports intent through callbacks.
    /// </summary>
    internal sealed class KeyTweenTimelineView : VisualElement
    {
        public const float HeaderWidth = 260f;
        const float Snap = 0.01f;
        const float LaneRight = 16f;
        const float DragThreshold = 3f;

        // Callbacks to the window. A "track" is a KeyTweenTrack, KeyTweenEventTrack or KeyTweenSoundTrack object.
        public Action<float> Scrubbed;
        public Action<object> TrackSelected;                              // track header clicked
        public Action<KeyTweenKey, bool> KeyClicked;                        // key, additive (Shift/Ctrl)
        public Action<List<KeyTweenKey>, bool> BoxSelected;                 // keys inside the box, additive
        public Action SelectionCleared;
        public Action<float> SelectedKeysMoved;                           // delta seconds from drag start (live)
        public Action<float, float> SelectedKeysScaled;                   // pivot (timeline seconds), factor from drag start (live)
        public Action SelectedKeysDragStarted;
        public Action DragFinished;
        public Action DeleteRequested;
        public Action CopyRequested;                                      // Ctrl+C (Edit › Copy) on the timeline: the selected keys
        public Action<float?> PasteRequested;                             // Ctrl+V (Edit › Paste): at the playhead (null) or a timeline time
        public Func<bool> CanPaste;
        public Action<KeyTweenTrack, float> AddKeyRequested;
        public Action<KeyTweenTrack, float> StaggerDragged;                 // character track, new stagger (live while dragging its end)
        public Action StaggerDragFinished;                // track, time on that track (call offset removed)
        public Action<object> RemoveRequested;                            // a track, child call, event or sound
        public Action<Rect> AddTrackMenuRequested;                        // where to drop the menu
        public Action<KeyTweenMenu, float> PopulateChildCallMenu;           // appends "Add Child Call/..." items for that time
        // Markers: KeyTweenEventMarker, KeyTweenSound or KeyTweenChildCall.
        public Action<object> MarkerClicked;                              // null = clicked an empty marker lane
        public Action<object, float> MarkerMoved;                         // item, new time (live while dragging)
        public Action MarkerDragFinished;
        public Action<KeyTweenEventTrack, Rect, float> AddEventRequested;   // track, where to drop the menu, time
        public Action<KeyTweenSoundTrack, float, AudioClip> AddSoundRequested;   // track, time, clip (null = pick later)
        public Action<Rect, float> AddCallRequested;                      // where to drop the menu, time
        public Action<KeyTweenChildCall, KeyTweenTrack> OverrideRequested;    // copy that one track of the child's state into this prefab
        public Action<KeyTweenChildCall> RevertOverrideRequested;           // every overridden track of the call back to the original

        public object SelectedMarker { get; set; }

        readonly VisualElement _ruler;
        readonly VisualElement _rows, _keyArea;
        readonly VisualElement _playhead;
        readonly VisualElement _endShade;   // past the state's end, like the Animation window's area after the clip
        readonly Label _endMarker;
        readonly List<(KeyTweenTrack track, VisualElement el, float offset)> _tailHandles = new();
        double _lastDownTime;   // double-click fallback: clickCount isn't reliable after the first click scrubs the preview
        Vector2 _lastDownPos;
        readonly VisualElement _box;
        readonly VisualElement _span;   // around the selected keys: its end handles stretch them, like the Animation window
        float _scalePivot, _scaleFrom, _scaleGrab;
        readonly List<(KeyTweenKey key, VisualElement el, float offset)> _keyElements = new();
        readonly List<(KeyTweenKey key, VisualElement el, float offset)> _readOnlyKeys = new();   // positioned like keys, never selected
        readonly List<(VisualElement header, object track)> _headers = new();
        readonly Dictionary<KeyTweenTrack, (VisualElement lane, float offset)> _lanes = new();
        readonly List<VisualElement> _repaintLanes = new();
        readonly HashSet<Transform> _collapsed = new();   // folded object rows, kept across rebuilds
        readonly List<(object item, VisualElement el)> _markerElements = new();
        object _dragMarker;
        bool _markerDragged;
        float _dragOffset;
        int _rowCount;
        KeyTweenState _state;
        KeyTween _root;
        ICollection<KeyTweenKey> _selection = Array.Empty<KeyTweenKey>();
        object _selTrack;
        float _time;
        float? _frozenRange;
        // Zoom/pan: null = fit the whole state. Otherwise the visible window starts at _viewStart and spans _viewLength seconds.
        float? _viewLength;
        float _viewStart;
        bool _panning;   // middle-button drag
        float _panStartView;
        Vector2 _panStartPos;
        KeyTweenMenu _menu;   // right-click: filled by the clicked element and its ancestors, shown once it reaches this view

        // Pointer gesture state
        enum Gesture { None, Pending, DragKeys, Box }
        Gesture _gesture;
        Vector2 _downWorld;
        KeyTweenTrack _downTrack;
        KeyTweenKey _downKey;
        bool _downAdditive;

        public float Time => _time;

        public static Color PropertyColor(KeyTweenTrack track) => track.Type.TrackColor;

        public static readonly Color EventColor = new Color32(255, 183, 77, 255);
        public static readonly Color SoundColor = new Color32(129, 199, 132, 255);

        public static string PropertyName(KeyTweenTrack track) => track.Type.Name;

        public KeyTweenTimelineView()
        {
            AddToClassList("kt-timeline");
            focusable = true;   // receives Delete, Copy and Paste
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Delete or KeyCode.Backspace) { DeleteRequested?.Invoke(); e.StopPropagation(); }
            });
            // Ctrl+C / Ctrl+V reach a focused element as Edit › Copy / Paste commands (text fields in the inspector handle their own).
            RegisterCallback<ValidateCommandEvent>(e =>
            {
                if (e.commandName == "Copy" && _selection.Count > 0 || e.commandName == "Paste" && CanPaste?.Invoke() == true) e.StopPropagation();
            });
            RegisterCallback<ExecuteCommandEvent>(e =>
            {
                if (e.commandName == "Copy") { CopyRequested?.Invoke(); e.StopPropagation(); }
                else if (e.commandName == "Paste") { PasteRequested?.Invoke(null); e.StopPropagation(); }
            });

            var rulerRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var corner = new VisualElement { style = { width = HeaderWidth } };
            corner.AddToClassList("kt-header");
            var hint = new Label("Right-click: menu · Ctrl+Wheel: zoom") { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, paddingLeft = 8 } };
            hint.AddToClassList("kt-zoom-hint");
            corner.Add(hint);
            corner.tooltip = "Right-click: add tracks / child calls, remove items\n" +
                             "Wheel over the ruler, Ctrl+Wheel: zoom\n" +
                             "Shift+Wheel, horizontal wheel, middle-drag: pan\n" +
                             "F or ⤢: fit all";
            corner.Add(SmallButton("⤢", "Fit All (F)", FitView));
            rulerRow.Add(corner);
            _ruler = new VisualElement { style = { flexGrow = 1, marginRight = LaneRight } };
            _ruler.AddToClassList("kt-ruler");
            _ruler.RegisterCallback<PointerDownEvent>(e => { _ruler.CapturePointer(e.pointerId); Scrubbed?.Invoke(XToTime(e.localPosition.x)); });
            _ruler.RegisterCallback<PointerMoveEvent>(e => { if (_ruler.HasPointerCapture(e.pointerId)) Scrubbed?.Invoke(XToTime(e.localPosition.x)); });
            _ruler.RegisterCallback<PointerUpEvent>(e => _ruler.ReleasePointer(e.pointerId));
            rulerRow.Add(_ruler);
            Add(rulerRow);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            _rows = new VisualElement();
            scroll.Add(_rows);
            // On the whole key area, like the Animation window: a box can start on the event / call lanes or below the last track.
            _keyArea = scroll;
            _keyArea.RegisterCallback<PointerDownEvent>(OnRowsDown);
            _keyArea.RegisterCallback<PointerMoveEvent>(OnRowsMove);
            _keyArea.RegisterCallback<PointerUpEvent>(OnRowsUp);
            // Room below the last row: it never hides behind the window edge, and right-click has empty space to land on.
            scroll.Add(new VisualElement { style = { height = 160, flexShrink = 0 } });
            // Anywhere in the track area (rows add their own items above these): add tracks and child calls.
            ContextMenu(scroll, (menu, position) =>
            {
                if (_state == null) return;
                var x = this.WorldToLocal(position).x - HeaderWidth;
                var time = x >= 0f ? XToTime(x) : _time;
                menu.Separator();
                menu.Add("Paste Keys at Playhead (Ctrl+V)", () => PasteRequested?.Invoke(null), enabled: CanPaste?.Invoke() == true);
                if (x >= 0f) menu.Add("Paste Keys Here", () => PasteRequested?.Invoke(time), enabled: CanPaste?.Invoke() == true);   // also after the end
                menu.Add("Add Track...", () => AddTrackMenuRequested?.Invoke(new Rect(position, Vector2.zero)));
                PopulateChildCallMenu?.Invoke(menu, time);
            });
            Add(scroll);
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 1 || _menu == null) return;
                var menu = _menu;
                _menu = null;
                menu.Show(this, e.position);
            });

            _box = new VisualElement { pickingMode = PickingMode.Ignore, style = { display = DisplayStyle.None } };
            _box.AddToClassList("kt-box");
            Add(_box);

            // Inside the rows, so it scrolls with them; added again after each rebuild.
            _span = new VisualElement { pickingMode = PickingMode.Ignore, style = { display = DisplayStyle.None } };
            _span.AddToClassList("kt-span");
            _span.Add(SpanHandle(left: true));
            _span.Add(SpanHandle(left: false));
            _rows.RegisterCallback<GeometryChangedEvent>(_ => LayoutSpan());

            _endShade = new VisualElement { pickingMode = PickingMode.Ignore };
            _endShade.AddToClassList("kt-end-shade");
            Add(_endShade);
            _endMarker = new Label { pickingMode = PickingMode.Ignore };
            _endMarker.AddToClassList("kt-end-marker");
            _ruler.Add(_endMarker);

            _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _playhead.AddToClassList("kt-playhead");
            Add(_playhead);

            RegisterCallback<GeometryChangedEvent>(_ => Layout());
            // Trickle down so the vertical ScrollView doesn't eat the wheel first. Over the ruler a plain wheel zooms too.
            RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            // Middle-button drag pans the time axis, like Unity's Animation window.
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 2 || _state == null) return;
                _viewLength = Range;
                _panStartView = ViewStart;
                _panStartPos = e.position;
                _panning = true;
                this.CapturePointer(e.pointerId);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_panning) return;
                _viewStart = Mathf.Max(0f, _panStartView - (e.position.x - _panStartPos.x) / PxPerSecond);
                Layout();
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_panning || e.button != 2) return;
                _panning = false;
                this.ReleasePointer(e.pointerId);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.F && !e.actionKey) { FitView(); e.StopPropagation(); }
            });
        }

        const float Inset = 9f;   // 0s sits this far into the lane, so a key there shows whole (a 12px key turned 45° is 17px wide)
        float LaneWidth => Mathf.Max(50f, layout.width - HeaderWidth - LaneRight - Inset);   // the span that shows Range
        // Visible range: the state's length with room to drag keys further out. Frozen while dragging so keys don't jitter.
        float Range => _frozenRange ?? _viewLength ?? FitRange;
        float FitRange => _state == null ? 1f : Mathf.Max(0.3f, Mathf.Ceil(_state.Duration * 1.4f / 0.05f) * 0.05f);
        float ViewStart => _viewLength.HasValue ? _viewStart : 0f;
        float PxPerSecond => LaneWidth / Range;
        float TimeToX(float t) => Inset + (t - ViewStart) * PxPerSecond;
        float XToTime(float x) => Mathf.Max(0f, Mathf.Round(((x - Inset) / PxPerSecond + ViewStart) / Snap) * Snap);

        void FitView()
        {
            _viewLength = null;
            _viewStart = 0f;
            Layout();
        }

        void OnWheel(WheelEvent e)
        {
            if (_state == null) return;
            var x = this.WorldToLocal(e.mousePosition).x - HeaderWidth;
            if (x < 0f) return;   // over the track headers: normal vertical scrolling
            var overRuler = _ruler.worldBound.Contains(e.mousePosition);
            var length = Range;
            // Windows sends Shift+wheel (and tilt wheels / trackpads) as horizontal scrolling, sometimes without the Shift flag.
            var horizontal = Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y);
            var delta = horizontal ? e.delta.x : e.delta.y;
            if (e.shiftKey || horizontal)
            {
                _viewLength = length;
                _viewStart = Mathf.Max(0f, ViewStart + Mathf.Clamp(delta, -30f, 30f) * length * 0.03f);   // one notch (≈3) ≈ 9% of the view
            }
            else if (e.actionKey || e.altKey || overRuler)
            {
                // Zoom around the time under the pointer so it stays put.
                var anchor = (x - Inset) / PxPerSecond + ViewStart;
                var newLength = Mathf.Clamp(length * Mathf.Pow(1.1f, Mathf.Clamp(delta, -10f, 10f)), 0.1f, 120f);   // one notch ≈ ±30%
                _viewLength = newLength;
                _viewStart = Mathf.Max(0f, anchor - (x - Inset) / LaneWidth * newLength);
            }
            else return;
            Layout();
            e.StopPropagation();
        }

        public void SetState(KeyTween root, KeyTweenState state, object selTrack, ICollection<KeyTweenKey> selection)
        {
            // Another object or state: start from the whole view. (By name: undo recreates the state objects.)
            if (root != _root || state?.name != _state?.name) { _viewLength = null; _viewStart = 0f; }
            _root = root;
            _state = state;
            _selTrack = selTrack;
            _selection = selection;
            RebuildRows();
            Layout();
        }

        public void SetTime(float t)
        {
            _time = t;
            var x = TimeToX(t);
            _playhead.style.left = HeaderWidth + x;
            _playhead.style.display = x < 0f || x > Inset + LaneWidth ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // The state's end: a tag on the ruler and a shade over everything after it.
        void LayoutEnd()
        {
            var duration = _state?.Duration ?? 0f;
            var x = TimeToX(duration);
            var visible = _state != null && x <= Inset + LaneWidth;
            _endShade.style.display = _endMarker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            x = Mathf.Max(0f, x);
            _endShade.style.left = HeaderWidth + x;
            _endShade.style.width = Inset + LaneWidth - x;
            _endMarker.style.left = x;
            _endMarker.text = $"End {duration:0.00}s";
        }

        /// <summary>Repaints selection and positions without rebuilding (rebuilding would kill an active drag).</summary>
        public void Refresh(object selTrack)
        {
            _selTrack = selTrack;
            foreach (var (key, el, _) in _keyElements) el.EnableInClassList("kt-key--selected", _selection.Contains(key));
            foreach (var (header, t) in _headers) header.EnableInClassList("kt-header--selected", t == _selTrack);
            foreach (var (item, el) in _markerElements) el.EnableInClassList("kt-marker--selected", item == SelectedMarker);
            Layout();
        }

        public static float TimeOf(object marker) => marker switch
        {
            KeyTweenEventMarker e => e.time,
            KeyTweenChildCall call => call.time,
            KeyTweenSound sound => sound.time,
            _ => 0f,
        };

        public static string TargetPath(KeyTween root, Component target)
        {
            if (target == null) return "(None)";
            if (root == null || target.transform == root.transform) return target.gameObject.name;
            var path = AnimationUtility.CalculateTransformPath(target.transform, root.transform);
            return string.IsNullOrEmpty(path) ? target.gameObject.name : path;
        }

        /// <summary>This prefab's override of the called state (its changed tracks), if any.</summary>
        public static KeyTweenOverride OverrideOf(KeyTween root, KeyTweenChildCall call) =>
            root != null ? root.overrides.Find(o => o.child == call.child && o.state == call.state) : null;

        /// <summary>
        /// Like Unity's nested prefabs: the override that supplies this (effective) track of the called state, on the child's
        /// ancestors in the current context (the outermost one wins). Null = the child's own track.
        /// </summary>
        public static (KeyTweenOverride o, KeyTween owner) LayerOf(KeyTweenChildCall call, KeyTweenTrack track)
        {
            if (call.child == null) return default;
            for (var t = call.child.transform.parent; t != null; t = t.parent)
                foreach (var ancestor in t.GetComponents<KeyTween>())
                foreach (var o in ancestor.overrides)
                    if (o.child == call.child && o.state == call.state && o.tracks.Contains(track)) return (o, ancestor);
            return default;
        }

        static int OverriddenCount(KeyTweenChildCall call) => call.Target?.tracks.Count(t => LayerOf(call, t).o != null) ?? 0;

        // ---------- rows ----------

        void RebuildRows()
        {
            _rows.Clear();
            _keyElements.Clear();
            _readOnlyKeys.Clear();
            _headers.Clear();
            _lanes.Clear();
            _tailHandles.Clear();
            _repaintLanes.Clear();
            _markerElements.Clear();
            _rowCount = 0;
            if (_state == null) return;
            AddNode(BuildTree(), 0);
        }

        // Tracks, event/sound tracks and child calls grouped under the objects they belong to.
        sealed class Node
        {
            public Transform transform;
            public string name;
            public readonly List<KeyTweenTrack> tracks = new();
            public readonly List<object> markerTracks = new();   // KeyTweenEventTrack / KeyTweenSoundTrack
            public readonly List<KeyTweenChildCall> calls = new();
            public readonly List<Node> children = new();
        }

        Node BuildTree()
        {
            var rootTransform = _root != null ? _root.transform : null;
            var top = new Node { transform = rootTransform, name = rootTransform != null ? rootTransform.name : "(None)" };
            var map = new Dictionary<Transform, Node>();
            if (rootTransform != null) map[rootTransform] = top;

            Node For(Transform tr)
            {
                if (tr == null) return top;
                if (map.TryGetValue(tr, out var node)) return node;
                // Outside the animated hierarchy: listed directly under the root.
                var parent = tr.parent != null && rootTransform != null && tr.IsChildOf(rootTransform) ? For(tr.parent) : top;
                node = new Node { transform = tr, name = tr.name };
                map[tr] = node;
                parent.children.Add(node);
                return node;
            }

            foreach (var track in _state.tracks) For(track.target != null ? track.target.transform : null).tracks.Add(track);
            foreach (var track in _state.eventTracks) top.markerTracks.Add(track);
            foreach (var track in _state.soundTracks) For(track.source != null ? track.source.transform : null).markerTracks.Add(track);
            foreach (var call in _state.childCalls) For(call.child != null ? call.child.transform : null).calls.Add(call);
            Sort(top);
            return top;

            static void Sort(Node node)
            {
                node.children.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                foreach (var child in node.children) Sort(child);
            }
        }

        void AddNode(Node node, int depth)
        {
            var collapsed = node.transform != null && _collapsed.Contains(node.transform);
            _rows.Add(MakeNodeRow(node, depth, collapsed));
            if (collapsed) return;
            foreach (var track in node.markerTracks) _rows.Add(MakeMarkerTrackRow(track, depth + 1));
            foreach (var track in node.tracks) _rows.Add(MakeRow(track, depth + 1, 0f, PropertyName(track)));
            foreach (var call in node.calls) AddCallRows(call, depth + 1);
            foreach (var child in node.children) AddNode(child, depth + 1);
        }

        static void SubtreeTracks(Node node, List<KeyTweenTrack> tracks)
        {
            tracks.AddRange(node.tracks);
            foreach (var child in node.children) SubtreeTracks(child, tracks);
        }

        // Right-click contributions bubble from the clicked element up to this view, which shows the menu (see constructor).
        void ContextMenu(VisualElement el, Action<KeyTweenMenu, Vector2> populate) =>
            el.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 1) return;
                populate(_menu ??= new KeyTweenMenu(), e.position);
            });

        void ContextMenu(VisualElement el, Action<KeyTweenMenu> populate) => ContextMenu(el, (menu, _) => populate(menu));

        void CallMenu(KeyTweenMenu menu, KeyTweenChildCall call)
        {
            menu.Add("Revert All (outermost overrides)", () => RevertOverrideRequested?.Invoke(call), enabled: OverriddenCount(call) > 0);
            menu.Add($"Remove Child Call  ({(call.child != null ? call.child.name : "None")} · {call.state})", () => RemoveRequested?.Invoke(call), danger: true);
        }

        static VisualElement Indent(int depth) => new() { pickingMode = PickingMode.Ignore, style = { width = 4 + depth * 14, flexShrink = 0 } };

        VisualElement Header(int depth, Color? stripe, string title, string titleClass, object track)
        {
            var header = new VisualElement { style = { width = HeaderWidth } };
            header.AddToClassList("kt-header");
            header.Add(Indent(depth));
            if (stripe.HasValue)
            {
                var bar = new VisualElement { style = { backgroundColor = stripe.Value } };
                bar.AddToClassList("kt-stripe");
                header.Add(bar);
            }
            var label = new Label(title) { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
            label.AddToClassList(titleClass);
            header.Add(label);
            if (track != null)
            {
                header.EnableInClassList("kt-header--selected", track == _selTrack);
                header.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button == 0 && !(e.target is Button)) { TrackSelected?.Invoke(track); e.StopPropagation(); }
                });
                _headers.Add((header, track));
            }
            return header;
        }

        VisualElement MakeNodeRow(Node node, int depth, bool collapsed)
        {
            var row = new VisualElement();
            row.AddToClassList("kt-node-row");

            var header = new VisualElement { style = { width = HeaderWidth } };
            header.AddToClassList("kt-header");
            header.AddToClassList("kt-node-header");
            header.Add(Indent(depth));
            var fold = new Label(collapsed ? "▸" : "▾") { pickingMode = PickingMode.Ignore };
            fold.AddToClassList("kt-fold");
            header.Add(fold);
            var name = new Label(node.name) { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
            name.AddToClassList("kt-node-name");
            header.Add(name);
            header.tooltip = node.transform != null ? TargetPath(_root, node.transform) : "";
            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || node.transform == null) return;
                if (!_collapsed.Remove(node.transform)) _collapsed.Add(node.transform);
                RebuildRows();
                Layout();
                e.StopPropagation();
            });
            row.Add(header);

            // The object's lane: child calls on this object, plus (when folded) a summary of the keys below it.
            var lane = MarkerLane(null, "Double-click: add child call · Drag a bar: move · Delete: remove");
            lane.AddToClassList("kt-node-lane");
            if (collapsed)
            {
                var summary = new List<KeyTweenTrack>();
                SubtreeTracks(node, summary);
                lane.generateVisualContent += ctx => DrawSummary(ctx, lane, summary);
                _repaintLanes.Add(lane);
            }
            foreach (var call in node.calls) AddMarker(lane, call, new Label($"▶ {call.state}"), "kt-call");
            row.Add(lane);
            return row;
        }

        // Animation event track / sound track: a marker lane (+ drop AudioClips on a sound track).
        VisualElement MakeMarkerTrackRow(object track, int depth)
        {
            var row = new VisualElement();
            row.AddToClassList("kt-row");
            if (_rowCount++ % 2 == 1) row.AddToClassList("kt-row--odd");

            VisualElement header;
            VisualElement lane;
            if (track is KeyTweenEventTrack events)
            {
                header = Header(depth, EventColor, "Animation Events", "kt-track-prop", track);
                header.Add(SmallButton("+", "Add animation event at playhead", () => AddEventRequested?.Invoke(events, header.worldBound, _time)));
                lane = MarkerLane(track, "Double-click: add event · Drag: move · Delete: remove");
                foreach (var e in events.events)
                    AddMarker(lane, e, new Label("▼ " + e.name + (string.IsNullOrEmpty(e.dataType) ? "" : " •")), "kt-signal");
            }
            else
            {
                var sounds = (KeyTweenSoundTrack)track;
                header = Header(depth, SoundColor, sounds.source != null ? $"Sound · {sounds.source.name}" : "Sound", "kt-track-prop", track);
                header.Add(SmallButton("+", "Add sound at playhead", () => AddSoundRequested?.Invoke(sounds, _time, null)));
                lane = MarkerLane(track, "Drop AudioClips or double-click: add sound · Drag: move · Delete: remove");
                lane.RegisterCallback<DragUpdatedEvent>(_ =>
                {
                    if (DragAndDrop.objectReferences.OfType<AudioClip>().Any()) DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                });
                lane.RegisterCallback<DragPerformEvent>(e =>
                {
                    var clips = DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();
                    if (clips.Length == 0) return;
                    DragAndDrop.AcceptDrag();
                    var time = XToTime(lane.WorldToLocal(e.mousePosition).x);
                    foreach (var clip in clips) AddSoundRequested?.Invoke(sounds, time, clip);
                });
                foreach (var sound in sounds.sounds)
                    AddMarker(lane, sound, new Label("♪ " + (sound.clip != null ? sound.clip.name : "(No Clip)")), "kt-sound");
            }
            header.Add(SmallButton("×", "Remove track", () => RemoveRequested?.Invoke(track)));
            ContextMenu(header, menu =>
            {
                if (track is KeyTweenEventTrack et) menu.Add("Add Event at Playhead", () => AddEventRequested?.Invoke(et, header.worldBound, _time));
                else menu.Add("Add Sound at Playhead", () => AddSoundRequested?.Invoke((KeyTweenSoundTrack)track, _time, null));
                menu.Add("Remove Track", () => RemoveRequested?.Invoke(track), danger: true);
            });
            row.Add(header);
            row.Add(lane);
            return row;
        }

        VisualElement MarkerLane(object owner, string tooltip)
        {
            var lane = new VisualElement { userData = owner, tooltip = tooltip, style = { flexGrow = 1, marginRight = LaneRight } };
            lane.AddToClassList("kt-marker-lane");
            lane.RegisterCallback<PointerDownEvent>(e => OnMarkerDown(lane, e));
            lane.RegisterCallback<PointerMoveEvent>(e => OnMarkerMove(lane, e));
            lane.RegisterCallback<PointerUpEvent>(e => OnMarkerUp(lane, e));
            return lane;
        }

        void AddMarker(VisualElement lane, object item, VisualElement el, string className)
        {
            el.userData = item;
            el.AddToClassList(className);
            el.EnableInClassList("kt-marker--selected", item == SelectedMarker);
            ContextMenu(el, menu =>
            {
                if (item is KeyTweenChildCall call) CallMenu(menu, call);
                else menu.Add("Delete", () => RemoveRequested?.Invoke(item), danger: true);
            });
            _markerElements.Add((item, el));
            lane.Add(el);
        }

        // A state called on a child KeyTween, shifted by the call time. Overridden in this prefab: editable here.
        // Still the child's own: shown read-only until ✎ copies it into this prefab.
        void AddCallRows(KeyTweenChildCall call, int depth)
        {
            var target = call.Target;
            if (target == null) return;
            var count = OverriddenCount(call);

            var row = new VisualElement();
            row.AddToClassList("kt-callee-row");
            var header = new VisualElement { style = { width = HeaderWidth } };
            header.AddToClassList("kt-header");
            header.Add(Indent(depth));
            var title = new Label($"▶ {call.state}  {(count > 0 ? $"{count} track(s) overridden" : "Original")}")
                { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
            title.AddToClassList(count > 0 ? "kt-callee-own" : "kt-callee-source");
            header.Add(title);
            var revertAll = SmallButton("↺", "Revert the outermost overrides (like Unity's Revert All)", () => RevertOverrideRequested?.Invoke(call));
            revertAll.SetEnabled(count > 0);
            header.Add(revertAll);
            header.tooltip = $"'{call.state}' of {call.child.name} as it plays here. Click a dimmed track to override just that track; " +
                             "the other tracks keep following the original. × on an overridden track reverts it.";
            ContextMenu(header, menu => CallMenu(menu, call));
            row.Add(header);
            row.Add(new VisualElement { style = { flexGrow = 1, marginRight = LaneRight } });
            _rows.Add(row);

            foreach (var track in target.tracks)
            {
                var label = $"{(track.target != null ? track.target.name : "(None)")} · {PropertyName(track)}";
                var own = LayerOf(call, track).o != null;
                _rows.Add(MakeRow(track, depth + 1, call.time, label, own ? null : call, own ? call : null));
            }
        }

        // Every keyframe track row, own or called. offset = where its time 0 sits on this timeline (a called child state starts later).
        // readOnlyOf: a child's original state, not overridden yet. Same row, dimmed; clicking it copies the state into this
        // prefab, after which the same row comes back editable.
        // overrideOf: a child's track overridden in this prefab — editable, name in the override color, × reverts it.
        VisualElement MakeRow(KeyTweenTrack track, int depth, float offset, string label, KeyTweenChildCall readOnlyOf = null,
            KeyTweenChildCall overrideOf = null)
        {
            var readOnly = readOnlyOf != null;
            var overridden = overrideOf != null;
            var row = new VisualElement();
            row.AddToClassList("kt-row");
            row.EnableInClassList("kt-row--readonly", readOnly);
            if (_rowCount++ % 2 == 1) row.AddToClassList("kt-row--odd");

            var header = Header(depth, PropertyColor(track), label, "kt-track-prop", readOnly ? null : track);
            header.Q<Label>()?.EnableInClassList("kt-track-override", overridden);
            header.tooltip = readOnly ? $"Original track of '{readOnlyOf.state}' on {readOnlyOf.child.name}: click to override just this track in this prefab."
                : overridden ? $"Overridden in {LayerOf(overrideOf, track).owner.name}. × reverts this override."
                : track.target != null ? $"{TargetPath(_root, track.target)} · {track.target.GetType().Name}" : "No target";
            var addKey = SmallButton("◆", "Add key at playhead", () => AddKeyRequested?.Invoke(track, Mathf.Max(0f, _time - offset)));
            var remove = SmallButton("×", overridden ? "Revert this track to the original" : "Remove track", () => RemoveRequested?.Invoke(track));
            addKey.SetEnabled(!readOnly);
            remove.SetEnabled(!readOnly);
            header.Add(addKey);
            header.Add(remove);
            row.Add(header);

            var lane = new VisualElement { userData = readOnly ? null : track, style = { marginRight = LaneRight } };
            lane.AddToClassList("kt-lane");
            lane.generateVisualContent += ctx => DrawTrackLine(ctx, lane, track, offset);
            lane.RegisterCallback<PointerMoveEvent>(e => lane.tooltip = readOnly ? header.tooltip : TailTooltip(track, e.localPosition.x, offset));
            if (readOnly)
            {
                void Override(IPointerEvent e) => OverrideRequested?.Invoke(readOnlyOf, track);
                header.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { Override(e); e.StopPropagation(); } });
                lane.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { Override(e); e.StopPropagation(); } });   // before the key gestures on _rows
                ContextMenu(header, menu => CallMenu(menu, readOnlyOf));
                ContextMenu(lane, menu => CallMenu(menu, readOnlyOf));
            }
            else
            {
                ContextMenu(header, menu =>
                {
                    menu.Add("Add Key at Playhead", () => AddKeyRequested?.Invoke(track, Mathf.Max(0f, _time - offset)));
                    menu.Add(overridden ? "Revert Track to Original" : "Remove Track", () => RemoveRequested?.Invoke(track), danger: true);
                });
                ContextMenu(lane, (menu, position) =>
                {
                    var t = Mathf.Max(0f, XToTime(lane.WorldToLocal(position).x) - offset);
                    menu.Add("Add Key Here", () => AddKeyRequested?.Invoke(track, t));
                    menu.Add("Copy Selected Keys (Ctrl+C)", () => CopyRequested?.Invoke(), enabled: _selection.Count > 0);
                    menu.Add("Paste Keys at Playhead (Ctrl+V)", () => PasteRequested?.Invoke(null), enabled: CanPaste?.Invoke() == true);
                    menu.Add("Paste Keys Here", () => PasteRequested?.Invoke(t + offset), enabled: CanPaste?.Invoke() == true);
                    menu.Add("Delete Selected Keys", () => DeleteRequested?.Invoke(), enabled: _selection.Count > 0, danger: true);
                    menu.Add(overridden ? "Revert Track to Original" : "Remove Track", () => RemoveRequested?.Invoke(track), danger: true);
                });
            }
            foreach (var key in track.keys) lane.Add(MakeKey(key, offset, readOnly));
            if (!readOnly && KeyTweenTextChars.IsCharProperty(track.property) && CharCount(track) > 1) lane.Add(MakeTailHandle(track, offset));
            if (!readOnly) _lanes[track] = (lane, offset);
            _repaintLanes.Add(lane);
            row.Add(lane);
            return row;
        }

        // One bar over the span the track moves, the same for every track: from the first key (or from 0 when a lone key is
        // reached from the live start value) to the last key. A zero-length span is just its key.
        void DrawTrackLine(MeshGenerationContext ctx, VisualElement lane, KeyTweenTrack track, float offset)
        {
            if (track.keys.Count == 0) return;
            var p = ctx.painter2D;
            var y = lane.contentRect.height * 0.5f;
            var color = PropertyColor(track);

            var first = track.keys[0].time;
            var last = track.keys[track.keys.Count - 1].time;
            var from = track.startFromCurrent && track.keys.Count == 1 ? 0f : first;
            p.lineWidth = 4f;
            if (last > from)
            {
                p.strokeColor = new Color(color.r, color.g, color.b, 0.85f);
                p.BeginPath();
                p.MoveTo(new Vector2(TimeToX(offset + from), y));
                p.LineTo(new Vector2(TimeToX(offset + last), y));
                p.Stroke();
            }

            // Character tracks: the keys are the first character's motion; later characters finish at track.End.
            if (track.End > last)
            {
                p.strokeColor = new Color(color.r, color.g, color.b, 0.3f);
                p.BeginPath();
                p.MoveTo(new Vector2(TimeToX(offset + last), y));
                p.LineTo(new Vector2(TimeToX(offset + track.End), y));
                p.Stroke();
            }
        }

        static int CharCount(KeyTweenTrack track) => track.target is TMPro.TMP_Text text && text.textInfo != null ? text.textInfo.characterCount : 0;

        // The end of a character track (where the last character finishes): drag it to change the stagger, like resizing a clip.
        VisualElement MakeTailHandle(KeyTweenTrack track, float offset)
        {
            var handle = new VisualElement { style = { backgroundColor = PropertyColor(track) } };
            handle.AddToClassList("kt-tail-handle");
            var dragging = false;
            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                dragging = true;
                _frozenRange = Range;
                handle.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                var last = CharCount(track) - 1;
                var end = XToTime(handle.parent.WorldToLocal(e.position).x) - offset;
                // End = last x stagger + (last x durationPerChar, or the last key's time).
                var start = track.durationPerChar > 0f ? last * track.durationPerChar : track.keys[track.keys.Count - 1].time;
                StaggerDragged?.Invoke(track, Mathf.Max(0f, Mathf.Round((end - start) / last * 1000f) / 1000f));
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                _frozenRange = null;
                handle.ReleasePointer(e.pointerId);
                StaggerDragFinished?.Invoke();
                e.StopPropagation();
            });
            _tailHandles.Add((track, handle, offset));
            return handle;
        }

        // Explains the faded tail while hovering it.
        string TailTooltip(KeyTweenTrack track, float x, float offset)
        {
            if (track.keys.Count == 0) return "";
            var last = track.keys[track.keys.Count - 1].time;
            var end = track.End;
            if (end <= last || x < TimeToX(offset + last) || x > TimeToX(offset + end)) return "";
            return $"Character delay +{end - last:0.00}s (ends {end:0.00}s)\n" +
                   "The keys are the first character's motion; each later character starts 'Stagger' later and plays until here.\n" +
                   "Drag the end handle to change 'Stagger', or edit it in the track inspector.";
        }

        // Folded object: a small tick per key of every track under it.
        void DrawSummary(MeshGenerationContext ctx, VisualElement lane, List<KeyTweenTrack> tracks)
        {
            var p = ctx.painter2D;
            var y = lane.contentRect.height * 0.5f;
            p.fillColor = new Color(0.8f, 0.82f, 0.86f, 0.7f);
            foreach (var track in tracks)
            foreach (var key in track.keys)
                Diamond(p, TimeToX(key.time), y, 4f);
        }

        static void Diamond(Painter2D p, float x, float y, float r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y - r));
            p.LineTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x, y + r));
            p.LineTo(new Vector2(x - r, y));
            p.ClosePath();
            p.Fill();
        }

        static Button SmallButton(string text, string tooltip, Action onClick)
        {
            var b = new Button(onClick) { text = text, tooltip = tooltip };
            b.AddToClassList("kt-smallbtn");
            return b;
        }

        VisualElement MakeKey(KeyTweenKey key, float offset, bool readOnly = false)
        {
            var el = new VisualElement { userData = readOnly ? null : key };
            el.AddToClassList("kt-key");
            el.EnableInClassList("kt-key--selected", !readOnly && _selection.Contains(key));
            (readOnly ? _readOnlyKeys : _keyElements).Add((key, el, offset));
            return el;
        }

        static readonly float[] Steps = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f };

        void Layout()
        {
            _ruler.Clear();
            _ruler.Add(_endMarker);
            if (_state == null) { LayoutEnd(); return; }

            // Widest labels ("10.25") need ~44px; pick the finest step that keeps them apart at this zoom.
            var step = Steps[Steps.Length - 1];
            foreach (var s in Steps)
                if (s * PxPerSecond >= 44f) { step = s; break; }
            for (var i = Mathf.FloorToInt(ViewStart / step); i * step <= ViewStart + Range + 0.0001f; i++)
            {
                var t = i * step;
                if (t < ViewStart - 0.0001f) continue;
                var tick = new Label(t.ToString(step < 0.1f ? "0.00" : step < 1f ? "0.0#" : "0")) { pickingMode = PickingMode.Ignore, style = { left = TimeToX(t) } };
                tick.AddToClassList("kt-tick");
                _ruler.Add(tick);
            }

            foreach (var (key, el, offset) in _keyElements.Concat(_readOnlyKeys))
            {
                el.style.left = TimeToX(offset + key.time) - 6f;
                el.tooltip = offset > 0f ? $"{key.time:0.00}s ({offset + key.time:0.00}s on this timeline)" : $"{key.time:0.00}s";
            }
            foreach (var (item, el) in _markerElements)
            {
                switch (item)
                {
                    case KeyTweenEventMarker e:
                        el.style.left = TimeToX(e.time) - 5f;   // the ▼ tip sits on the event time
                        var data = e.DataType;
                        el.tooltip = $"{e.name} · {e.time:0.00}s" + (data != null ? $" · {data.Name}" : "");
                        break;
                    case KeyTweenSound sound:
                        var clipLength = sound.clip != null ? sound.clip.length : 0f;   // the bar shows how long the clip plays
                        el.style.left = TimeToX(sound.time);
                        el.style.width = Mathf.Max(24f, clipLength * PxPerSecond);
                        el.tooltip = sound.clip != null
                            ? $"{sound.clip.name} · {sound.time:0.00}s ~ {sound.End:0.00}s ({clipLength:0.00}s)"
                            : $"No clip · {sound.time:0.00}s";
                        break;
                    case KeyTweenChildCall call:
                        var length = call.Target?.Duration ?? 0f;   // the bar spans the called state
                        el.style.left = TimeToX(call.time);
                        el.style.width = Mathf.Max(24f, length * PxPerSecond);
                        el.tooltip = $"Plays '{call.state}' of {(call.child != null ? call.child.name : "(None)")} at {call.time:0.00}s ({length:0.00}s)";
                        break;
                }
            }
            foreach (var (track, el, offset) in _tailHandles)
            {
                el.style.left = TimeToX(offset + track.End) - 3f;
                el.tooltip = $"Ends {offset + track.End:0.00}s · Stagger {track.stagger:0.###}s × {CharCount(track) - 1}\nDrag: change the stagger (character delay)";
            }
            foreach (var lane in _repaintLanes) lane.MarkDirtyRepaint();
            LayoutSpan();
            LayoutEnd();
            SetTime(_time);
        }

        /// <summary>Where a track's keys start on this timeline: a called child state's track starts at the call's time.</summary>
        public float OffsetOf(KeyTweenTrack track) => _lanes.TryGetValue(track, out var lane) ? lane.offset : 0f;

        /// <summary>Where a key is drawn on this timeline: its track's start (a child call's time) is added to its own time.</summary>
        public float OffsetOf(KeyTweenKey key)
        {
            foreach (var (k, _, offset) in _keyElements)
                if (k == key) return offset;
            return 0f;
        }

        // The selected keys' time range on the rows they are on. Shown for two or more keys at different times.
        void LayoutSpan()
        {
            if (_span.parent != _rows) _rows.Add(_span);
            float from = float.MaxValue, to = float.MinValue, top = float.MaxValue, bottom = float.MinValue;
            var count = 0;
            VisualElement firstLane = null;
            foreach (var (key, el, offset) in _keyElements)
            {
                if (!_selection.Contains(key) || el.parent == null) continue;
                count++;
                firstLane ??= el.parent;
                from = Mathf.Min(from, offset + key.time);
                to = Mathf.Max(to, offset + key.time);
                var lane = _rows.WorldToLocal(el.parent.worldBound);
                top = Mathf.Min(top, lane.yMin);
                bottom = Mathf.Max(bottom, lane.yMax);
            }
            // Rows just rebuilt (after a drag the keys are re-sorted) have no layout yet, and the rows' own size may not change:
            // again when the lane is laid out, and on the next frame in case that came first.
            if (count >= 2 && !(bottom > top))
            {
                _span.style.display = DisplayStyle.None;
                firstLane.RegisterCallback<GeometryChangedEvent>(OnLaneLaidOut);
                schedule.Execute(LayoutSpan);
                return;
            }
            var show = count >= 2 && to - from >= Snap - 1e-4f;
            _span.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            _span.style.left = HeaderWidth + TimeToX(from) - 12f;   // the handles sit just outside the end keys
            _span.style.width = (to - from) * PxPerSecond + 24f;
            _span.style.top = top + 2f;
            _span.style.height = bottom - top - 4f;
            _span.BringToFront();
        }

        void OnLaneLaidOut(GeometryChangedEvent e)
        {
            ((VisualElement)e.target).UnregisterCallback<GeometryChangedEvent>(OnLaneLaidOut);
            LayoutSpan();
        }

        VisualElement SpanHandle(bool left)
        {
            var handle = new VisualElement { tooltip = "Drag: stretch or shrink the selected keys (the other end stays)" };
            handle.AddToClassList("kt-span-handle");
            handle.AddToClassList(left ? "kt-span-handle--left" : "kt-span-handle--right");
            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                var times = _keyElements.Where(k => _selection.Contains(k.key)).Select(k => k.offset + k.key.time).ToList();
                (_scaleFrom, _scalePivot) = left ? (times.Min(), times.Max()) : (times.Max(), times.Min());
                _scaleGrab = this.WorldToLocal(e.position).x - HeaderWidth - TimeToX(_scaleFrom);   // keep the grab point under the pointer
                _frozenRange = Range;
                SelectedKeysDragStarted?.Invoke();
                handle.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!handle.HasPointerCapture(e.pointerId)) return;
                var edge = XToTime(this.WorldToLocal(e.position).x - HeaderWidth - _scaleGrab);
                // The handle does not pass the other end: the keys keep their order.
                edge = left ? Mathf.Min(edge, _scalePivot - Snap) : Mathf.Max(edge, _scalePivot + Snap);
                SelectedKeysScaled?.Invoke(_scalePivot, (edge - _scalePivot) / (_scaleFrom - _scalePivot));
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!handle.HasPointerCapture(e.pointerId)) return;
                handle.ReleasePointer(e.pointerId);
                _frozenRange = null;
                DragFinished?.Invoke();
                e.StopPropagation();
            });
            return handle;
        }

        // ---------- marker lanes (event tracks, sound tracks, child calls on object rows) ----------

        void OnMarkerDown(VisualElement lane, PointerDownEvent e)
        {
            if (e.button != 0 || _state == null) return;
            Focus();
            var x = lane.WorldToLocal(e.position).x;
            _dragMarker = FindMarker(e.target as VisualElement);
            _markerDragged = false;
            if (_dragMarker == null)
            {
                if (e.clickCount == 2)
                {
                    var at = new Rect(e.position, Vector2.zero);
                    switch (lane.userData)
                    {
                        case KeyTweenEventTrack events: AddEventRequested?.Invoke(events, at, XToTime(x)); break;
                        case KeyTweenSoundTrack sounds: AddSoundRequested?.Invoke(sounds, XToTime(x), null); break;
                        default: AddCallRequested?.Invoke(at, XToTime(x)); break;
                    }
                }
                else
                {
                    MarkerClicked?.Invoke(null);
                    Scrubbed?.Invoke(XToTime(x));
                }
                return;
            }
            MarkerClicked?.Invoke(_dragMarker);
            _frozenRange = Range;
            _dragOffset = x - TimeToX(TimeOf(_dragMarker));   // keep the grab point under the pointer
            lane.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMarkerMove(VisualElement lane, PointerMoveEvent e)
        {
            if (_dragMarker == null || !lane.HasPointerCapture(e.pointerId)) return;
            _markerDragged = true;
            MarkerMoved?.Invoke(_dragMarker, XToTime(lane.WorldToLocal(e.position).x - _dragOffset));
            e.StopPropagation();
        }

        void OnMarkerUp(VisualElement lane, PointerUpEvent e)
        {
            if (_dragMarker == null) return;
            lane.ReleasePointer(e.pointerId);
            _frozenRange = null;
            if (_markerDragged) MarkerDragFinished?.Invoke();
            _dragMarker = null;
            e.StopPropagation();
        }

        static object FindMarker(VisualElement el)
        {
            for (; el != null; el = el.parent)
                if (el.userData is KeyTweenEventMarker or KeyTweenChildCall or KeyTweenSound) return el.userData;
            return null;
        }

        // ---------- pointer gestures on the keyframe lanes: click / Shift+click / box select / drag selected keys ----------

        static bool IsAdditive(IPointerEvent e) => e.shiftKey || e.actionKey;

        // x in the key column (0 = its left edge), for points that are on no keyframe lane.
        float LaneX(Vector2 world) => this.WorldToLocal(world).x - HeaderWidth;

        void OnRowsDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            var lane = FindLane(e.target as VisualElement);
            if (lane == null && (LaneX(e.position) < 0f || e.target is Button)) return;   // track headers handle their own clicks
            Focus();

            _downWorld = e.position;
            _downTrack = (KeyTweenTrack)lane?.userData;
            _downKey = lane == null ? null : (e.target as VisualElement)?.userData as KeyTweenKey;
            _downAdditive = IsAdditive(e);

            var now = UnityEditor.EditorApplication.timeSinceStartup;
            var doubleClick = e.clickCount == 2 || (now - _lastDownTime < 0.4 && ((Vector2)e.position - _lastDownPos).magnitude < 5f);
            _lastDownTime = doubleClick ? 0 : now;
            _lastDownPos = e.position;
            if (_downKey == null && doubleClick && _downTrack != null)
            {
                var offset = _lanes[_downTrack].offset;
                AddKeyRequested?.Invoke(_downTrack, Mathf.Max(0f, XToTime(lane.WorldToLocal(e.position).x) - offset));
                return;
            }
            // Clicking an unselected key selects it right away so it can be dragged in the same gesture.
            if (_downKey != null && !_selection.Contains(_downKey)) KeyClicked?.Invoke(_downKey, _downAdditive);

            _gesture = Gesture.Pending;
            _keyArea.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnRowsMove(PointerMoveEvent e)
        {
            if (_gesture == Gesture.None || !_keyArea.HasPointerCapture(e.pointerId)) return;
            var delta = (Vector2)e.position - _downWorld;

            if (_gesture == Gesture.Pending && delta.magnitude > DragThreshold)
            {
                _gesture = _downKey != null ? Gesture.DragKeys : Gesture.Box;
                if (_gesture == Gesture.DragKeys)
                {
                    _frozenRange = Range;
                    SelectedKeysDragStarted?.Invoke();
                }
            }

            if (_gesture == Gesture.DragKeys)
                SelectedKeysMoved?.Invoke(Mathf.Round(delta.x / PxPerSecond / Snap) * Snap);
            else if (_gesture == Gesture.Box)
            {
                var a = this.WorldToLocal(_downWorld);
                var b = this.WorldToLocal(e.position);
                _box.style.display = DisplayStyle.Flex;
                _box.style.left = Mathf.Min(a.x, b.x);
                _box.style.top = Mathf.Min(a.y, b.y);
                _box.style.width = Mathf.Abs(a.x - b.x);
                _box.style.height = Mathf.Abs(a.y - b.y);
            }
            e.StopPropagation();
        }

        void OnRowsUp(PointerUpEvent e)
        {
            if (_gesture == Gesture.None) return;
            _keyArea.ReleasePointer(e.pointerId);
            var gesture = _gesture;
            _gesture = Gesture.None;

            switch (gesture)
            {
                case Gesture.Pending:   // a plain click
                    if (_downKey != null) KeyClicked?.Invoke(_downKey, _downAdditive);
                    else
                    {
                        if (!_downAdditive) SelectionCleared?.Invoke();
                        Scrubbed?.Invoke(XToTime(_downTrack != null ? _lanes[_downTrack].lane.WorldToLocal(_downWorld).x : LaneX(_downWorld)));
                    }
                    break;
                case Gesture.DragKeys:
                    _frozenRange = null;
                    DragFinished?.Invoke();
                    break;
                case Gesture.Box:
                    var box = _box.worldBound;
                    _box.style.display = DisplayStyle.None;
                    var hits = new List<KeyTweenKey>();
                    foreach (var (key, el, _) in _keyElements)
                        if (box.Overlaps(el.worldBound)) hits.Add(key);
                    BoxSelected?.Invoke(hits, _downAdditive);
                    break;
            }
            e.StopPropagation();
        }

        VisualElement FindLane(VisualElement el)
        {
            for (; el != null && el != _keyArea; el = el.parent)
                if (el.ClassListContains("kt-lane")) return el;
            return null;
        }
    }
}
