# KeyTween agent contract (version 1)

How an AI agent creates and edits KeyTween animations. You read an object's animation as a JSON document, change the
document, and write it back. The result is ordinary KeyTween data (states, tracks, keys) stored in the prefab or scene
object, so a person opens it in **Window > KeyTween Editor** and carries on from there.

Do not edit KeyTween data by hand in `.prefab` / `.unity` YAML. Use the commands below.

## Commands

They are Unity CLI commands (package `com.unity.pipeline`; the Unity Editor must be open with the project). The same
commands are MCP tools after `unity mcp configure <client>`. Run from the project folder, or add
`--project-path <project folder>` (after `command`).

| Command | Changes | Use |
|---|---|---|
| `keytween_contract` | nothing | Prints this document, as installed. |
| `keytween_read` | nothing | `--target <target>`: the document of one object. No target: the KeyTweens of the open scenes and of the prefab open in Prefab Mode. `--objects false` leaves out the object list. |
| `keytween_write_scene` | an object of an **open scene**, in memory | One Undo step. The scene is marked unsaved and is never saved by the command. |
| `keytween_write_prefab` | a **prefab asset** (every instance gets it) | Saved. If the prefab is open in Prefab Mode it is changed there and saved by the KeyTween Editor (Auto Save / Save). |

Both writes take `--content '<json>'` or `--file <path>.json`, and `--dry_run true` to check a document without changing
anything (a dry run returns the same token: nothing changed). A read tells you which write a target needs (`"scope"`).
Nothing is written in Play mode. The commands change KeyTween data only: they add the `KeyTween` (and, for Play On, the
`KeyTweenTrigger`) component when needed, and a `CanvasGroup` to a UI object that a `CanvasGroupAlpha` track fades and
that has none. No other component.

```
unity --json command keytween_read --target Assets/UI/Button.prefab
unity --json command keytween_write_prefab --file Temp/hover.json --dry_run true
unity --json command keytween_write_prefab --file Temp/hover.json
```

## Targets

The target is the object that has (or will get) the `KeyTween` component.

- A prefab asset: `Assets/UI/Button.prefab`. An object inside it: `Assets/UI/Popup.prefab:Panel/OkButton` (path from the
  prefab's root, without the root's name).
- An object of an open scene: `/Canvas/Button`. A scene that is not open is not loaded for you: ask the person to open it.

## The rules of a write

1. **Read first, then write from what you read.** A read returns a `token`. Send it back unchanged. If the data changed
   since the read (a person edited it, or an earlier write of yours), the write is refused: read again and redo your
   change on the new document. Never edit a token.
2. **All or nothing.** Every problem is reported in one error and nothing is changed. Fix them all and write again.
3. **Each state in `states` is created, or replaces the state of that name as a whole.** States you leave out stay as
   they are, so send only the states you change. `deleteStates` removes states by name.
4. What this contract cannot express is kept when a state is replaced: sounds, child calls, callbacks, and tracks whose
   object is missing or outside the target. A read lists them in `keeps`.
5. A write returns the new `token`: use it for your next write of the same target.

## The document

```json
{
  "contract": 1,
  "target": "Assets/UI/Button.prefab",
  "scope": "prefab",
  "token": "3f9a0c21d4e87b65",
  "hasKeyTween": true,
  "objects": [
    { "path": "", "components": ["RectTransform", "Image", "Button"], "tracks": ["Scale", "Rotation", "Active", "SizeDelta", "AnchoredPosition", "GraphicColor", "GraphicAlpha", "CanvasGroupAlpha"] },
    { "path": "Label", "components": ["RectTransform", "TextMeshProUGUI"], "tracks": ["Scale", "GraphicColor", "FontSize", "TextReveal", "CharScale", "CanvasGroupAlpha"] }
  ],
  "states": [
    {
      "name": "Hover",
      "loop": false,
      "playOn": "PointerEnter",
      "tracks": [
        {
          "object": "",
          "track": "Scale",
          "start": "Current",
          "keys": [
            { "time": 0, "value": [1, 1, 1] },
            { "time": 0.2, "value": [1.1, 1.1, 1.1], "ease": "OutBack" }
          ]
        }
      ],
      "events": [ { "time": 0.2, "name": "Hovered" } ]
    }
  ],
  "deleteStates": []
}
```

`scope`, `hasKeyTween`, `objects`, `duration` and `keeps` are read-only: a write ignores them. A write needs only
`contract`, `target`, `token` and `states` (and/or `deleteStates`): you can send the document you read with `states`
edited, or a new file with just those fields. A target without a
KeyTween reads as `"hasKeyTween": false` with token `"none"`; writing states to it adds the component.

### State

| Field | Meaning |
|---|---|
| `name` | How code and Play On find it: `anim.Play("Hover")`. Unique in the KeyTween. |
| `loop` | Plays over and over until stopped (idle motion, a pulsing badge). |
| `playOn` | When it plays without code. `None` (the default when left out) for a state your code plays (`anim.Play("Open")`, a screen container). Otherwise: `OnEnable`, `Start`, `PointerEnter`, `PointerExit`, `PointerDown`, `PointerUp`, `Click`, `Select`, `Deselect`, `ToggleOn`, `ToggleOff`. Pair them: a `Hover` on `PointerEnter` needs an `Unhover` on `PointerExit` that goes back. |
| `tracks` | One per animated value. |
| `events` | `{ "time", "name" }`: animation events code receives (`EventRaised`, `WaitEventAsync`). |

The state's length is its last key (or event).

### Track

| Field | Meaning |
|---|---|
| `object` | Path from the target; `""` is the target itself. Must be listed in `objects`. |
| `track` | A track id from that object's `tracks` list. Built-in ids are `KeyTweenProperty` names (`Scale`, `AnchoredPosition`, `GraphicColor`, `GraphicAlpha`, `CanvasGroupAlpha`, `TextReveal`, …). A material property is `MaterialColor:_BaseColor`; a custom track is `Custom:Namespace.Type, Assembly`. |
| `start` | Where the first key's value comes from when the state starts. `Current` (default): the value the property has at that moment, so an interrupted animation never jumps: use it for anything a pointer can interrupt. `KeyValue`: the value written in the first key (an entrance that must start from a set pose). `Pointer` (`AnchoredPosition` only): where the pointer pressed. |
| `relativeToRest` | Key values are added to the value the property had when the object woke up. |
| `sizeRelative` | `AnchoredPosition` / `SizeDelta` only: values are multiples of the object's own size (`[1, 0]` = one width to the right). |
| `stagger`, `durationPerChar` | Per-character text tracks (`CharScale`, `CharOffset`, `CharRotation`, `CharColor`, `CharAlpha`): seconds between characters. |
| `scramble` | `TextReveal` only: `None`, `Uppercase`, `Lowercase`, `Numerals`, `All`. |
| `keys` | At least one, in any order (they are sorted by time). |

### Key

| Field | Meaning |
|---|---|
| `time` | Seconds from the start of the state, 0 or more. |
| `value` | As many numbers as the track uses: 1 (`GraphicAlpha`, `FillAmount`, `TextReveal` 0..1, `Active` 0 or 1), 2 (`AnchoredPosition`, `SizeDelta`), 3 (`Scale`, `Rotation` in degrees), 4 (colors as r, g, b, a from 0 to 1). A wrong count is an error that names the right one. |
| `arrival` | How the track gets to this key from the one before: `Ease` (default), `Curve`, `Punch`, `Shake`. The first key has no arrival. |
| `ease` | For `Ease`: `Linear`, and `In` / `Out` / `InOut` of `Sine`, `Quad`, `Cubic`, `Quart`, `Quint`, `Expo`, `Circ`, `Back`, `Elastic`, `Bounce` (e.g. `OutBack`), and `OutBackQuad`, `OutBackCubic`, `OutBackQuart`, `OutBackQuint` (stronger overshoots). Default `OutSine`. A wrong name is an error that lists them all. |
| `frequency`, `damping` | For `Punch` / `Shake`: the key's `value` is the strength, and the track ends back where it was. |
| `curve` | For `Curve`: points `{ "time", "value", "inTangent", "outTangent" }` from 0 to 1 over the segment. |

With `start: Current` and two or more keys, the first key's `value` is replaced by the live value when the state plays,
and the track holds it until the first key's time (a delay). A track with one key at a time above 0 goes from the live
value to that key.

## Choosing eases (UI)

- Entering (appear, open, hover): an `Out` ease. `OutBack` overshoots a little (buttons, popups), `OutCubic` settles calmly.
- Leaving (close, unhover): an `In` ease for scale and position (`InCubic`, `InQuad`), `InSine` or `OutSine` for alpha.
  About half the entrance's length.
- Idle loops: `InOutSine`, with `loop: true` and a last key equal to the first.
- Hits and errors: `Punch` (one direction and back) or `Shake`, 0.2 to 0.4 s.
- Typical lengths: hover 0.1 to 0.2 s, popup 0.2 to 0.35 s, screen 0.3 to 0.5 s.
- Typical amounts: "slightly smaller / bigger" is a scale of 0.9 to 0.95 / 1.05 to 1.1; a hover grows by 0.05 to 0.1.

## Fading a group

To fade a whole popup or panel with its children, write one `CanvasGroupAlpha` track on that object: one track fades
everything under it. Every UI object (one with a `RectTransform`) lists `CanvasGroupAlpha` in its `tracks`. If the object
has no `CanvasGroup` yet (see its `components`), the write adds one, which changes nothing by itself (alpha 1), and says so
in `changes`. Use `GraphicAlpha` for one graphic, or for children that fade at different times, not one per graphic to
fade a group.

A closing state leaves the alpha at 0, so the opening state must start from 0 and bring it back to 1
(`start: KeyValue`). An object at alpha 0 still takes clicks, and these commands do not turn it off: tell the person to
deactivate it after the closing state (or turn off its CanvasGroup's Blocks Raycasts) if it stays over other controls.

## After a write

Tell the person what you made (state names, when each plays, and how their code plays it if `playOn` is `None`) and
that they can adjust it in **Window > KeyTween Editor**:
drag keys, pick another ease, press ▶ to preview without Play mode. A scene write is not saved until they save the scene.
