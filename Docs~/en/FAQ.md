# FAQ and Troubleshooting

Questions that come up when starting out, what to check when something does not work, how other tools map to KeyTween, and versions and support.

## Frequently Asked Questions

### Can I use it with Unity 2022 LTS?

No. It needs Unity 6 (6000.0) or later. The reason is in [Versions and Support](#versions-and-support).

### Does it work with UI Toolkit?

KeyTween is for uGUI (Canvas). There are no tracks that animate UI Toolkit elements. Objects that are not UI can be animated: there are Transform, SpriteRenderer, material property, Light, Camera and AudioSource tracks, and other components are added with [custom tracks](API.md#custom-tracks).

### Can I use it together with DOTween or LitMotion?

Yes. KeyTween does not depend on another tween library, and they do not affect each other. If both animate the same property of the same object at the same time, the value written last in each frame is what you see. Animate one property from one side only.

### Where is an animation stored?

In the `KeyTween` component of the prefab (or scene object). There are no separate assets such as clips or controllers. In version control it shows as a change to that prefab file.

### Can I use it without writing code?

Choose when each state plays (pointer enter, click, when enabled …) in its [Play On](Guide.md#4-play-on), and it plays without code. You can also pick `KeyTween.Play (string)` in a UnityEvent such as a Button's `OnClick`.

### State names are strings. What happens with a typo?

Playing a name that does not exist logs a warning (`[KeyTween] 'object' has no state 'name'.`) and `PlayAsync` completes at once. If a name is used in several places, keeping it in a constant is safer. `anim.HasState("name")` tells whether a state exists.

### The UI animations stop when the game is paused (timeScale 0).

By default they follow game time. For UI that must move while the game is paused, such as a pause menu, set that `KeyTween`'s **Time** to `Unscaled`.

### Which platforms has it been checked on?

Development, tests and performance measurements were done on Windows (the editor and an IL2CPP player). Other platforms have not been checked. There is no platform-specific code, and eases are computed with Burst.

### What do the samples need?

The Input System package. TextMeshPro's Essential Resources are imported with a sample if the project does not have them.

### What happens when an object is destroyed while a state plays?

* **An object a track animates is destroyed**: only that track is skipped. The other tracks play to the end and `PlayAsync` completes normally, with no error.
* **The object with the KeyTween is destroyed or turned off**: the play stops and `PlayAsync` ends with `OperationCanceledException`. No error is logged.

### How does it look in version control? What about merge conflicts?

An animation has no file of its own: it is inside the `KeyTween` component in the prefab (or scene) file. Editing a state changes that prefab file, and version control shows it like any other prefab change.

* Two people editing states of the same prefab at once is an ordinary prefab conflict. Use Unity's Smart Merge (UnityYAMLMerge), or edit one at a time.
* Splitting screens and parts into prefabs narrows what can conflict. A child's animation changed from an outer prefab is saved in the outer prefab's file.
* With Auto Save on, an edit is written to the prefab file 0.4 seconds after the last change. Something tried in the window is in the file too, so check the changes before committing.

### How do we remove KeyTween later?

1. Replace the code that uses the `KeyTweening` namespace.
2. Remove the KeyTween components, such as `KeyTween` and `KeyTweenTrigger`, from prefabs and scenes. Removing the package first leaves them as Missing Scripts.
3. Remove the package in the Package Manager.

There is no export of an animation to an Animator clip or another tool's format. The data goes with the component.

## Troubleshooting

### I played a state and nothing happens

* **Look for a warning in the Console.** A wrong state name logs `has no state`.
* **Check Time.** With `Time.timeScale` at 0, a KeyTween whose Time is `Scaled` stands still.
* **Check the tracks' objects.** A track whose object was deleted or is empty writes nothing.
* **Check the other states.** A state played later takes the properties it animates. See [When States Overlap](Guide.md#11-when-states-overlap).
* **Check for a Layout Group.** The layout sets the position and size of objects inside a Layout Group. Put a child inside and animate that child, or use a Scale track.

### The editor preview works but it does not play in Play mode

* A state without a Play On has to be played from code.
* A state played by the pointer needs an EventSystem in the scene and Raycast Target enabled on the object's graphic.
* If a Selectable on the same object (a Button …) is not `interactable`, Play On moments that come from input are ignored.

### `PlayAsync` throws `OperationCanceledException`

The play did not reach its end: the same state was played again, another state took all of its properties, the object was disabled, or `Stop` was called. Every case is in [How a Play Ends](API.md#how-a-play-ends).

* If the code after the `await` should run only when the state really finished, this is the intended behavior. Catch it with `try` / `catch (OperationCanceledException)`.
* To bring the earlier state to its end pose first, call `Complete` on it before playing the next one.

### The value jumps when an animation is interrupted

If the track's **Start** is `Key Value`, every play starts from the first key's value. For animations the pointer can interrupt (hover, press), set Start to `Current Value`, and the track carries on from where the property is.

### Playing many objects at once makes that frame slow

Starting a play has a cost. If thousands start in the same frame, spread them over a few frames. The numbers are in [Performance](Performance.md).

### A custom track shows `(missing)`, or does not move in a build

A custom track is found by its saved class name.

* Renaming the class, its namespace or its assembly makes existing tracks lose it.
* If the build strips code (IL2CPP with Managed Stripping Level above Minimal), put `[Preserve]` on the class.

### Edits made in Prefab Mode do not show on the running screen

Running copies that already exist do not get them; copies made afterwards do. Selecting the running copy in Play mode and editing it shows at once, and is saved to the prefab too.

### A warning says `Sample` is obsolete

Use `Stop(state, time)`. It does the same.

### An agent's write is refused as `Stale token`

The data changed after it was read. Read again, redo the change on that document, and write. See [Creating with AI Agents](Agents.md#safeguards).

## Coming from Other Tools

There is no converter. You make the animation again in the KeyTween window. Move one screen or prefab at a time; the old tool can stay in use meanwhile.

### From DOTween or LitMotion

| Code tween | KeyTween |
|---|---|
| One tween, one sequence | One state |
| `DOScale`, `DOColor`, `BindToLocalScale` … | A track of that property and its keys |
| `SetEase`, `WithEase` | The key's Arrival (Ease) |
| A sequence's `Append`, `Insert`, `SetDelay` | The times of the keys. Several tracks share one timeline |
| `SetLoops(-1)`, `WithLoops(-1)` | The state's Loop |
| Starting from the current value | The track's Start = `Current Value` |
| `From` | The track's Start = `Key Value` |
| `DOPunch…`, `DOShake…`, `LMotion.Punch`, `LMotion.Shake` | The key's Arrival = Punch / Shake |
| Revealing text character by character | A Reveal (Typewriter) track |
| Counting a number | A Number (Count) track; the end value with `With(...).WithEndValue(...)` |
| `OnComplete` | `await anim.PlayAsync(...)`, the state's On Complete, `WithOnComplete` |
| `Kill`, `Cancel` | `Stop` |
| `Complete` | `Complete` |
| `Goto`, `handle.Time` | `Play(state, time)`, `Stop(state, time)` |
| `SetUpdate(true)` (ignores pausing) | The KeyTween's Time = `Unscaled` |

Animations you would rather keep making in code can be made with `Build(...)`. The point of KeyTween is keeping the animation in the editor, though, so passing only the values known at runtime with `With` is the recommended split.

### From Animator

| Animator | KeyTween |
|---|---|
| A clip | A state |
| The controller's states and transitions, `SetTrigger` | `Play("name")` in code, or Play On |
| The clip's Loop Time | The state's Loop |
| Animation Event | An event track |
| Override Controller | Overriding a child state's tracks |
| Update Mode = Unscaled Time | The KeyTween's Time = `Unscaled` |

* There is no transition graph. Code or Play On decides which state plays when.
* A clip's keys cannot be imported. With [recording](Guide.md#15-recording-), changing values in the Scene view and the Inspector is enough to make keys.

## Versions and Support

### Supported Unity

Unity 6 (6000.0) or later.

* `PlayAsync`, which waits for a play, returns Unity's `Awaitable`, and the package depends on uGUI 2.0 (which includes TextMeshPro). Both come with Unity 6, so it does not compile on 2022 LTS.
* Installing and compiling were checked on 6000.0.85f1. Development and the full tests run on 6000.6.

### Versioning

It follows [Semantic Versioning](https://semver.org). Before 1.0.0 (0.x):

* **MINOR** (0.3 → 0.4): new features, and the API may change.
* **PATCH** (0.3.1 → 0.3.2): bug fixes, and editor improvements that do not change playback.
* An API that goes away is first marked `[Obsolete]` with what to use instead, and removed in the MINOR version after that.
* Data saved in prefabs is converted automatically when a newer version loads it.

What changed in each version is in the [CHANGELOG](../../CHANGELOG.md). To pin the version you use, add its tag to the install address, like `#v0.4.1`.

### Reporting a Problem

Open an issue on [GitHub Issues](https://github.com/UnialGames/KeyTween/issues). The Unity version, the KeyTween version and the steps to reproduce make it quick to check.
