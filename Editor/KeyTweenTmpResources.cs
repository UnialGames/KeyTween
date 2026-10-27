using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KeyTweening.Editor
{
    /// <summary>
    /// The samples' text needs TMP Essential Resources (TMP Settings, the SDF shaders, the default font). They live in each
    /// project's Assets, so no package dependency can bring them, and without them every label is invisible. When a KeyTween
    /// sample (Package Manager › Samples, imported under Assets/Samples/KeyTween/) lands in a project that has none, import
    /// them right away, as Window › TextMeshPro › Import TMP Essential Resources does.
    /// </summary>
    sealed class KeyTweenTmpResources : AssetPostprocessor
    {
        const string TriedKey = "UnialGames.KeyTween.TmpEssentialsTried";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(p => p.Replace('\\', '/').Contains("/Samples/KeyTween/"))) return;
            if (SessionState.GetBool(TriedKey, false) || HasEssentials()) return;
            SessionState.SetBool(TriedKey, true);   // once per editor session, even if the import fails
            EditorApplication.delayCall += () =>
            {
                if (HasEssentials()) return;
                TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
                Debug.Log("[KeyTween] The sample uses TextMeshPro, so TMP Essential Resources were imported into Assets/TextMesh Pro " +
                          "(the same as Window › TextMeshPro › Import TMP Essential Resources).");
            };
        }

        static bool HasEssentials() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;
    }
}
