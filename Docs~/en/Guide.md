# User Guide — KeyTween Editor

This guide explains how to create, preview and save animations in the KeyTween editor. The editor UI is in English, and this guide uses the names exactly as they appear on screen.

## 1. Concepts

This page explains the terms used throughout the KeyTween editor.

### The KeyTween Component

`KeyTween` is the component that holds the animation. Add it to a prefab or a scene object, and it animates the objects under it.

The animation data is serialized inside the `KeyTween` component, with no separate asset. As a result, the animation travels with the prefab when you duplicate or move it.

**Time** in the component inspector decides whether playback follows `Time.timeScale`. The default is `Scaled` (it follows); set it to `Unscaled` for UI that must keep moving while the game is paused. For details, see [Play Time](API.md#play-time).

### State

A state is one named animation. Code plays a state by this name.

* Examples: `Hover`, `Open`, `Close`
* A state can contain several tracks.

For details, see [States](#3-states).

### Track

A track animates one property of one object. For example, the Scale of a close button is one track.

For details, see [Adding Tracks](#5-adding-tracks).

### Key

A key is the value of a track at one point in time. As in the Unity Animation window, animating 1 → 1.2 → 1 takes three keys.

For details, see [Editing Keys](#6-editing-keys).

### Arrival

Arrival is how the value moves from the previous key to this one. Choose one of the following:

* An ease (Ease)
* A curve (Curve)
* Punch
* Shake

For details, see [Arrival](#7-arrival).

### Play On

Play On is the moment a state plays by itself. If you do not pick one, the state plays from code. Examples of moments you can pick:

* When the pointer enters
* On click
* When the component is enabled

For details, see [Play On](#4-play-on).

## 2. Opening the Window and Its Layout

The KeyTween window is where you create states and tracks, edit keys and preview the result. This page explains how to open the window and what is on screen.

### Opening the Window

Open the window in any of these ways. The window title is "KeyTween".

* Choose **Window > KeyTween Editor** from the menu.
* Click **Open KeyTween Editor** in the `KeyTween` inspector.
* Click **Edit** or **Create** on a state row of another component. Components such as [ScrollList](ScrollList.md) show these buttons.

### The Edited Target

The window edits the `KeyTween` on the selected object or on one of its parents.

Expand **Raw Data (read-only)** in the inspector to see the serialized data as it is stored. It is read-only; edit states and tracks in the KeyTween Editor.

### Adding a KeyTween

When nothing is being edited, you can add a `KeyTween` right from the window.

1. Select an object with a RectTransform.
2. Click the **Add KeyTween to 'name'** button that appears in the window.

If another `KeyTween` is already being edited, the window keeps that target. In that case, add the component with **Add Component** instead.

### Window Layout

The window is divided into the top bar, the state tabs, the timeline on the left and the inspector on the right.

![The KeyTween window. Below the top bar and state tabs is the timeline with tracks and keys, and the key inspector is on the right.](../media/editor/editor-window.png)

| Area | Contents |
|---|---|
| Top bar | The target name, playback and recording, and saving controls |
| State tabs | One tab per state |
| Timeline (left) | Track rows grouped by object hierarchy, the ruler and the state end (`End`) mark |
| Inspector (right) | Settings for the selected item |

* A state tab's tooltip shows that state's Play On.
* The **+** tab adds a state.
* The inspector's contents change depending on whether you selected a state, track, key, event, sound or child call.

### The Top Bar

The top bar has the following controls.

| Control | Function |
|---|---|
| **▶** | Play and pause |
| **■** | Stop. Returns to the original values |
| **⟲** | Loop |
| **●** | Record |
| Speed | 0.1x, 0.25x, 0.5x, 1x |
| Time | The current time and length |
| **Auto Save**, **Save** | Saving |
| Orange dot | Shown when unsaved values are on screen |

For details, see [Saving](#14-saving--auto-save--save) and [Recording](#15-recording-).

## 3. States

A state is one named animation. You create a state for each situation, such as `Hover`, `Open` or `Close`, and play it from code or through Play On.

### Adding a State

1. Click **+** in the state tabs. States are named `State`, `State 1`, and so on.
2. Select the new state's tab.

The timeline is empty and the state inspector appears.

![A newly created Hover state. The timeline is empty, and the state inspector on the right shows Name, Loop, Duration and Play On.](../media/editor/editor-state.png)

### State Inspector

When no track or key is selected, the inspector shows the state's settings.

| Item | Contents |
|---|---|
| **Name** | The state's name. Play On links follow a new name |
| **Loop** | When playback reaches the end, it continues seamlessly from 0 s and repeats |
| Duration | The state's length and track count (for example, `Duration 0.80s · 3 track(s)`) |
| **Play On** | `None` or an event |
| **Delete State** | Deletes the state after you confirm |

* **Loop** is the same idea as Loop Time on an Animator clip. Events, sounds and child calls fire again on every loop.
* Choosing an event in **Play On** adds a `KeyTweenTrigger` component automatically.

For the available events, see [Play On](#4-play-on).

### State Length

A state's length is set by whatever ends last. The latest of the following is the state's length:

* The last key of a track
* An event
* The end of a sound
* The end of a child call

## 4. Play On

Play On sets when a state plays by itself, without code.

### Choosing Play On

1. Select the state tab to show the state inspector.
2. Choose an event from the **Play On** list.

Choosing an event adds a `KeyTweenTrigger` component automatically.

![The Play On list opened in the state inspector, showing None, OnEnable, PointerEnter, PointerExit, PointerDown, PointerUp, Click, Select, Deselect, ToggleOn, ToggleOff and more.](../media/editor/editor-play-on.png)

### When Each Option Plays

| Play On | Plays when |
|---|---|
| `None` | The state does not play by itself. Play it from code with `Play` or `PlayAsync` |
| `OnEnable` | The component is enabled |
| `Start` | Once, after the component is first enabled |
| `PointerEnter` / `PointerExit` / `PointerDown` / `PointerUp` / `Click` | The matching pointer event occurs |
| `Select` / `Deselect` | The matching selection event occurs |
| `ToggleOn` / `ToggleOff` | The value of the `Toggle` on the same object changes |

For playing from code, see [API](API.md).

### Things to Note

* A warning appears when two states would play at the same moment. For example, `OnEnable` and `Start` are both called on Instantiate.
* A Selectable whose `interactable` is off ignores input events. `OnEnable` and `Start` still play in that case.

### Playing from a UnityEvent

You can also play a state from a UnityEvent, such as a Button's OnClick.

1. Choose `KeyTween.Play (string)` in the function list.
2. Enter the state name.

To play it in reverse, choose `PlayReversed (string)`.

## 5. Adding Tracks

A track animates one property of one object. You choose tracks in the Add Track window and add them to the state.

### Opening the Add Track Window

Right-click an empty part of the timeline and choose **Add Track...**.

![The context menu on the track area, with Add Track... and Add Child Call.](../media/editor/editor-add-track-menu.png)

### Choosing Tracks

The left side of the Add Track window shows the object tree of the prefab. The right side shows the track cards available for the selected object, along with their current values.

1. Select an object in the tree on the left. You can search the tree.
2. Click track cards on the right to select them. You can select several.
3. Click **Add N**.

![Selecting an object and track cards in the Add Track window, then adding them](../media/editor/editor-add-track.gif)

* Double-click a card to add it immediately.
* Your selection is kept while you move between objects.
* Tracks that already exist are marked `Added`.

### Defaults for New Tracks

* A new track gets a key at 0 s and a key at the state's end time. Both keys hold the current value.
* If the state is empty, the end key is placed at 0.2 s.
* Start is **Current Value**. Only text tracks start with Key Value.
* A new per-character track's Stagger is 0.05 s.

For the Start option, see [Track Options](#8-track-options-track-inspector).

### Available Tracks

The Add Track window shows only the tracks that match the components on the selected object.

**Objects**

| Target | Tracks |
|---|---|
| Any object | Scale, Rotation, Active |
| Objects that are not UI (Transform) | Position, World Position, World Rotation |

**UI**

| Target | Tracks |
|---|---|
| RectTransform | Position, Size, Anchor Min, Anchor Max, Pivot, Offset Min, Offset Max |
| CanvasGroup | Group Alpha |
| Graphic (Image, RawImage, TMP, and so on) | Color, Alpha |
| Image (Image Type = Filled) | Fill Amount |
| RawImage | UV Rect |
| Shadow, Outline | Effect Color, Effect Distance |
| RectMask2D | Softness, Padding |
| Slider | Value |
| ScrollRect | Scroll Position |
| LayoutElement | Min Size, Preferred Size, Flexible Size |

**TextMeshPro**

| Target | Tracks |
|---|---|
| TMP_Text | Font Size, Character Spacing, Word Spacing, Line Spacing, Reveal (Typewriter), Number (Count) |
| TMP_Text material | Outline Width, Outline Color, Face Dilate, Underlay Color |
| TMP_Text per character | Char Scale, Char Position, Char Rotation, Char Color, Char Alpha |

**2D, 3D, camera and sound**

| Target | Tracks |
|---|---|
| SpriteRenderer | Color, Alpha |
| Renderer (MeshRenderer and so on) | Material tracks: one per color, number and vector property of its material (e.g. `Material _BaseColor`) |
| Light | Light Color, Intensity, Range (Point, Spot), Spot Angle (Spot) |
| Camera | Camera Background, Camera Size, Field of View, Near Clip, Far Clip, Viewport Rect |
| AudioSource | Volume, Pitch |

**Events and sounds**

| Target | Tracks |
|---|---|
| Root | Animation Events, Sound |

**Other tracks**

* With UIEffect installed, 14 UIEffect tracks are added. For details, see [Integrations](Integrations.md).
* You can write your own track for a component of your project. For details, see [Custom Tracks](API.md#custom-tracks).

**Things to know**

* `Group Alpha` is for fading a whole screen or popup.
* `Rotation` and `World Rotation` use Euler angles in degrees. Values are read in the range −180 to 180, so going from −30° to 0° does not turn the long way around.
* `Active` does not interpolate between keys. It turns the object on or off at each key's time.
* Material tracks write through a MaterialPropertyBlock. The material asset does not change, and other objects that use the same material are not affected.
* TMP material tracks make a material instance used only by that text. When the preview stops, the original material comes back.

### Changing a Track's Target

To change a track's target, click the `path · property ▾` button at the top of the track inspector.

## 6. Editing Keys

A key is the value of a track at one point in time. You add and move keys on the timeline, and edit their values and arrival in the key inspector.

### Selecting and Moving Keys

* Click a key to select it.
* Shift-click or Ctrl-click (Cmd-click on Mac) to add to the selection.
* Drag on empty space in the key area to select the keys the box touches, across tracks. You can start on a track's lane, on the event and child call lanes at the top, or in the empty space below the last track. Hold Shift or Ctrl while dragging to add to the selection.
* Drag one of the selected keys to move all of them by the same time. One Ctrl+Z undoes it.
* When two or more keys at different times are selected, a box spans them. Drag its right handle to stretch or shrink the keys around the left end, or its left handle to do it around the right end; the spacing between the keys scales evenly. A handle stops before the other end, so the keys keep their order. One Ctrl+Z undoes it.

Keys move in 0.01 s steps and never go below 0 s.

### Adding and Deleting Keys

Add a key in any of these ways:

* Double-click an empty part of a row. The key gets the interpolated value at that time.
* Click **◆** on the track header. The key is added at the playhead.
* Right-click and choose **Add Key Here**.

**Every track has a key at 0 s.** If you add a key to a track that has no 0 s key, the 0 s key is created first.

To delete keys, press Delete or Backspace, or right-click and choose **Delete Selected Keys**.

### Navigating the Timeline

| Action | How |
|---|---|
| Move the playhead | Click or drag on the ruler |
| Zoom | The wheel over the ruler, or Ctrl+wheel or Alt+wheel over the rows |
| Pan | Shift+wheel, a horizontal wheel, or a middle-button drag |
| Frame all | `F` or **⤢** |
| Collapse | Click an object row |

* Zooming is centered on the pointer.
* When an object row is collapsed, the keys under it are summarized as small diamonds.
* The ruler shows where the state ends (`End 0.80s`), and everything after that point is shaded dark.

### Key Inspector

When you select a key, the inspector shows a **Key** section followed by an **Arrival** section. Every key uses the same layout.

![The key inspector for the Button · Color track, showing Time 0.2, a color value, the Capture Current Value button, Arrival Type = Ease, Ease = Out Sine and a curve preview.](../media/editor/editor-key-inspector.png)

* In the Key section, fields that do not apply are not hidden. They are disabled and show the reason.
* The Arrival section shows only the fields for the chosen Type.

### Setting a Key's Value

In the Key section, set the key's **Time (s)** and value. The value field is labeled by what the value means, as one of the following:

* `Value`
* `Value (rest +)`
* `Value (press point +)`
* `Value (× own size)`
* `Strength` (for Punch and Shake)

How you edit the value depends on its kind:

* Scale is edited with one linked slider (0 to 2). A button switches to per-axis editing.
* Colors use a color field.
* Active uses a checkbox.

Values that are decided when playback starts are disabled. For example, the first key of a track whose Start is Current Value is disabled.

### Capturing the Current Value

**Capture Current Value** puts the object's current value into the key. It is available on absolute-value tracks only.

1. Pose the object the way you want in the Scene.
2. Click **Capture Current Value** in the key inspector.

### Setting the Arrival

In the Arrival section, choose a **Type** and set the fields for that Type. A curve graph is shown as well.

| Type | Fields |
|---|---|
| Ease | Ease |
| Punch, Shake | Ease, Frequency, Damping |
| Curve | Animation Curve |

* Arrival is disabled on the first key, since no segment arrives at it.
* With several keys selected, you can change Arrival for all of them at once.

For how each Type behaves, see [Arrival](#7-arrival).

## 7. Arrival

Arrival sets how the value moves from the previous key to this one. Choose the Type in the Arrival section of the key inspector.

### Types

| Type | Behavior |
|---|---|
| `Ease` | Moves with one of the ease presets |
| `Curve` | Uses an AnimationCurve to shape the motion |
| `Punch` | Oscillates around the previous value and returns to it |
| `Shake` | Like Punch, but shakes randomly on each axis every frame |

### Ease Presets

`Ease` offers 36 presets:

* Linear
* In, Out and InOut for each of Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Elastic, Back and Bounce
* CustomAnimationCurve (same as Linear)
* OutBackQuad, OutBackCubic, OutBackQuart and OutBackQuint (these correspond to Unity-AnimationUI's OutBack)

The first 32 have the same numbers and formulas as LitMotion's `Ease`.

### Curve

`Curve` shapes the motion with the values of an AnimationCurve.

* 0 is the previous key's value.
* 1 is this key's value.
* Values above 1 overshoot the target and come back.

### Punch and Shake

For Punch and Shake, the key value is not a destination but the **strength**. The formula is the same as LitMotion's.

| Field | Meaning |
|---|---|
| `Frequency` | The number of shakes per second |
| `Damping` | How quickly the shaking fades |

## 8. Track Options (Track Inspector)

Track options decide where the first key's value comes from and what the key values are measured against. Select a track to see them in the track inspector.

![The track inspector for the Button · Color track, showing Start = Current Value, Relative To Rest and Size Relative.](../media/editor/editor-track-inspector.png)

### Start

Sets where the first key's value comes from.

| Value | Meaning |
|---|---|
| `Key Value` | The value written in the key |
| `Current Value` | The value at the moment playback starts |
| `Pointer (press position)` | Where the pointer that played the state was pressed. Position tracks only |

* With `Current Value`, the value does not jump even when playback is cut short. This suits hover and unhover, or open and close.
* If the first key of a `Current Value` track is placed after 0 s, the track waits that long before it starts moving.
* With `Pointer`, the later keys are relative to the press point, so `(0,0)` means "the spot that was pressed". Use it for button ripples.

### Relative To Rest

Adds the key values to the original value. For example, use it to change a button's size by −(34, 17) while it is pressed.

The original value is:

* At runtime: the value at Awake
* In preview: the value when the preview starts

### Size Relative

Key values are read as multiples of the target's own size, so x = 1 means its own width. This option is for Position and Size tracks only.

The motion therefore keeps the same proportions at any resolution. For example, a screen can slide in from one screen width to the right (x = 1) to 0.

### Options on Text Tracks

The options above belong to keyframe tracks. Text tracks have different options.

* A Reveal track has only Scramble.
* Per-character tracks have only Stagger and Duration per Char.
* The Start of a text track is always Key Value.

For details, see [Text Tracks](#9-text-tracks).

## 9. Text Tracks

Text tracks animate the characters of a TMP_Text. You can reveal characters one after another, count a number, or move each character on its own.

### Reveal (Typewriter)

Sets how many characters are visible, as a ratio from 0 to 1. Rich text tags are not counted as characters.

**Scramble** fills the characters that are not shown yet with random characters. Spaces in the original text are kept, so line breaks do not jump. Choose the character set from:

* Uppercase
* Lowercase
* Numerals
* All

### Number (Count)

Counts and changes only the **first number** in the text, and leaves the other characters as they are.

* Examples: `21/100`, `Lv. 3`
* Supports integers and thousands separators (`1,234`).
* With Start set to Current Value, counting starts from the number currently shown.

To set the end number at runtime, call `SetEndValue` from code. The last key's value in the window then becomes a value for preview only. For details, see [API](API.md#changing-end-values-at-runtime).

### Per-Character Tracks

Char Scale, Char Position, Char Rotation, Char Color and Char Alpha are per-character tracks. Their keys describe **the motion of a single character**.

| Option | Meaning |
|---|---|
| **Stagger (s)** | The i-th character starts i × stagger seconds later |
| **Duration per Char (s)** | When greater than 0, the i-th character plays all of its keys over i × this value, in seconds |

On the timeline, the faint tail after the last key is the span in which the later characters play. Drag the end of the tail to change Stagger.

### Combining Per-Character Tracks

When the same text has several per-character tracks, their results are combined.

| Property | Combined by |
|---|---|
| Scale | Multiplying |
| Position, rotation | Adding |
| Color, alpha | Overwriting |

### Playing After Changing the Text

A state with text tracks reads the character count again when it plays. If you change the text and then play the state, the length and spacing follow the new text.

## 10. Events and Sounds

A state can signal your code or play a sound at a set time. Both are added as tracks from the Add Track window.

### Adding an Event

1. Choose `Animation Events` in the Add Track window. An event track is created.
2. Click **+** on the track header, or double-click the row, to add an event.
3. In the inspector, set the **Name**, **Time** and **Type**.

Drag an event to move it.

### Event Type

The Type is one of the following:

* `Name Only`
* A struct marked with `[KeyTweenEvent]` in code

When you choose a struct, you can edit its fields right in the inspector. These field types can be edited:

* float, int, bool, string
* Vector2/3
* Color
* enum

### Receiving Events

Receive events in either of these ways:

* In code. For details, see [API](API.md#animation-events).
* With the `KeyTween Event Listener` component, which connects a UnityEvent to each event name.

### Adding a Sound

1. Choose `Sound` in the Add Track window. A sound track is created.
2. Click **+** on the track header, double-click the row, or drop an AudioClip onto the row to add a sound.
3. In the inspector, set the **Clip**, **Volume** and **Time**.
4. Click **▶ Preview** to listen to it.

The length of the bar on the timeline matches the length of the clip.

### Which AudioSource Plays the Sound

The sound plays through the first AudioSource found in this order, so you do not need to add an AudioSource to every UI object:

1. **Audio Source (optional)** in the sound track's inspector
2. The `KeyTween`'s Audio Source
3. If neither exists, a hidden shared 2D AudioSource

### Sounds and State Length

The state's length includes the end of the sound. As a result, the sound is not cut off when you Destroy the object after a close animation ends.

## 11. When States Overlap

Several states can play at the same time. For example, pressing a button while it is hovered plays both states together.

### Taking Over Properties

The new state takes over **only the properties it animates** from the earlier state, right where they are. The earlier state keeps playing the rest.

* A state that has handed over all of its properties stops, and its `PlayAsync` is canceled.
* Tracks whose Start is Current Value continue from where they stopped. This is why a hover that is cut short does not jump.

### Tracks That Write the Same Value

Tracks of different kinds that write the same value take over **only the part they share (the channels)**.

| Tracks that write the same value | Shared channels |
|---|---|
| Color and Alpha (Graphic, SpriteRenderer) | transparency (a) |
| Char Color and Char Alpha | transparency (a) |
| Position, Size and Offset Min, Offset Max (RectTransform) | the position and size an offset changes |
| Position and World Position, Rotation and World Rotation | all of it |

For example, when a button's `Hover` plays Color and `Fade` then plays Alpha:

* `Hover` keeps writing R, G and B.
* `Fade` takes the transparency.
* The other way round, when `Hover` starts during `Fade`, `Fade` has handed over its only channel, so it stops.

### Finishing the Earlier State First

Sometimes the earlier state must reach its end pose and raise its events before the next one plays. In that case, call `Complete("earlier state")` from code first, then play the next state.

```csharp
anim.Complete("Hover");   // moves the earlier state to its end pose
anim.Play("Press");
```

For details, see [API](API.md#playing).

## 12. Child Calls and Overrides (Nested Prefabs)

These features are for a shared prefab, such as a button, placed inside another prefab, such as a popup.

* **Child call**: the popup's state calls the button's state.
* **Override**: changes the button's animation only inside the popup.

### Adding a Child Call

A child call plays a state of a child prefab at a set time in the parent state.

1. Double-click an object row, or right-click and choose **Add Child Call ▸ child ▸ state**.
2. Drag the bar that appears on the parent's timeline to change its time.
3. In the inspector, set the **Child**, **State** and **Time**.

States that are already called are checked and cannot be chosen.

### How Child Calls Play

* The parent state's length includes `call time + child state length`.
* Calling `Complete` on the parent brings the child to its end values as well.
* If the parent is canceled, a child that has already started keeps playing.
* Cycles of states calling each other are cut off at depth 8.

### Overriding a Track

An override changes the child's animation per track, and only inside the current prefab. Under a child call, the child state's tracks are shown shifted by the call time. The original tracks are dimmed and read-only.

1. Click a dimmed track under the child call. **Only that track** is overridden.
2. Edit its keys. The edits go into the override, and the original prefab is not changed.

In the screenshot below, the Popup prefab overrides the Color key of the Hover state on the nested OkButton prefab with yellow. The button in the Game view keeps its original red, while the Scene view and the KeyTween window show the popup's override.

![A Popup prefab with a nested OkButton prefab. The Game view shows the button's original red, and the Scene view and the KeyTween window show the popup's override: a yellow Color key on OkButton's Hover state.](../media/editor/editor-override.png)

### Editing the Original Prefab

Overrides are saved in the outer prefab's own `KeyTween`. Only the changed tracks are saved, so when you edit other tracks in the original, the popup picks up those changes.

| In the original prefab | Overridden track | Track not overridden |
|---|---|---|
| Change key values or times | The popup keeps its values | The popup follows |
| Add a track | — | It appears in the popup too |
| Delete a track | It stays in the popup | It is removed from the popup too |

### Overrides on Several Levels

When several levels, such as screen > popup > button, override the same track, **the outermost level wins**. This is the same rule as Unity's nested prefabs.

* Overridden tracks have blue names.
* Their tooltip shows which prefab overrides them.

### Reverting Overrides

| Control | What it reverts |
|---|---|
| **×** on a track | The outermost level of that track's override |
| **↺** on the child call title | All overrides at the outermost level (like Unity's Revert All) |
| **Inspector › Overrides › Revert** | Overrides made on a scene instance |

## 13. Preview

Preview lets you see how a state moves, right in the editor. Preview uses the same calculation as the runtime, so it gives the same result as real playback, including child calls and overrides.

### Playing a Preview

1. Select the state to preview in the state tabs.
2. Click **▶** in the top bar.

To see the pose at a given time, drag the ruler to scrub. The following shows a preview in the editor, without entering Play mode.

![Previewing a state in the KeyTween window without Play mode](../media/editor/editor-preview.gif)

### Loop, Speed and Sounds

* **Loop**: Turn on **⟲**, or the state's Loop, to repeat.
* **Speed**: Choose from 0.1x to 1x.
* **Sounds**: Sounds that the playhead passes during playback are played. Scrubbing plays no sound.

### Behavior by Where You Preview

| Where | Behavior |
|---|---|
| Scene (Edit mode) | The changed properties are handed to Unity's `AnimationMode` |
| Prefab mode | The values at the start are remembered and restored when you stop |
| A prefab selected in the Project window | The prefab opens in Prefab mode and the same state is previewed there |
| Play mode | Values are written straight to the running objects and restored when you stop |

* In a scene, `AnimationMode` returns the properties to their original values when you stop, and they are not saved in the scene.
* When the prefab is saved in Prefab mode (Ctrl+S, or Auto Save in Unity's Prefab mode), it is saved with the original values. Then the same time is shown again.
* The prefab asset is not drawn in any view. Playing or scrubbing a prefab selected in the Project window therefore opens it in Prefab mode.

You can also preview in Play mode with the same window.

![Previewing with the same KeyTween window in Play mode](../media/editor/editor-play-mode.gif)

### Starting Pose Chosen Automatically

Some states only show their motion when they start from the pose in which another state ends. Such states are previewed from that pose. The pairs are:

* Play On pairs: PointerExit←PointerEnter, PointerUp←PointerDown, Deselect←Select, ToggleOff←ToggleOn
* Name pairs: Open↔Close, Show↔Hide, In↔Out

### When the Preview Stops

The preview stops when you:

* Click **■**
* Change the target or the state tab
* Delete the state
* Enter or leave Play mode
* Reload scripts
* Save or close the scene
* Close the window

## 14. Saving — Auto Save · Save

Your edits in the KeyTween window are saved wherever that `KeyTween` lives. The tooltips of **Auto Save** and **Save** show where the edits are saved.

### Auto Save and Save

| Control | Behavior |
|---|---|
| **Auto Save** | Saves automatically. On by default |
| **Save** | Saves when you click it |

* Pressing Ctrl+S (Cmd+S on Mac) while the window has focus does the same as Save.
* Auto Save saves 0.4 s after your last edit, so that it does not save every frame while you drag a value.
* Auto Save also saves any remaining edits when Play mode ends. Undo (Ctrl+Z) is saved the same way.
* With Auto Save off, edits are saved only when you click Save.

### Where Edits Are Saved

| What you edit | Where it is saved |
|---|---|
| An object in Prefab mode | That prefab |
| A prefab asset selected in the Project window | That prefab |
| A scene object (Edit mode) | The scene (with Unity's scene save) |
| In Play mode, a prefab copy created at runtime | The original prefab |
| In Play mode, an object placed in the scene | That scene object (applied when Play mode ends) |

When you edit a scene object in Edit mode, both controls are disabled and the tooltip explains why. Edits go straight into the object and are saved with Unity's scene save (Ctrl+S).

### Saving in Play Mode

* Prefab copies created at runtime include screens and list items. Other running copies of the same prefab get the same values immediately.
* Unity normally discards changes to an object placed in the scene. KeyTween applies them to that scene object when Play mode ends, and the scene file is written when you save the scene.
* In Play mode, edits that were not saved with Save while Auto Save was off are lost when Play mode ends.
* Editing part of a nested prefab in Play mode saves the change as an override in the outer prefab, and the inner prefab stays as it is.
* Objects in a scene that has never been saved (Untitled) cannot be found again after Play mode ends, so their edits are not applied.

For overrides, see [Child Calls and Overrides](#12-child-calls-and-overrides-nested-prefabs).

### Orange Dot

Like the `*` on a scene name, the orange dot appears while the values on screen are not saved. Its tooltip says which values these are.

* During preview
* When there are unsaved edits

## 15. Recording (●)

With recording on, you can create keys simply by changing an object in the Scene view or the Inspector. This works the same way as recording in the Unity Animation window.

### Recording Keys

1. Turn on **●** in the top bar. The button turns red.
2. Put the playhead at the time you want.
3. Change an object in the Scene view or the Inspector. The new value becomes a key.

The object itself returns to its value before the change, and the view shows the new key through preview. Each change is one Undo step.

### How Recording Creates Keys

| Case | Result |
|---|---|
| Properties that have a track | A key is placed at the playhead. If a key already exists at that time, only its value changes |
| Properties without a track | A new track is created with a 0 s key (the value before the change) and a key at the playhead (the changed value) |

* For Relative To Rest and Size Relative tracks, the value is converted to that basis.
* Recording at 0 s switches Start to Key Value so that the first key uses the recorded value.

### Recorded Properties

A changed field is recorded onto the track that writes its value. Every track except those under "Not Recorded" is recorded (Transform, UI, TextMeshPro, SpriteRenderer, Light, Camera, AudioSource, UIEffect).

* If the state already has a track that writes the value, the key goes on that track. For example, with a World Position track, moving the object is recorded as World Position; with an Offset Min track, changing Position or Size is recorded as Offset Min; with a Scroll Position track, moving the Content is recorded as Scroll Position.
* Without such a track, the field's default track is created: Position for a position, Color for a color.
* If you change only the alpha of a Color and there is an Alpha track, the change is recorded as Alpha. With only an Alpha track, changing R, G or B creates a Color track.
* With a Number track, changing the number in the text is recorded as Number. Changing a text does not create a Number track.

### Not Recorded

* Material values: Material tracks and TextMeshPro's Outline Width, Outline Color, Face Dilate and Underlay Color. They change the material asset, not a field of the object.
* Reveal and per-character tracks
* Custom tracks that do not list their serialized fields. For details, see [Custom Tracks](API.md#custom-tracks).
* Tracks whose Start is Pointer
* Objects outside the `KeyTween` being edited in the window

### Saving Recorded Keys

Recorded keys are saved by Auto Save and Save as well. Recording also works in Play mode, and keys recorded on a runtime copy stay in the original prefab.

## 16. Context Menus and Shortcuts

These are the context menus and keyboard shortcuts available in the KeyTween window.

### Context Menus

| Where | Menu |
|---|---|
| Empty part of the timeline | Add Track..., Add Child Call ▸ child ▸ state |
| Keyframe track header | Add Key at Playhead, Remove Track (on an override track: Revert Track to Original) |
| Keyframe row | Add Key Here, Delete Selected Keys, Remove Track |
| Event or sound track header | Add Event / Sound at Playhead, Remove Track |
| Event or sound marker | Delete |
| Child call | Revert All (outermost overrides), Remove Child Call |

### Shortcuts

| Key | Action |
|---|---|
| Ctrl+S (Cmd+S) | Save |
| Delete / Backspace | Deletes the selected keys and markers |
| F | Frame all |
| Esc | Closes the context menu |

* When you are editing a scene object in Edit mode, Ctrl+S runs Unity's scene save instead.
* You can change the Save shortcut at `KeyTween/Save` in **Edit > Shortcuts**.

Every edit can be undone with Unity's Undo.

## 17. Not Supported

The KeyTween editor currently does not support the following:

* Copying and pasting keys
* Renaming, duplicating or reordering states directly in the state tabs. To rename a state, use **Name** in the state inspector.

For the playback clock, see Design.
