using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Inspector laid out like Unity's Vertical / Horizontal / Grid Layout Group (same fields, same order), so designers
    /// set a scroll list up the way they already set up layout groups. Item states are picked from the item prefab,
    /// and Edit / Create opens that state in the KeyTween Editor. Preview lays out items in edit mode.
    /// </summary>
    [CustomEditor(typeof(KeyTweenScrollList))]
    internal sealed class KeyTweenScrollListEditor : UnityEditor.Editor
    {
        SerializedProperty _itemPrefab, _extraPrefabs, _measureItems, _layout, _padding, _spacing, _cellSize, _gridSpacing, _childAlignment,
            _reverse, _controlChildSize, _constraint, _constraintCount, _loop, _snap, _snapAlignment, _snapDuration, _autoScroll,
            _stickToEnd, _stickyHeader, _entryState, _entryStagger, _playEntryOnFill, _appearState, _scrubState;

        int _previewCount = 20;
        float _entryStart = -1f;

        KeyTweenScrollList List => (KeyTweenScrollList)target;

        void OnEnable()
        {
            SerializedProperty P(string name) => serializedObject.FindProperty(name);
            _itemPrefab = P(nameof(KeyTweenScrollList.itemPrefab));
            _extraPrefabs = P(nameof(KeyTweenScrollList.extraPrefabs));
            _measureItems = P(nameof(KeyTweenScrollList.measureItems));
            _layout = P(nameof(KeyTweenScrollList.layout));
            _padding = P(nameof(KeyTweenScrollList.padding));
            _spacing = P(nameof(KeyTweenScrollList.spacing));
            _cellSize = P(nameof(KeyTweenScrollList.cellSize));
            _gridSpacing = P(nameof(KeyTweenScrollList.gridSpacing));
            _childAlignment = P(nameof(KeyTweenScrollList.childAlignment));
            _reverse = P(nameof(KeyTweenScrollList.reverseArrangement));
            _controlChildSize = P(nameof(KeyTweenScrollList.controlChildSize));
            _constraint = P(nameof(KeyTweenScrollList.constraint));
            _constraintCount = P(nameof(KeyTweenScrollList.constraintCount));
            _loop = P(nameof(KeyTweenScrollList.loop));
            _snap = P(nameof(KeyTweenScrollList.snap));
            _snapAlignment = P(nameof(KeyTweenScrollList.snapAlignment));
            _snapDuration = P(nameof(KeyTweenScrollList.snapDuration));
            _autoScroll = P(nameof(KeyTweenScrollList.autoScrollInterval));
            _stickToEnd = P(nameof(KeyTweenScrollList.stickToEnd));
            _stickyHeader = P(nameof(KeyTweenScrollList.stickyHeaderPrefab));
            _entryState = P(nameof(KeyTweenScrollList.entryState));
            _entryStagger = P(nameof(KeyTweenScrollList.entryStagger));
            _playEntryOnFill = P(nameof(KeyTweenScrollList.playEntryOnFill));
            _appearState = P(nameof(KeyTweenScrollList.appearState));
            _scrubState = P(nameof(KeyTweenScrollList.scrubState));
        }

        void OnDisable()
        {
            EditorApplication.update -= TickEntry;
            if (target != null && List.Previewing) List.EndPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_itemPrefab);
            EditorGUILayout.PropertyField(_extraPrefabs);
            EditorGUILayout.PropertyField(_measureItems);
            EditorGUILayout.PropertyField(_layout);
            EditorGUILayout.Space();

            // The same rows as Unity's layout group inspectors.
            EditorGUILayout.PropertyField(_padding, true);
            var layout = (KeyTweenScrollList.LayoutType)_layout.enumValueIndex;
            var linear = layout is KeyTweenScrollList.LayoutType.Vertical or KeyTweenScrollList.LayoutType.Horizontal;
            if (!linear)
            {
                EditorGUILayout.PropertyField(_cellSize, new GUIContent(layout == KeyTweenScrollList.LayoutType.Masonry ? "Column Size" : "Cell Size", _cellSize.tooltip));
                EditorGUILayout.PropertyField(_gridSpacing, new GUIContent("Spacing"));
                EditorGUILayout.PropertyField(_childAlignment);
                EditorGUILayout.PropertyField(_constraint);
                if (_constraint.enumValueIndex != (int)KeyTweenScrollList.GridConstraint.Flexible)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_constraintCount);
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(_spacing);
                EditorGUILayout.PropertyField(_childAlignment);
                EditorGUILayout.PropertyField(_reverse);
                // "Control Child Size  ☐ Width" like the layout group, with the one axis a scroll list can stretch.
                var rect = EditorGUILayout.GetControlRect();
                rect = EditorGUI.PrefixLabel(rect, new GUIContent("Control Child Size", _controlChildSize.tooltip));
                _controlChildSize.boolValue = EditorGUI.ToggleLeft(rect,
                    layout == KeyTweenScrollList.LayoutType.Vertical ? "Width" : "Height", _controlChildSize.boolValue);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scrolling", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!linear))
                EditorGUILayout.PropertyField(_loop, new GUIContent("Loop", linear ? _loop.tooltip : "Vertical / Horizontal only"));
            EditorGUILayout.PropertyField(_snap);
            using (new EditorGUI.DisabledScope(_snap.enumValueIndex == 0))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_snapAlignment, new GUIContent("Alignment", "Where the snapped item sits (also the Current Index and Auto Scroll place)"));
                EditorGUILayout.PropertyField(_snapDuration, new GUIContent("Duration (s)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_autoScroll, new GUIContent("Auto Scroll (s)", _autoScroll.tooltip));
            EditorGUILayout.PropertyField(_stickToEnd);
            using (new EditorGUI.DisabledScope(!linear))
                PrefabPopup(_stickyHeader, new GUIContent("Sticky Header", linear ? _stickyHeader.tooltip : "Vertical / Horizontal only"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            var item = _itemPrefab.objectReferenceValue as RectTransform;
            StateRow(_entryState, "Entry State", item, "Show");
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_entryState.stringValue)))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_entryStagger, new GUIContent("Stagger (s)", _entryStagger.tooltip));
                EditorGUILayout.PropertyField(_playEntryOnFill, new GUIContent("Play On Fill", _playEntryOnFill.tooltip));
                EditorGUI.indentLevel--;
            }
            StateRow(_appearState, "Appear State", item, "Appear");
            StateRow(_scrubState, "Scrub State", item, "Scrub");

            if (_itemPrefab.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Assign the item prefab. Fill the list from code: list.SetCount(count, (View v, int i) => ...).", MessageType.Info);
            serializedObject.ApplyModifiedProperties();

            PreviewGUI();
        }

        // ---------- states: pick from the item prefab, Edit / Create in the KeyTween Editor ----------

        static void StateRow(SerializedProperty property, string label, Component item, string newName) =>
            KeyTweenStateField.Draw(property, new GUIContent(label, property.tooltip), item, newName);

        void PrefabPopup(SerializedProperty property, GUIContent label)
        {
            var options = new List<GUIContent> { new("None") };
            var main = _itemPrefab.objectReferenceValue;
            options.Add(new GUIContent("0: " + (main != null ? main.name : "Item Prefab")));
            for (var i = 0; i < _extraPrefabs.arraySize; i++)
            {
                var extra = _extraPrefabs.GetArrayElementAtIndex(i).objectReferenceValue;
                options.Add(new GUIContent($"{i + 1}: " + (extra != null ? extra.name : "(empty)")));
            }
            var index = Mathf.Clamp(property.intValue + 1, 0, options.Count - 1);
            var picked = EditorGUILayout.Popup(label, index, options.ToArray());
            if (picked != index) property.intValue = picked - 1;
        }

        // ---------- edit-mode preview: temporary items (not saved), a scroll slider and the entry ----------

        void PreviewGUI()
        {
            if (Application.isPlaying || _itemPrefab.objectReferenceValue == null || serializedObject.isEditingMultipleObjects) return;
            var list = List;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _previewCount = EditorGUILayout.IntSlider(new GUIContent("Items", "Temporary items laid out in edit mode (not saved)"), _previewCount, 1, 200);
                if (GUILayout.Button(list.Previewing ? "Update" : "Show", EditorStyles.miniButton, GUILayout.Width(52)))
                {
                    list.EndPreview();
                    list.BeginPreview(_previewCount);
                }
                using (new EditorGUI.DisabledScope(!list.Previewing))
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52)))
                        StopPreview();
            }
            using (new EditorGUI.DisabledScope(!list.Previewing))
            {
                var offset = EditorGUILayout.Slider(new GUIContent("Scroll", "0 = start, 1 = end"), list.Previewing ? list.NormalizedOffset : 0f, 0f, 1f);
                if (list.Previewing && !Mathf.Approximately(offset, list.NormalizedOffset))
                {
                    list.NormalizedOffset = offset;
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(list.entryState)))
                    if (GUILayout.Button(new GUIContent("Play Entry", "Plays the entry state on the preview items")))
                    {
                        _entryStart = (float)EditorApplication.timeSinceStartup;
                        EditorApplication.update -= TickEntry;
                        EditorApplication.update += TickEntry;
                    }
            }
        }

        void TickEntry()
        {
            if (target == null || !List.Previewing)
            {
                EditorApplication.update -= TickEntry;
                return;
            }
            var elapsed = (float)EditorApplication.timeSinceStartup - _entryStart;
            List.SampleEntry(elapsed);
            SceneView.RepaintAll();
            if (elapsed > 10f) EditorApplication.update -= TickEntry;
        }

        void StopPreview()
        {
            EditorApplication.update -= TickEntry;
            List.EndPreview();
            SceneView.RepaintAll();
        }
    }
}
