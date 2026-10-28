using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Plays a KeyTween state in edit mode without entering Play mode.
    /// Values are driven through AnimationMode, so Unity reverts them on Stop and never saves them;
    /// a snapshot restore and stop-before-save hooks back that up.
    /// In Play mode the values are written directly (nothing is saved there) and the snapshot puts them back on Stop.
    /// </summary>
    internal sealed class KeyTweenPreview : IDisposable
    {
        public KeyTween Target { get; private set; }
        public KeyTweenState State { get; private set; }
        public float Time { get; private set; }
        public bool IsActive => Target != null;
        public bool IsPlaying { get; private set; }
        public bool Loop { get; set; }
        public float Speed { get; set; } = 1f;

        public event Action Changed;

        readonly AnimationModeDriver _driver;
        readonly List<(KeyTweenTrack track, Vector4 value)> _snapshot = new();
        readonly List<(TMP_Text text, string value)> _texts = new();
        // Object references a track can replace (a text's material becomes an instance): put back, the instance deleted.
        readonly List<(UnityEngine.Object owner, string path, UnityEngine.Object value)> _references = new();
        readonly List<(KeyTweenTrack track, Vector4 value)> _childRest = new();   // called child states: their rest before the call   // scramble rewrites text; restored on stop
        double _lastTick;
        float _soundCursor;   // sounds after this time play on the next tick
        bool _inPrefabStage;
        bool _live;   // Play mode: no AnimationMode

        public KeyTweenPreview()
        {
            _driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            _driver.hideFlags = HideFlags.HideAndDontSave;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabSaved += OnPrefabSaved;
        }

        public void Dispose()
        {
            Stop();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            UnityEngine.Object.DestroyImmediate(_driver);
        }

        /// <summary>Starts (or restarts from the rest pose) previewing a state, showing time t.</summary>
        public void Begin(KeyTween target, KeyTweenState state, float t = 0f)
        {
            Stop();
            _live = EditorApplication.isPlaying;
            if (target == null || state == null || EditorApplication.isPlayingOrWillChangePlaymode != _live) return;   // not while switching modes

            Target = target;
            State = state;
            Target.CaptureRest();

            _snapshot.Clear();
            _texts.Clear();
            _childRest.Clear();
            _references.Clear();
            foreach (var s in target.states) SnapshotTracks(s, false);
            // Child KeyTweens called from this one: same snapshot/restore, and their rest is shown until their call.
            foreach (var child in CalledChildren(target))
            {
                child.CaptureRest();
                foreach (var s in child.states) SnapshotTracks(s, true);
            }

            // Prefab Mode only redraws values written inside a sampling block (as the Animation window samples), and
            // there EndSampling would revert registered properties; so nothing is registered and the snapshot restore
            // below (plus stop-before-save) is what keeps the asset untouched.
            _inPrefabStage = !_live && PrefabStageUtility.GetPrefabStage(target.gameObject) != null;
            if (!_live) AnimationMode.StartAnimationMode(_driver);
            if (!_live && !_inPrefabStage)
            {
                AnimationMode.BeginSampling();
                foreach (var (track, _) in _snapshot) Register(target, track);
                AnimationMode.EndSampling();
            }

            // Put the object where this state is normally played from (e.g. Off starts from the end of On).
            var from = PreviewFrom(target, state);
            if (from != null)
            {
                KeyTween.Resolve(from);
                KeyTween.Evaluate(from, from.Duration);
            }

            KeyTween.ResolveTree(state);
            Seek(t);
        }

        void SnapshotTracks(KeyTweenState state, bool child)
        {
            foreach (var track in state.tracks)
            {
                if (track.target == null) continue;
                var value = track.Type.Read(track.target);
                _snapshot.Add((track, value));
                if (child) _childRest.Add((track, value));
                Fields.Clear();
                track.Type.SerializedFields(track.target, Fields);
                foreach (var (owner, path) in Fields)
                    if (new SerializedObject(owner).FindProperty(path) is { propertyType: SerializedPropertyType.ObjectReference } reference
                        && !_references.Exists(r => r.owner == owner && r.path == path))
                        _references.Add((owner, path, reference.objectReferenceValue));
                if (track.target is TMP_Text text) _texts.Add((text, text.text));
            }
        }

        static List<KeyTween> CalledChildren(KeyTween root)
        {
            var found = new List<KeyTween>();
            var pending = new Queue<KeyTween>();
            pending.Enqueue(root);
            while (pending.Count > 0 && found.Count < 64)
            {
                foreach (var s in pending.Dequeue().states)
                foreach (var call in s.childCalls)
                    if (call.child != null && call.child != root && !found.Contains(call.child))
                    {
                        found.Add(call.child);
                        pending.Enqueue(call.child);
                    }
            }
            return found;
        }

        // Children start from their rest pose at every seek, so scrubbing back before a call shows them untouched.
        void EvaluateAt(float t)
        {
            foreach (var (track, value) in _childRest) track.Type.Write(track.target, value);
            KeyTween.EvaluateTree(State, t);
        }

        // A state bound to the second half of an event pair (Exit, Up, Deselect, ToggleOff) is previewed from the end
        // of the state bound to the first half, so its motion is visible instead of starting at the rest pose.
        static readonly Dictionary<KeyTweenEvent, KeyTweenEvent> PairedWith = new()
        {
            [KeyTweenEvent.PointerExit] = KeyTweenEvent.PointerEnter,
            [KeyTweenEvent.PointerUp] = KeyTweenEvent.PointerDown,
            [KeyTweenEvent.Deselect] = KeyTweenEvent.Select,
            [KeyTweenEvent.ToggleOff] = KeyTweenEvent.ToggleOn,
        };

        // States played from code have no trigger; their names pair them instead: "Open" is previewed from the end
        // of "Close" (the popup is spawned closed), "Show" from "Hide", ...
        static readonly (string from, string state)[] NamePairs =
        {
            ("Close", "Open"), ("Open", "Close"), ("Hide", "Show"), ("Show", "Hide"), ("Out", "In"), ("In", "Out"),
        };

        static KeyTweenState PreviewFrom(KeyTween target, KeyTweenState state)
        {
            if (target.TryGetComponent<KeyTweenTrigger>(out var trigger))
                foreach (var b in trigger.bindings)
                {
                    if (b.state != state.name || !PairedWith.TryGetValue(b.evt, out var first)) continue;
                    foreach (var other in trigger.bindings)
                        if (other.evt == first && Find(target, state, other.state) is { } fromTrigger) return fromTrigger;
                }
            foreach (var (from, name) in NamePairs)
                if (string.Equals(state.name, name, StringComparison.OrdinalIgnoreCase) && Find(target, state, from) is { } fromName)
                    return fromName;
            return null;
        }

        static KeyTweenState Find(KeyTween target, KeyTweenState except, string name)
        {
            foreach (var s in target.states)
                if (s != except && string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public void Seek(float t)
        {
            if (!IsActive) return;
            Time = Mathf.Clamp(t, 0f, State.Duration);
            // Scenes: written outside Begin/EndSampling on purpose, since EndSampling restores registered properties to
            // their recorded originals; registration alone keeps them revert-on-stop and unsaved.
            // Prefab Mode: nothing is registered, and only writes inside a sampling block get redrawn.
            if (_inPrefabStage)
            {
                AnimationMode.BeginSampling();
                EvaluateAt(Time);
                AnimationMode.EndSampling();
            }
            else EvaluateAt(Time);
            Repaint();
            Changed?.Invoke();
        }

        public void Play()
        {
            if (!IsActive) return;
            if (Time >= State.Duration) Seek(0f);
            IsPlaying = true;
            _lastTick = EditorApplication.timeSinceStartup;
            _soundCursor = Time > 0f ? Time : -1f;   // a sound at 0 plays when playback starts from 0
            Changed?.Invoke();
        }

        public void Pause()
        {
            IsPlaying = false;
            Changed?.Invoke();
        }

        /// <summary>Re-applies the state at the current time, e.g. after a clip was edited.</summary>
        public void Refresh()
        {
            if (!IsActive) return;
            var (target, state, t, playing) = (Target, State, Time, IsPlaying);
            Begin(target, state, t);
            IsPlaying = playing;
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsPlaying = false;
            KeyTweenAudioPreview.StopAll();
            if (AnimationMode.InAnimationMode(_driver)) AnimationMode.StopAnimationMode(_driver);

            // Fallback: AnimationMode has already reverted registered properties; this covers anything it missed.
            WriteOriginals();
            _snapshot.Clear();
            _texts.Clear();
            _childRest.Clear();
            _references.Clear();

            Target = null;
            State = null;
            Time = 0f;
            Repaint();
            Changed?.Invoke();
        }

        void Tick()
        {
            if (!IsPlaying || !IsActive) return;
            if (Target == null) { Stop(); return; }   // destroyed while previewing

            var now = EditorApplication.timeSinceStartup;
            var t = Time + (float)(now - _lastTick) * Speed;
            _lastTick = now;

            // Sounds the playhead passed this tick (only while playing; scrubbing stays silent).
            foreach (var track in State.soundTracks)
            foreach (var sound in track.sounds)
                if (sound.time > _soundCursor && sound.time <= t) KeyTweenAudioPreview.Play(sound.clip);
            _soundCursor = t;

            var duration = State.Duration;
            if (t >= duration)
            {
                if ((Loop || State.loop) && duration > 0f)   // the preview button, or the state itself loops
                {
                    Begin(Target, State, t % duration);   // restart from the rest pose so Current clips loop cleanly
                    IsPlaying = true;
                    _lastTick = now;
                    _soundCursor = -1f;   // next loop plays its sounds again
                    return;
                }
                t = duration;
                IsPlaying = false;
            }
            Seek(t);
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change is PlayModeStateChange.ExitingEditMode or PlayModeStateChange.ExitingPlayMode) Stop();
        }

        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => Stop();
        // Revert/reopen replaces the previewed objects; stop first so the restore lands on the old ones, not the new scene.
        void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removing) => Stop();
        // The previewed prefab is being saved (Ctrl+S, or Unity's Prefab Mode auto save after an edit): the original values
        // go into the file, then the preview shows the same moment again. Stopping instead would turn the preview off after
        // every edit wherever Unity's auto save is on (its default).
        void OnPrefabSaving(GameObject prefab)
        {
            if (!IsActive || !Target.transform.IsChildOf(prefab.transform)) return;   // another prefab: nothing of ours in it
            WriteOriginals();
            _resumeAfterSave = true;
        }

        void OnPrefabSaved(GameObject prefab)
        {
            if (!_resumeAfterSave) return;
            _resumeAfterSave = false;
            if (IsActive) Seek(Time);
        }

        bool _resumeAfterSave;

        // The values the objects had before the preview (Stop and saving).
        void WriteOriginals()
        {
            foreach (var (track, value) in _snapshot)
                if (track.target != null) track.Type.Write(track.target, value);
            foreach (var (owner, path, value) in _references)
            {
                if (owner == null) continue;
                var so = new SerializedObject(owner);
                var prop = so.FindProperty(path);
                var made = prop.objectReferenceValue;
                if (made == value) continue;
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (made is Material && !EditorUtility.IsPersistent(made) && !_references.Exists(r => r.value == made))
                    UnityEngine.Object.DestroyImmediate(made);   // the instance the preview made
            }
            // Scrambled text is restored first; character effects live only in the generated mesh, so regenerate it.
            foreach (var (text, value) in _texts)
                if (text != null && text.text != value) text.text = value;
            foreach (var (track, _) in _snapshot)
                if (track.target is TMP_Text text) text.ForceMeshUpdate();
        }

        static void Repaint()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();   // refreshes the Game view and canvases
        }

        // Tells AnimationMode which serialized fields the preview drives, with their original values (the track type lists them).
        static readonly List<(UnityEngine.Object owner, string path)> Fields = new();

        void Register(KeyTween root, KeyTweenTrack track)
        {
            Fields.Clear();
            track.Type.SerializedFields(track.target, Fields);
            foreach (var (owner, propertyPath) in Fields)
            {
                var transform = owner switch { GameObject go => go.transform, Component c => c.transform, _ => null };
                if (transform == null || !transform.IsChildOf(root.transform)) continue;
                AddOne(new SerializedObject(owner), owner, AnimationUtility.CalculateTransformPath(transform, root.transform), propertyPath);
            }
        }

        static void AddOne(SerializedObject so, UnityEngine.Object target, string path, string propertyPath)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null) return;
            if (prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                // e.g. a text's material: a material track makes the text use an instance, the preview puts the original back
                var reference = EditorCurveBinding.PPtrCurve(path, target.GetType(), propertyPath);
                AnimationMode.AddPropertyModification(reference,
                    new PropertyModification { target = target, propertyPath = propertyPath, objectReference = prop.objectReferenceValue }, true);
                return;
            }
            var value = prop.propertyType switch
            {
                SerializedPropertyType.Float => prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SerializedPropertyType.Integer => prop.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SerializedPropertyType.Boolean => prop.boolValue ? "1" : "0",
                SerializedPropertyType.String => prop.stringValue,
                _ => null,
            };
            if (value == null) return;

            var binding = EditorCurveBinding.FloatCurve(path, target.GetType(), propertyPath);
            var modification = new PropertyModification { target = target, propertyPath = propertyPath, value = value };
            AnimationMode.AddPropertyModification(binding, modification, true);
        }
    }
}
