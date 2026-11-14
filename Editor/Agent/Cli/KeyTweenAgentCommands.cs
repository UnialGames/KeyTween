using System;
using System.IO;
using Unity.Pipeline.Commands;

namespace KeyTweening.Editor
{
    /// <summary>
    /// The agent operations as Unity CLI commands (`unity command keytween_read …`), which the CLI also offers as MCP tools.
    /// This assembly compiles only when com.unity.pipeline is installed (asmdef version define KEYTWEEN_PIPELINE).
    /// Read and the two writes are separate commands, so a person can allow reading, scene edits and prefab edits one by one.
    /// </summary>
    static class KeyTweenAgentCommands
    {
        const string Document = "The JSON document: what keytween_read returned, with \"states\" edited (the format: keytween_contract).";
        const string FromFile = "A .json file holding the document, instead of content (long documents, shells that mangle quotes).";

        [CliCommand("keytween_contract",
            "KeyTween: the contract (Markdown) that explains the document keytween_read returns and the writes take: fields, track ids, " +
            "eases, and the rules (token, all or nothing). Read it before the first write. Changes nothing.",
            Tags = new[] { "animation" })]
        static string Contract() => KeyTweenAgent.ContractText();

        [CliCommand("keytween_read",
            "KeyTween: read an object's animation states (tracks, keys, Play On) as a JSON document, with the objects and tracks that can be animated. " +
            "Without a target: lists the KeyTweens of the open scenes and of the prefab open in Prefab Mode. Changes nothing.",
            Tags = new[] { "animation" })]
        static object Read(
            [CliArg("target", "A prefab asset (Assets/UI/Button.prefab, or Assets/UI/Popup.prefab:Panel/Ok for an object inside it) or an object of an open scene (/Canvas/Button). Empty: list what is there.")] string target = "",
            [CliArg("objects", "Include the objects under the target and the tracks each takes (default true). false: only the states.")] bool objects = true) =>
            string.IsNullOrEmpty(target) ? KeyTweenAgent.Survey() : KeyTweenAgent.Read(target, objects);

        [CliCommand("keytween_write_scene",
            "KeyTween: create, replace or delete animation states on an object of an OPEN SCENE. All or nothing; refused when the object changed since it was read (token). " +
            "One Undo step (Ctrl+Z). The scene is marked unsaved and never saved by this command.",
            Tags = new[] { "animation" })]
        static KeyTweenAgentResult WriteScene(
            [CliArg("content", Document)] string content = "",
            [CliArg("file", FromFile)] string file = "",
            [CliArg("dry_run", "Check the document and report what would change, without changing anything.")] bool dryRun = false) =>
            KeyTweenAgent.Write(Content(content, file), KeyTweenAgentScope.Scene, dryRun);

        [CliCommand("keytween_write_prefab",
            "KeyTween: create, replace or delete animation states in a PREFAB ASSET (every instance of the prefab gets them). All or nothing; refused when the prefab changed since it was read (token). " +
            "The prefab is saved; Ctrl+Z puts the data back in the editor.",
            Tags = new[] { "animation", "prefabs" })]
        static KeyTweenAgentResult WritePrefab(
            [CliArg("content", Document)] string content = "",
            [CliArg("file", FromFile)] string file = "",
            [CliArg("dry_run", "Check the document and report what would change, without changing anything.")] bool dryRun = false) =>
            KeyTweenAgent.Write(Content(content, file), KeyTweenAgentScope.Prefab, dryRun);

        static string Content(string content, string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("file must be a .json file.");
                if (!File.Exists(file)) throw new ArgumentException($"There is no file '{file}'.");
                return File.ReadAllText(file);
            }
            if (string.IsNullOrEmpty(content)) throw new ArgumentException("Give the document as content (JSON) or as file (a .json file).");
            return content;
        }
    }
}
