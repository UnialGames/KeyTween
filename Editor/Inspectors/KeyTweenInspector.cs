using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KeyTweening.Editor
{
    [CustomEditor(typeof(KeyTween))]
    internal sealed class KeyTweenInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Button(() => KeyTweenEditorWindow.Open((KeyTween)target))
            {
                text = "Open KeyTween Editor",
                style = { height = 26, marginBottom = 6 },
            });
            // Like Animator's Update Mode: whether pausing the game (Time.timeScale 0) pauses these animations.
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(KeyTween.timeMode)), "Time") { style = { marginBottom = 6 } });

            // The saved data, to look at only: editing states and tracks here bypasses the KeyTween Editor (undo, preview,
            // overrides), so it is drawn disabled. IMGUI, because a disabled IMGUI foldout still opens (a disabled UI Toolkit
            // one does not), so the lists can be browsed.
            var raw = new Foldout { text = "Raw Data (read-only)", value = false };
            raw.Add(new IMGUIContainer(() =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                using (new EditorGUI.DisabledScope(true))
                {
                    var property = serializedObject.GetIterator();
                    for (var enter = true; property.NextVisible(enter); enter = false)
                        if (property.name != nameof(KeyTween.timeMode)) EditorGUILayout.PropertyField(property, true);
                }
            }));
            root.Add(raw);
            return root;
        }
    }
}
