using UnityEditor;
using UnityEngine;

namespace KeyTweening.Editor
{
    /// <summary>The scroll helpers' inspectors: their fields as usual, and every state as the same Edit / Create row.</summary>
    internal abstract class StateFieldsEditor : UnityEditor.Editor
    {
        protected abstract (string field, string ownerField, string newName)[] States { get; }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var states = States;
            var excluded = new string[states.Length + 1];
            excluded[0] = "m_Script";
            for (var i = 0; i < states.Length; i++) excluded[i + 1] = states[i].field;
            DrawPropertiesExcluding(serializedObject, excluded);
            foreach (var (field, ownerField, newName) in states)
            {
                var property = serializedObject.FindProperty(field);
                var owner = serializedObject.FindProperty(ownerField).objectReferenceValue as Component;
                KeyTweenStateField.Draw(property, new GUIContent(property.displayName, property.tooltip), owner, newName);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(KeyTweenPageIndicator))]
    internal sealed class KeyTweenPageIndicatorEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(KeyTweenPageIndicator.onState), nameof(KeyTweenPageIndicator.itemPrefab), "On"),
            (nameof(KeyTweenPageIndicator.offState), nameof(KeyTweenPageIndicator.itemPrefab), "Off"),
        };
    }

    [CustomEditor(typeof(KeyTweenScrollScrub))]
    internal sealed class KeyTweenScrollScrubEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(KeyTweenScrollScrub.state), nameof(KeyTweenScrollScrub.target), "Scroll"),
        };
    }

    [CustomEditor(typeof(KeyTweenPullToRefresh))]
    internal sealed class KeyTweenPullToRefreshEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(KeyTweenPullToRefresh.pullState), nameof(KeyTweenPullToRefresh.target), "Pull"),
            (nameof(KeyTweenPullToRefresh.refreshingState), nameof(KeyTweenPullToRefresh.target), "Refreshing"),
        };
    }
}
