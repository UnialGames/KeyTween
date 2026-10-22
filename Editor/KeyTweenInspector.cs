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
            var raw = new Foldout { text = "Raw Data", value = false };
            InspectorElement.FillDefaultInspector(raw, serializedObject, this);
            root.Add(raw);
            return root;
        }
    }
}
