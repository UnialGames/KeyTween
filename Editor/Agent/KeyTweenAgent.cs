using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeyTweening.Editor
{
    /// <summary>Which data a write may change, so a person can allow one and not the other.</summary>
    public enum KeyTweenAgentScope
    {
        Scene,    // an object of an open scene: changed in memory (one Undo step, the scene is marked unsaved), never saved
        Prefab,   // a prefab asset: changed and saved, like an edit made in the KeyTween Editor
    }

    // ---------- the document an agent reads, edits and writes back (KEYTWEEN-CONTRACT.md) ----------

    [Serializable]
    public sealed class KeyTweenAgentDocument
    {
        public int contract = KeyTweenAgent.Contract;
        public string target = "";
        public string scope = "";         // read: "scene" or "prefab", the write that changes this target
        public string token = "";         // read: the data as it was read. A write is refused when it changed since
        public bool hasKeyTween;          // read
        public List<KeyTweenAgentObject> objects = new();   // read: what can be animated
        public List<KeyTweenAgentState> states = new();     // write: each is created, or replaces the state of that name
        public string[] deleteStates = Array.Empty<string>();   // write
    }

    [Serializable]
    public sealed class KeyTweenAgentObject
    {
        public string path = "";          // from the target ("" is the target itself)
        public string[] components = Array.Empty<string>();
        public string[] tracks = Array.Empty<string>();   // the track ids this object takes
    }

    [Serializable]
    public sealed class KeyTweenAgentState
    {
        public string name = "";
        public bool loop;
        public string playOn = "None";    // a KeyTweenEvent name: when it plays without code
        public float duration;            // read
        public List<KeyTweenAgentTrack> tracks = new();
        public List<KeyTweenAgentEvent> events = new();
        public string[] keeps = Array.Empty<string>();   // read: parts this contract cannot write; a replace keeps them as they are
    }

    [Serializable]
    public sealed class KeyTweenAgentTrack
    {
        public string @object = "";       // path from the target
        public string track = "";         // a KeyTweenProperty name; "MaterialColor:_BaseColor"; "Custom:Namespace.Type, Assembly"
        public string start = "Current";  // KeyValue, Current, Pointer
        public bool relativeToRest, sizeRelative;
        public float stagger, durationPerChar;
        public string scramble = "None";
        public List<KeyTweenAgentKey> keys = new();
    }

    [Serializable]
    public sealed class KeyTweenAgentKey
    {
        public float time;
        public float[] value = Array.Empty<float>();
        public string arrival = "Ease";   // Ease, Curve, Punch, Shake: how the track gets here from the key before
        public string ease = "OutSine";
        public float frequency = 10f, damping = 1f;
        public List<KeyTweenAgentCurveKey> curve = new();
    }

    [Serializable]
    public sealed class KeyTweenAgentCurveKey
    {
        public float time, value, inTangent, outTangent;
    }

    [Serializable]
    public sealed class KeyTweenAgentEvent
    {
        public float time;
        public string name = "Event";
        public string dataType = "", dataJson = "";
    }

    [Serializable]
    public sealed class KeyTweenAgentResult
    {
        public bool applied;
        public string target = "";
        public string token = "";         // the data after the write (the next write of this target needs it)
        public string[] changes = Array.Empty<string>();
        public string saved = "";
    }

    [Serializable]
    public sealed class KeyTweenAgentSurvey
    {
        public int contract = KeyTweenAgent.Contract;
        public List<KeyTweenAgentFound> targets = new();
    }

    [Serializable]
    public sealed class KeyTweenAgentFound
    {
        public string target = "", scope = "";
        public string[] states = Array.Empty<string>();
    }

    /// <summary>
    /// What an AI agent (or any script) does to KeyTween data: read an object's states as a document, change the document,
    /// write it back. The result is ordinary states, tracks and keys, so a person carries on in the KeyTween Editor.
    /// A write is all or nothing (every error is reported and nothing changes), is refused when the data changed since it
    /// was read (the token), and is one Undo step. The commands that reach this are in Agent/Cli.
    /// </summary>
    public static class KeyTweenAgent
    {
        public const int Contract = 1;
        const string NoKeyTween = "none";
        const string UndoName = "KeyTween Agent Edit";

        // ---------- read ----------

        /// <summary>
        /// The contract document (KEYTWEEN-CONTRACT.md): the format and the rules an agent follows, from the installed package.
        /// For agents that cannot open the package's files (a "Packages/…" path is resolved by Unity only).
        /// </summary>
        public static string ContractText()
        {
            var path = KeyTweenAgentSetup.ContractPath();
            if (path == null) throw new InvalidOperationException("KEYTWEEN-CONTRACT.md was not found in the KeyTween package.");
            return System.IO.File.ReadAllText(System.IO.Path.GetFullPath(path));
        }

        /// <summary>The KeyTweens of the open scenes and of the prefab open in Prefab Mode: where an agent can start.</summary>
        public static KeyTweenAgentSurvey Survey()
        {
            var survey = new KeyTweenAgentSurvey();
            if (PrefabStageUtility.GetCurrentPrefabStage() is { } stage && stage.prefabContentsRoot != null)
                foreach (var anim in stage.prefabContentsRoot.GetComponentsInChildren<KeyTween>(true))
                    survey.targets.Add(Found(anim, PrefabTarget(stage.assetPath, PathFrom(stage.prefabContentsRoot.transform, anim.transform)), "prefab"));
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var anim in root.GetComponentsInChildren<KeyTween>(true))
                    survey.targets.Add(Found(anim, SceneTarget(anim.transform), "scene"));
            }
            return survey;
        }

        static KeyTweenAgentFound Found(KeyTween anim, string target, string scope) =>
            new() { target = target, scope = scope, states = anim.states.Select(s => s.name).ToArray() };

        /// <summary>
        /// The states of <paramref name="target"/> ("Assets/UI/Button.prefab", "Assets/UI/Popup.prefab:Panel/Ok" for an object
        /// inside it, "/Canvas/Button" for an object of an open scene) and, with <paramref name="objects"/>, what can be animated.
        /// </summary>
        public static KeyTweenAgentDocument Read(string target, bool objects = true)
        {
            var at = Resolve(target);
            var anim = at.go.GetComponent<KeyTween>();
            var doc = new KeyTweenAgentDocument { target = at.canonical, scope = ScopeName(at.scope), token = TokenOf(at.go), hasKeyTween = anim != null };
            if (objects)
                foreach (var t in at.go.GetComponentsInChildren<Transform>(true))
                    doc.objects.Add(new KeyTweenAgentObject
                    {
                        path = PathFrom(at.go.transform, t),
                        components = t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray(),
                        tracks = Tracks(t),
                    });
            if (anim == null) return doc;
            var trigger = at.go.GetComponent<KeyTweenTrigger>();
            foreach (var state in anim.states) doc.states.Add(ToDocument(anim, state, trigger));
            return doc;
        }

        static KeyTweenAgentState ToDocument(KeyTween anim, KeyTweenState state, KeyTweenTrigger trigger)
        {
            var binding = trigger != null ? trigger.bindings.Find(b => b.state == state.name) : null;
            var doc = new KeyTweenAgentState { name = state.name, loop = state.loop, duration = state.Duration, playOn = binding != null ? binding.evt.ToString() : "None" };
            var kept = 0;
            foreach (var track in state.tracks)
            {
                if (!Writable(track, anim.transform))
                {
                    kept++;
                    continue;
                }
                var dimensions = track.Type.Dimensions;
                var t = new KeyTweenAgentTrack
                {
                    @object = PathFrom(anim.transform, track.target.transform), track = TrackId(track), start = track.start.ToString(),
                    relativeToRest = track.relativeToRest, sizeRelative = track.sizeRelative, stagger = track.stagger,
                    durationPerChar = track.durationPerChar, scramble = track.scramble.ToString(),
                };
                foreach (var key in track.keys)
                {
                    var value = new float[dimensions];
                    for (var i = 0; i < dimensions; i++) value[i] = key.value[i];
                    var k = new KeyTweenAgentKey { time = key.time, value = value, arrival = key.curveType.ToString(), ease = key.ease.ToString(), frequency = key.frequency, damping = key.damping };
                    if (key.curveType == KeyTweenCurveType.Curve && key.curve != null)
                        foreach (var c in key.curve.keys) k.curve.Add(new KeyTweenAgentCurveKey { time = c.time, value = c.value, inTangent = c.inTangent, outTangent = c.outTangent });
                    t.keys.Add(k);
                }
                doc.tracks.Add(t);
            }
            foreach (var track in state.eventTracks)
            foreach (var e in track.events)
                doc.events.Add(new KeyTweenAgentEvent { time = e.time, name = e.name, dataType = e.dataType, dataJson = e.dataJson });

            var keeps = new List<string>();
            if (kept > 0) keeps.Add($"{kept} track(s) whose object is missing or outside the target");
            var sounds = state.soundTracks.Sum(s => s.sounds.Count);
            if (sounds > 0) keeps.Add($"{sounds} sound(s)");
            if (state.childCalls.Count > 0) keeps.Add($"{state.childCalls.Count} child call(s)");
            var callbacks = state.onStart.GetPersistentEventCount() + state.onComplete.GetPersistentEventCount() + state.onCancel.GetPersistentEventCount();
            if (callbacks > 0) keeps.Add($"{callbacks} callback(s)");
            doc.keeps = keeps.ToArray();
            return doc;
        }

        // The tracks an object takes. A UI object takes CanvasGroupAlpha with or without a CanvasGroup: a write adds the one it lacks.
        static string[] Tracks(Transform t)
        {
            var ids = KeyTweenPropertyPicker.Properties(t).Select(p => TrackId(p.type));
            if (t is RectTransform) ids = ids.Append(nameof(KeyTweenProperty.CanvasGroupAlpha));
            return ids.Distinct().ToArray();
        }

        // A track this contract can write: its object is the target or under it (named by its path from there).
        static bool Writable(KeyTweenTrack track, Transform root) =>
            track.target != null && (track.target.transform == root || track.target.transform.IsChildOf(root));

        // ---------- write ----------

        /// <summary>
        /// Applies a document (JSON, as <see cref="Read"/> returns it): each state in it is created or replaces the state of
        /// that name, and those in deleteStates go. Throws with every problem found, having changed nothing.
        /// </summary>
        public static KeyTweenAgentResult Write(string json, KeyTweenAgentScope scope, bool dryRun = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("KeyTween data is not written in Play mode: exit Play mode first.");
            KeyTweenAgentDocument doc;
            try { doc = JsonUtility.FromJson<KeyTweenAgentDocument>(json); }
            catch (ArgumentException e) { throw new ArgumentException("The content is not valid JSON: " + e.Message); }
            if (doc == null) throw new ArgumentException("The content is empty.");
            if (doc.contract != Contract) throw new ArgumentException($"\"contract\" must be {Contract} (this KeyTween's version of the format), got {doc.contract}.");

            var at = Resolve(doc.target);
            if (at.scope != scope)
                throw new ArgumentException($"'{at.canonical}' is a {ScopeName(at.scope)} target: use keytween_write_{ScopeName(at.scope)}.");
            if (doc.token != TokenOf(at.go))
                throw new InvalidOperationException($"Stale token: '{at.canonical}' changed since it was read (a person edited it, or an earlier write). Read it again and write from that; do not edit the token.");

            var errors = new List<string>();
            var plan = Plan(doc, at.go, errors);
            if (errors.Count > 0) throw new ArgumentException($"Nothing was changed. {errors.Count} problem(s):\n- " + string.Join("\n- ", errors));

            var result = new KeyTweenAgentResult { target = at.canonical, token = doc.token, changes = plan.changes.ToArray() };
            if (dryRun)
            {
                result.saved = "dry run: nothing was changed";
                return result;
            }

            // A prefab asset takes no new component in place: add what is missing through its contents, then plan against the reloaded objects.
            var needsTrigger = plan.bindings.Exists(b => b.evt != null) && at.go.GetComponent<KeyTweenTrigger>() == null;
            if (EditorUtility.IsPersistent(at.go) && (at.go.GetComponent<KeyTween>() == null || needsTrigger || plan.groups.Count > 0))
            {
                AddToPrefabAsset(at, needsTrigger, plan.groups.Select(g => PathFrom(at.go.transform, g.on)).Distinct().ToArray());
                at = Resolve(doc.target);
                plan = Plan(doc, at.go, errors);
                if (errors.Count > 0) throw new InvalidOperationException("The prefab changed while its components were added:\n- " + string.Join("\n- ", errors));
            }

            Apply(plan, at.go);
            result.applied = true;
            result.token = TokenOf(at.go);
            result.saved = Save(at);
            KeyTweenEditorWindow.Reload();
            return result;
        }

        sealed class WritePlan
        {
            public readonly List<KeyTweenState> upserts = new();
            public readonly List<string> deletes = new();
            public readonly List<(string state, KeyTweenEvent? evt)> bindings = new();   // null = no Play On
            public readonly List<(KeyTweenTrack track, Transform on)> groups = new();    // CanvasGroupAlpha tracks whose object has no CanvasGroup yet
            public readonly List<string> changes = new();
        }

        // Everything the document asks for, checked and built without touching the data.
        static WritePlan Plan(KeyTweenAgentDocument doc, GameObject go, List<string> errors)
        {
            var plan = new WritePlan();
            var anim = go.GetComponent<KeyTween>();
            if (anim == null) plan.changes.Add("add a KeyTween component");
            var names = new HashSet<string>();
            foreach (var s in doc.states ?? new List<KeyTweenAgentState>())
            {
                if (string.IsNullOrWhiteSpace(s.name))
                {
                    errors.Add("A state has no name.");
                    continue;
                }
                if (!names.Add(s.name))
                {
                    errors.Add($"State '{s.name}' is in the document twice.");
                    continue;
                }
                var old = anim != null ? anim.states.Find(x => x.name == s.name) : null;
                var state = Build(s, old, go.transform, plan, errors);
                plan.upserts.Add(state);
                KeyTweenEvent? evt = null;
                if (!string.IsNullOrEmpty(s.playOn) && s.playOn != "None")
                {
                    if (Name<KeyTweenEvent>(s.playOn, out var parsed)) evt = parsed;
                    else errors.Add($"State '{s.name}': playOn '{s.playOn}' is not one of None, {Names<KeyTweenEvent>()}.");
                }
                plan.bindings.Add((s.name, evt));
                plan.changes.Add($"{(old != null ? "replace" : "create")} state '{s.name}' ({state.tracks.Count} track(s), {state.Duration:0.###} s{(evt != null ? $", Play On {evt}" : "")})");
            }
            foreach (var name in doc.deleteStates ?? Array.Empty<string>())
            {
                if (names.Contains(name)) errors.Add($"State '{name}' is both written and deleted.");
                else if (anim == null || !anim.states.Exists(x => x.name == name)) errors.Add($"deleteStates: there is no state '{name}'.");
                else
                {
                    plan.deletes.Add(name);
                    plan.bindings.Add((name, null));
                    plan.changes.Add($"delete state '{name}'");
                }
            }
            if (plan.upserts.Count == 0 && plan.deletes.Count == 0 && errors.Count == 0) errors.Add("The document has no states and no deleteStates: there is nothing to write.");
            return plan;
        }

        static KeyTweenState Build(KeyTweenAgentState s, KeyTweenState old, Transform root, WritePlan plan, List<string> errors)
        {
            var state = new KeyTweenState { name = s.name, loop = s.loop };
            var seen = new HashSet<(Transform, string)>();
            foreach (var t in s.tracks ?? new List<KeyTweenAgentTrack>())
            {
                var where = $"State '{s.name}', track '{t.track}' on '{t.@object}'";
                var target = string.IsNullOrEmpty(t.@object) ? root : root.Find(t.@object);
                if (target == null)
                {
                    errors.Add($"{where}: there is no object at that path (paths are from the target, \"\" is the target itself; see \"objects\" in a read).");
                    continue;
                }
                var type = TrackType(t.track);
                if (type == null)
                {
                    errors.Add($"{where}: unknown track. This object takes: {string.Join(", ", Tracks(target))}.");
                    continue;
                }
                var component = target.GetComponent(type.ComponentType);
                // The one component a write adds for a track: fading a UI object with its children needs a CanvasGroup on it.
                var addGroup = component == null && type.Property == KeyTweenProperty.CanvasGroupAlpha && target is RectTransform;
                if (!addGroup && (component == null || !type.Offers(component)))
                {
                    errors.Add($"{where}: the object does not take this track (it needs a {type.ComponentType.Name}). It takes: {string.Join(", ", Tracks(target))}.");
                    continue;
                }
                if (!seen.Add((target, t.track)))
                {
                    errors.Add($"{where}: the state has this track twice.");
                    continue;
                }

                var track = new KeyTweenTrack { target = component, relativeToRest = t.relativeToRest, sizeRelative = t.sizeRelative, stagger = t.stagger, durationPerChar = t.durationPerChar };
                KeyTweenTrackTypes.Assign(track, type);
                if (addGroup)
                {
                    if (!plan.groups.Exists(g => g.on == target)) plan.changes.Add($"add a CanvasGroup to {(target == root ? "the target" : $"'{t.@object}'")}");
                    plan.groups.Add((track, target));
                }
                if (Name<KeyTweenStart>(Or(t.start, "Current"), out var start)) track.start = start;
                else errors.Add($"{where}: start '{t.start}' is not one of {Names<KeyTweenStart>()}.");
                if (track.start == KeyTweenStart.Pointer && !type.PointerStart) errors.Add($"{where}: start Pointer is for AnchoredPosition only.");
                if (t.sizeRelative && !type.SizeRelative) errors.Add($"{where}: sizeRelative is for AnchoredPosition and SizeDelta only.");
                if (Name<KeyTweenScramble>(Or(t.scramble, "None"), out var scramble)) track.scramble = scramble;
                else errors.Add($"{where}: scramble '{t.scramble}' is not one of {Names<KeyTweenScramble>()}.");
                if (track.scramble != KeyTweenScramble.None && track.property != KeyTweenProperty.TextReveal) errors.Add($"{where}: scramble is for TextReveal only.");

                if (t.keys == null || t.keys.Count == 0) errors.Add($"{where}: a track needs at least one key.");
                foreach (var k in (t.keys ?? new List<KeyTweenAgentKey>()).OrderBy(k => k.time))   // kept in time order, as the editor keeps them
                {
                    var at = $"{where}, key at {k.time} s";
                    var key = new KeyTweenKey { time = k.time, frequency = k.frequency, damping = k.damping };
                    if (k.time < 0f) errors.Add($"{at}: time is below 0.");
                    if (k.value == null || k.value.Length != type.Dimensions)
                        errors.Add($"{at}: value needs {type.Dimensions} number(s) for this track ({type.Kind}), got {k.value?.Length ?? 0}.");
                    else
                        for (var i = 0; i < k.value.Length; i++) key.value[i] = k.value[i];
                    if (Name<KeyTweenCurveType>(Or(k.arrival, "Ease"), out var arrival)) key.curveType = arrival;
                    else errors.Add($"{at}: arrival '{k.arrival}' is not one of {Names<KeyTweenCurveType>()}.");
                    if (Name<KeyTweenEase>(Or(k.ease, "OutSine"), out var ease)) key.ease = ease;
                    else errors.Add($"{at}: ease '{k.ease}' is not one of {Names<KeyTweenEase>()}.");
                    if (key.curveType == KeyTweenCurveType.Curve)
                    {
                        if (k.curve == null || k.curve.Count < 2) errors.Add($"{at}: arrival Curve needs \"curve\" with at least two points (time and value from 0 to 1).");
                        else key.curve = new AnimationCurve(k.curve.Select(c => new Keyframe(c.time, c.value, c.inTangent, c.outTangent)).ToArray());
                    }
                    track.keys.Add(key);
                }
                state.tracks.Add(track);
            }

            if (s.events != null && s.events.Count > 0)
                state.eventTracks.Add(new KeyTweenEventTrack
                {
                    events = s.events.OrderBy(e => e.time).Select(e => new KeyTweenEventMarker { time = e.time, name = Or(e.name, "Event"), dataType = e.dataType ?? "", dataJson = e.dataJson ?? "" }).ToList(),
                });

            // What the document cannot say stays as it is in the state it replaces.
            if (old != null)
            {
                foreach (var track in old.tracks)
                    if (!Writable(track, root)) state.tracks.Add(track);
                state.soundTracks = old.soundTracks;
                state.childCalls = old.childCalls;
                (state.onStart, state.onComplete, state.onCancel) = (old.onStart, old.onComplete, old.onCancel);
            }
            return state;
        }

        static void Apply(WritePlan plan, GameObject go)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            var group = Undo.GetCurrentGroup();

            foreach (var (track, on) in plan.groups)
                track.target = on.TryGetComponent<CanvasGroup>(out var has) ? has : Undo.AddComponent<CanvasGroup>(on.gameObject);

            var anim = go.GetComponent<KeyTween>();
            if (anim == null) anim = Undo.AddComponent<KeyTween>(go);
            Undo.RecordObject(anim, UndoName);
            foreach (var state in plan.upserts)
            {
                var i = anim.states.FindIndex(x => x.name == state.name);
                if (i >= 0) anim.states[i] = state;
                else anim.states.Add(state);
            }
            anim.states.RemoveAll(x => plan.deletes.Contains(x.name));
            PrefabUtility.RecordPrefabInstancePropertyModifications(anim);
            EditorUtility.SetDirty(anim);

            // Play On: one binding per state, as the window keeps it.
            var trigger = go.GetComponent<KeyTweenTrigger>();
            foreach (var (state, evt) in plan.bindings)
            {
                if (trigger == null)
                {
                    if (evt == null) continue;
                    trigger = Undo.AddComponent<KeyTweenTrigger>(go);
                }
                Undo.RecordObject(trigger, UndoName);
                trigger.bindings.RemoveAll(b => b.state == state);
                if (evt != null) trigger.bindings.Add(new KeyTweenBinding { evt = evt.Value, state = state });
            }
            if (trigger != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
                EditorUtility.SetDirty(trigger);
            }
            Undo.CollapseUndoOperations(group);
        }

        static void AddToPrefabAsset(Target at, bool trigger, string[] groups)
        {
            using var contents = new PrefabUtility.EditPrefabContentsScope(at.assetPath);
            var into = string.IsNullOrEmpty(at.inner) ? contents.prefabContentsRoot.transform : contents.prefabContentsRoot.transform.Find(at.inner);
            if (into.GetComponent<KeyTween>() == null) into.gameObject.AddComponent<KeyTween>();
            if (trigger && into.GetComponent<KeyTweenTrigger>() == null) into.gameObject.AddComponent<KeyTweenTrigger>();
            foreach (var path in groups)
            {
                var on = path.Length == 0 ? into : into.Find(path);
                if (on.GetComponent<CanvasGroup>() == null) on.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Where the change is now: a scene is left unsaved, a prefab is saved as the KeyTween Editor saves it.
        static string Save(Target at)
        {
            var anim = at.go.GetComponent<KeyTween>();
            if (at.scope == KeyTweenAgentScope.Scene)
            {
                EditorSceneManager.MarkSceneDirty(at.go.scene);
                return $"not saved: scene '{at.go.scene.name}' is marked unsaved (Ctrl+S saves it, Ctrl+Z undoes this write)";
            }
            if (!EditorUtility.IsPersistent(at.go))
            {
                KeyTweenSave.Changed(new[] { anim });   // open in Prefab Mode: the window's Auto Save / Save writes it
                return $"changed in Prefab Mode: {at.assetPath} is saved by the KeyTween Editor's Auto Save, or its Save button (Ctrl+Z undoes this write)";
            }
            AssetDatabase.SaveAssetIfDirty(anim);
            if (at.go.TryGetComponent<KeyTweenTrigger>(out var trigger)) AssetDatabase.SaveAssetIfDirty(trigger);
            return $"saved to {at.assetPath} (Ctrl+Z puts the data back in the editor; save again to write that)";
        }

        // ---------- targets, tokens, names ----------

        struct Target
        {
            public GameObject go;
            public KeyTweenAgentScope scope;
            public string canonical, assetPath, inner;
        }

        static Target Resolve(string target)
        {
            target = (target ?? "").Trim().Replace('\\', '/');
            if (target.Length == 0) throw new ArgumentException("No target: give a prefab asset path (Assets/UI/Button.prefab) or an object of an open scene (/Canvas/Button).");
            var split = target.IndexOf(".prefab", StringComparison.OrdinalIgnoreCase);
            if (split >= 0)
            {
                var assetPath = target.Substring(0, split + ".prefab".Length);
                var inner = target.Substring(assetPath.Length).TrimStart(':', '/');
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null) throw new ArgumentException($"There is no prefab at '{assetPath}' (the path is from the project folder, e.g. Assets/UI/Button.prefab).");
                // Open in Prefab Mode: the objects being edited there, so the person sees the change and can undo it.
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                var root = stage != null && stage.assetPath == assetPath && stage.prefabContentsRoot != null ? stage.prefabContentsRoot.transform : asset.transform;
                var t = inner.Length == 0 ? root : root.Find(inner);
                if (t == null) throw new ArgumentException($"Prefab '{assetPath}' has no object at '{inner}' (the path is from the prefab's root, without the root's name).");
                return new Target { go = t.gameObject, scope = KeyTweenAgentScope.Prefab, canonical = PrefabTarget(assetPath, inner), assetPath = assetPath, inner = inner };
            }

            var path = target.TrimStart('/');
            var slash = path.IndexOf('/');
            var (first, rest) = slash < 0 ? (path, "") : (path.Substring(0, slash), path.Substring(slash + 1));
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != first) continue;
                    var t = rest.Length == 0 ? root.transform : root.transform.Find(rest);
                    if (t != null) return new Target { go = t.gameObject, scope = KeyTweenAgentScope.Scene, canonical = SceneTarget(t) };
                }
            }
            throw new ArgumentException($"No object '{target}' in the open scenes. Open its scene first (a scene that is not open is not loaded for you), or give a prefab asset path ending in .prefab.");
        }

        static string PrefabTarget(string assetPath, string inner) => string.IsNullOrEmpty(inner) ? assetPath : assetPath + ":" + inner;

        static string SceneTarget(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return "/" + path;
        }

        static string PathFrom(Transform root, Transform t)
        {
            var path = "";
            for (; t != null && t != root; t = t.parent) path = path.Length == 0 ? t.name : t.name + "/" + path;
            return path;
        }

        static string ScopeName(KeyTweenAgentScope scope) => scope == KeyTweenAgentScope.Scene ? "scene" : "prefab";

        // The serialized data of the animation and its Play On bindings: any edit, by anyone, changes it.
        static string TokenOf(GameObject go)
        {
            if (!go.TryGetComponent<KeyTween>(out var anim)) return NoKeyTween;
            var text = EditorJsonUtility.ToJson(anim);
            if (go.TryGetComponent<KeyTweenTrigger>(out var trigger)) text += EditorJsonUtility.ToJson(trigger);
            return Hash128.Compute(text).ToString().Substring(0, 16);
        }

        static string TrackId(KeyTweenTrackType type) =>
            type.Property == KeyTweenProperty.Custom ? "Custom:" + KeyTweenTrackTypes.TypeKey(type.GetType())
            : KeyTweenBuiltInTracks.IsMaterial(type.Property) ? type.Property + ":" + type.ShaderProperty
            : type.Property.ToString();

        static string TrackId(KeyTweenTrack track) =>
            track.property == KeyTweenProperty.Custom ? "Custom:" + track.typeName
            : KeyTweenBuiltInTracks.IsMaterial(track.property) ? track.property + ":" + track.shaderProperty
            : track.property.ToString();

        // null: no such track (a name that is not a KeyTweenProperty, a custom class that is gone, a material track without a property).
        static KeyTweenTrackType TrackType(string id)
        {
            id ??= "";
            var colon = id.IndexOf(':');
            var (name, key) = colon < 0 ? (id, "") : (id.Substring(0, colon), id.Substring(colon + 1));
            if (!Name<KeyTweenProperty>(name, out var property)) return null;
            var type = property == KeyTweenProperty.Custom ? KeyTweenTrackTypes.OfCustom(key)
                : KeyTweenBuiltInTracks.IsMaterial(property) ? KeyTweenTrackTypes.OfMaterial(property, key)
                : KeyTweenTrackTypes.Of(property);
            return type is KeyTweenBuiltInTracks.Missing ? null : type;
        }

        // An enum member by its name only (Enum.TryParse also takes numbers).
        static bool Name<T>(string name, out T value) where T : struct, Enum
        {
            value = default;
            return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && name[0] != '-' && Enum.TryParse(name, out value) && Enum.IsDefined(typeof(T), value);
        }

        static string Names<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames(typeof(T)));

        static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
