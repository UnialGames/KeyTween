using System.IO;
using UnityEditor;
using UnityEngine;

namespace KeyTweening.Editor
{
    /// <summary>
    /// Tools > KeyTween > Set Up Agent Access: tells coding agents (Claude Code, Cursor, Codex, Copilot …) how to work with
    /// KeyTween in this project, by adding a short section to AGENTS.md and CLAUDE.md in the project folder that points at
    /// the contract. The section is replaced, not repeated, when the menu is used again.
    /// </summary>
    static class KeyTweenAgentSetup
    {
        internal const string Start = "<!-- keytween-agent:start -->", End = "<!-- keytween-agent:end -->";
        const string PipelinePackage = "Packages/com.unity.pipeline";

        [MenuItem("Tools/KeyTween/Set Up Agent Access")]
        static void SetUp()
        {
            var contract = ContractPath();
            if (contract == null)
            {
                EditorUtility.DisplayDialog("KeyTween", "KEYTWEEN-CONTRACT.md was not found in the KeyTween package.", "OK");
                return;
            }
            var folder = Directory.GetParent(Application.dataPath).FullName;
            foreach (var name in new[] { "AGENTS.md", "CLAUDE.md" })
            {
                var path = Path.Combine(folder, name);
                File.WriteAllText(path, Upsert(File.Exists(path) ? File.ReadAllText(path) : "", Section(ContractFile(contract))));
            }
            var cli = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PipelinePackage) != null;
            EditorUtility.DisplayDialog("KeyTween",
                "AGENTS.md and CLAUDE.md in the project folder now tell coding agents how to create and edit KeyTween animations.\n\n" +
                (cli
                    ? "The commands (keytween_read, keytween_write_scene, keytween_write_prefab) are available through the Unity CLI: `unity command keytween_read`."
                    : "The commands need the Unity CLI package: run `unity pipeline install` in the project folder (Unity 6), then reopen the project."),
                "OK");
        }

        internal static string ContractPath()
        {
            foreach (var guid in AssetDatabase.FindAssets("KEYTWEEN-CONTRACT"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("KEYTWEEN-CONTRACT.md")) return path;
            }
            return null;
        }

        /// <summary>
        /// The contract as a file an agent outside Unity can open: from the project folder when it is inside it (Assets/…, or
        /// Library/PackageCache/… for a package installed from Git), else absolute. An asset path "Packages/&lt;name&gt;/…" is not
        /// on disk: only Unity resolves it.
        /// </summary>
        internal static string ContractFile(string assetPath)
        {
            var full = Path.GetFullPath(assetPath).Replace('\\', '/');   // in the editor, a Packages/ path resolves to the package's folder
            var root = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
        }

        internal static string Section(string contractFile) =>
            Start + "\n" +
            "## KeyTween animations\n\n" +
            "UI and object animations in this project are KeyTween states stored in prefabs and scene objects " +
            "(component `KeyTween`: named states such as `Hover` or `Open`, each with tracks and keys).\n\n" +
            "- To create or change one, use the Unity CLI commands `keytween_read`, `keytween_write_scene` and `keytween_write_prefab` " +
            "(`unity command keytween_read --target Assets/UI/Button.prefab`; the Unity Editor must be open; " +
            "from outside the project folder add `--project-path <project folder>`).\n" +
            "- Read the contract first: it is the data format and the rules. `unity command keytween_contract` prints the installed version. " +
            $"It is also the file `{contractFile}` (the path changes when the package is updated: Tools > KeyTween > Set Up Agent Access writes it again).\n" +
            "- Read a target, edit the returned JSON, write it back with its `token`. A write refused as stale means someone edited the data: read again.\n" +
            "- Never edit KeyTween data in `.prefab` or `.unity` files by hand.\n" +
            "- From code a state is played by name: `await anim.PlayAsync(\"Open\")` (`using KeyTweening;`).\n" +
            "- After a write, tell the user which states you made and that they can adjust them in Window > KeyTween Editor.\n" +
            End + "\n";

        /// <summary><paramref name="text"/> with the KeyTween section replaced, or added at the end.</summary>
        internal static string Upsert(string text, string section)
        {
            var start = text.IndexOf(Start, System.StringComparison.Ordinal);
            var end = text.IndexOf(End, System.StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                var after = end + End.Length;
                if (after < text.Length && text[after] == '\r') after++;
                if (after < text.Length && text[after] == '\n') after++;
                return text.Substring(0, start) + section + text.Substring(after);
            }
            if (text.Length == 0) return section;
            return text.TrimEnd('\r', '\n') + "\n\n" + section;
        }
    }
}
