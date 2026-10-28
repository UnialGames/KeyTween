# Using KeyTween from Code — API

KeyTween lives in the `KeyTweening` namespace, and the component you add to a GameObject is `KeyTween`.

Animations are made in the editor as states ([Guide](Guide.md)). A state is one named animation, built from tracks and keys. From code, you **play a state by name and await it**.

## Playing

The `KeyTween` component provides methods to play and control a state by its name.

```csharp
public sealed class KeyTween : MonoBehaviour
{
    public void Play(string stateName);              // takes one argument, so it is also listed in UnityEvents such as Button OnClick
    public void Play(string stateName, PointerEventData pointer);   // for Start = Pointer tracks (where the pointer was pressed)
    public Awaitable PlayAsync(string stateName, CancellationToken cancellationToken = default);
    public void PlayReversed(string stateName);      // also listed in UnityEvents
    public Awaitable PlayReversedAsync(string stateName, CancellationToken cancellationToken = default);

    public void Complete(string stateName);         // jumps a playing state to its end
    public void Stop(string stateName);             // stops the state where it is
    public void Sample(string stateName, float time);   // writes the pose at a given time, without playing

    public bool IsPlaying(string stateName);
    public bool HasState(string stateName);
    public float Duration(string stateName);        // length including overrides; 0 if there is no such state
}
```

| Method | Behavior |
|---|---|
| `Play` | Starts playing. |
| `PlayAsync` | Starts playing and waits until the play ends. |
| `PlayReversed` | Plays from the end back to the start. |
| `Complete` | Jumps a playing state to its end pose. |
| `Stop` | Stops the state where it is. |
| `Sample` | Writes the pose at a given time, without playing. |

### Playing a State

`Play` starts playing and writes the first pose (0 s) **immediately**. If the state name does not exist, it only logs a warning.

```csharp
anim.Play("Open");
```

If the state has Start = Pointer tracks, also pass `pointer`. The tracks use it to know where the pointer was pressed.

```csharp
public void OnPointerDown(PointerEventData eventData) => anim.Play("Press", eventData);
```

### Awaiting a Play

`PlayAsync` completes when the play reaches the end or when `Complete` is called.

```csharp
await anim.PlayAsync("Open", destroyCancellationToken);
```

* If the play cannot finish, it ends with `OperationCanceledException`. For details, see [How a Play Ends](#how-a-play-ends).
* If the state does not exist, it completes immediately.
* It returns a Unity `Awaitable`, so you can await it **only once**.
* For a looping state, `PlayAsync` does not end until you call `Complete` or `Stop`.

### Playing in Reverse

`PlayReversed` plays from the end back to the start. To wait until it ends, use `PlayReversedAsync`.

```csharp
await anim.PlayReversedAsync("Open");   // returns to where the forward play started
```

* Events, sounds and child calls are each raised once, in reverse order.
* Child states also play reversed.
* Start = Current Value tracks reuse the value captured by the last forward play. So a reversed play returns exactly to where the forward play started.

### Completing a Play

`Complete` moves a playing state to its end pose and completes `PlayAsync` normally. It works only while the state is playing.

```csharp
anim.Complete("Open");
```

* The remaining events and child calls are raised in order.
* Remaining sounds are not played.
* During a reversed play, it moves to the start pose instead.

### Stopping a Play

`Stop` stops the state where it is. A pending `PlayAsync` is canceled. Use it to turn off a looping state.

```csharp
anim.Stop("Idle");
```

### Writing the Pose at a Given Time

`Sample` clamps the time to the range from 0 to the state's length and writes the pose at that time. Use it when something other than time drives the pose, such as a scroll position.

```csharp
anim.Sample("Reveal", progress * anim.Duration("Reveal"));   // progress: 0 to 1
```

* No timer runs, and no events or sounds are raised.
* If the state is playing, it stops.

### Checking a State

Use `IsPlaying`, `HasState` and `Duration` to check a state. `Duration` is the length including overrides, and 0 if there is no such state.

```csharp
if (anim.HasState("Open") && !anim.IsPlaying("Open")) anim.Play("Open");
```

### Play Time

Each KeyTween's **Time** (`timeMode`) decides whether playback follows `Time.timeScale`.

| Time | Clock | Use |
|---|---|---|
| `Scaled` (default) | Game time. Stops while `Time.timeScale` is 0 | Game objects, battle effects |
| `Unscaled` | Real time. Keeps playing while the game is paused | Pause menus, loading screens |

```csharp
pauseMenu.timeMode = KeyTweenTimeMode.Unscaled;   // applies from the next play
```

* It can be overridden per prefab instance. For example, keep a shared button `Unscaled` and switch only the buttons placed on the game screen to `Scaled`.
* A state played by a child call follows the child KeyTween's **Time**.
* Outside Play mode (playing from code in the editor), both use real time.

### When States Overlap

If another playing state is animating the same property, the new state takes over only that property, from its current value. To let the earlier state reach its end pose first, call `Complete` before playing.

```csharp
anim.Complete("earlier state");
anim.Play("next state");
```

For details, see [Guide §11](Guide.md#11-when-states-overlap).

### How a Play Ends

The table below shows how `PlayAsync` ends in each case, and what happens to the events and sounds that have not been reached yet.

| Case | `PlayAsync` result | Remaining events | Remaining sounds |
|---|---|---|---|
| Reaches the end | completes | all raised | all played |
| `Complete` called | completes | raised in order | not played |
| Another state takes all of its properties | canceled | not raised | not played |
| The same state is played again | earlier play canceled | not raised | not played |
| Object disabled | canceled | not raised | not played |
| `Stop` called, token canceled | canceled | not raised | not played |

A play that does not reach its end ends as **canceled**. This makes "continue only when the play really finished" the default behavior.

### Example: Closing a Popup

If the popup is opened again while it is closing, the close play is canceled and the popup is not destroyed.

```csharp
try { await popup.PlayAsync("Close", cancellationToken: popup.destroyCancellationToken); }
catch (OperationCanceledException) { return; }   // Open took over Close's properties, so keep the popup
Destroy(popup.gameObject);
```

### Example: A Dialogue Box

Change the text of the same TMP object and play the same state again. To skip, call `Complete`. The character count is read again on each play, so the animation follows the new line.

```csharp
dialogue.text = nextLine;
var typing = anim.PlayAsync("Typewriter");
// skip button: anim.Complete("Typewriter");
await typing;
```

The story screen of the RPG sample shows its lines one character at a time in this way.

![Story dialogue in the RPG sample: portraits and text that appears one character at a time](../media/rpg/rpg-story.gif)

## Awaiting Several KeyTweens Together

This section shows how to play `KeyTween` components on several objects at once and wait until all of them have finished.

### Starting All, Then Awaiting Each

`PlayAsync` returns a Unity `Awaitable`, not a .NET `Task`, so you cannot pass it to `Task.WhenAll` directly. However, each play starts as soon as `PlayAsync` is called. If you start them all first and then await them one by one, the result is the same as `WhenAll`.

```csharp
var plays = anims.Select(a => a.PlayAsync("Hover")).ToList();   // all of them start here, at the same time
foreach (var play in plays) await play;                         // wait until every one has ended
```

### Awaiting with Task.WhenAll

To use `Task.WhenAll`, wrap each play in a `Task`.

```csharp
static async Task AsTask(Awaitable awaitable) => await awaitable;

await Task.WhenAll(anims.Select(a => AsTask(a.PlayAsync("Hover"))));
```

### Awaiting with UniTask.WhenAll

If UniTask is installed, you can use `PlayUniTaskAsync` from the [integrations](Integrations.md).

```csharp
await UniTask.WhenAll(anims.Select(a => a.PlayUniTaskAsync("Hover")));
```

### When One Play Is Canceled

If any of the plays is canceled ([How a Play Ends](#how-a-play-ends)), an `OperationCanceledException` is thrown. Where it is thrown depends on the approach.

* Starting all, then awaiting each: at the `await` of the canceled play
* `Task.WhenAll`, `UniTask.WhenAll`: at `WhenAll`

## Changing End Values at Runtime

Some animations have an end value that is known only at runtime, such as a currency amount or a power rating. For these, make the motion (timing and easing) in the editor, and pass only the end value from code.

```csharp
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, Vector4 value);
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, float value);   // sets x only
```

### Setting the End Value and Playing

Call `SetEndValue` before playing.

```csharp
// "Count" state on the gold label: a Number (Count) track, Start = Current Value, 0.6 s OutQuad (made in the editor)
count.SetEndValue("Count", goldLabel, KeyTweenProperty.TextNumber, gold);
count.Play("Count");
```

### What Changes

`SetEndValue` replaces the **last key value** of the matching track.

* The new value applies from the next play onward.
* The saved data is not changed.
* The value is interpreted exactly like a key value, so the Relative To Rest and Size Relative settings apply.
* If no track matches, it logs a warning.

## Animation Events

You can receive, in code, the events placed on an event track in the editor. An event is raised when a play passes its time.

### Defining an Event Data Type

A struct marked with `[KeyTweenEvent]` appears in the editor's event type list.

```csharp
[KeyTweenEvent("Popup Sound")]
public struct PopupSoundEvent { public string clip; public float volume; }
```

* Public fields are saved as JSON.
* Object references cannot be saved.
* The type list shows only structs that are included in the player build.

### Receiving All Events

`event KeyTweenEventHandler EventRaised` is raised when a play passes an event. `Complete` raises the skipped events in order; a cancel raises none.

```csharp
anim.EventRaised += (in KeyTweenEventInfo e) => Debug.Log($"{e.State}:{e.Name} @ {e.Time}");
```

### Receiving Events by Data Type

`AddListener<T>` receives only events whose data type is `T`. Call `Dispose` on the returned `IDisposable` to remove the listener.

```csharp
using var sub = anim.AddListener<PopupSoundEvent>((in KeyTweenEventInfo e, in PopupSoundEvent data) => Play(data.clip));
```

### Waiting for an Event

`WaitEventAsync` completes the next time an event with that name is raised, from any state. It returns `Awaitable<KeyTweenEventInfo>`.

```csharp
var info = await anim.WaitEventAsync("Shown");
```

### Reading Event Info

`KeyTweenEventInfo` is a readonly struct, passed with `in`. It provides the following members.

* `Animation`
* `State`
* `Name`
* `Time`
* `TryGetData<T>(out T)`

### Receiving Events Without Code

The `KeyTweenEventListener` component receives events without code.

1. Add **Add Component > KeyTween > Event Listener**.
2. Connect event names to UnityEvents.

The event data is not passed in this case.

## Adding States at Runtime

States are usually made in the editor, but you can also add a state from code.

### Adding a State from Code

`AddState` adds a state. It replaces an existing state with the same name.

```csharp
public void AddState(KeyTweenState state);
```

Screen containers use this method to add their default transitions ([Screens](Screens.md)). For ordinary animations, we recommend making them in the editor.

## Reading and Writing Values

You can read and write a single property that a track animates directly from code, without playing anything.

### Reading and Writing a Property

`KeyTween.Read` and `KeyTween.Write` are static methods.

```csharp
public static Vector4 KeyTween.Read(Component target, KeyTweenProperty property);
public static void KeyTween.Write(Component target, KeyTweenProperty property, Vector4 value);
```

### How Values Are Interpreted

Values are interpreted the same way tracks interpret them.

* Properties with a single value use `x`.
* `Rotation` is read in the range −180..180.
* `TextNumber` reads the first integer in the text and writes in `1,234` format.
* Per-character properties cannot be written, because they are applied directly to the mesh while playing.

## Sound Source

This setting decides which AudioSource plays the sounds of sound tracks.

### Choosing the AudioSource

Set it with **Audio Source** in the inspector (`KeyTween.audioSource`).

* If it is set, sounds play through that source.
* If it is empty, sounds play through one shared, hidden 2D AudioSource (`PlayOneShot`).

You only need to set it when you need a mixer group or 3D sound. Each sound track can also set its own source.

## Optional Integrations

When the following libraries are installed, KeyTween gains extra extension methods.

| Library | Extension methods |
|---|---|
| UniTask | `PlayUniTaskAsync`, `PlayReversedUniTaskAsync`, `WaitEventUniTaskAsync` |
| R3 | `PlayAsObservable`, `EventsAsObservable`, `EventAsObservable` |

An integration turns on automatically when its library is installed, with nothing to configure. For details, see [Integrations](Integrations.md).

## Other Components

Besides `KeyTween`, the package provides the following components.

| Component | Documentation |
|---|---|
| `KeyTweenTrigger` (Play On) | Added automatically when you pick Play On in the editor. [Guide §4](Guide.md#4-play-on) |
| `KeyTweenScrollList`, `KeyTweenPageIndicator`, `KeyTweenScrollScrub`, `KeyTweenPullToRefresh` | [ScrollList](ScrollList.md) |
| `KeyTweenScreen`, `KeyTweenPageContainer`, `KeyTweenModalContainer`, `KeyTweenSheetContainer`, `KeyTweenSharedElement` | [Screens](Screens.md) |
| `KeyTweenEventListener` | [Animation Events](#animation-events) |

## Custom Tracks

You can make a track for a value that has no built-in track. Write one class that animates a component of your project, and it appears in the Add Track window for objects that have that component.

### Writing a Track Class

Derive from `KeyTweenFloatTrack<T>` and implement `Get`, which reads the value, and `Set`, which writes it. `T` is the component the track animates.

```csharp
using KeyTweening;
using UnityEngine.Scripting;
using UnityEngine.UI;

[Preserve]
public sealed class ScrollbarSizeTrack : KeyTweenFloatTrack<Scrollbar>
{
    protected override float Get(Scrollbar target) => target.size;
    protected override void Set(Scrollbar target, float value) => target.size = value;
}
```

* The class must be `public` and have a parameterless constructor.
* Put it in a runtime assembly, not in an Editor folder, because it must also play in builds.
* The track's name is the class name without `Track`. The example above shows as `Scrollbar Size`.

### Value Types

Derive from the class that matches the value. The key inspector shows a matching field.

| Base class | Value | Key field |
|---|---|---|
| `KeyTweenFloatTrack<T>` | `float` | number |
| `KeyTweenVector2Track<T>` | `Vector2` | x, y |
| `KeyTweenVector3Track<T>` | `Vector3` | x, y, z |
| `KeyTweenColorTrack<T>` | `Color` | color |
| `KeyTweenCustomTrack<T>` | `Vector4` | x, y, z, w |

### Changing the Name, Color and Conditions

Override these properties when you need to.

| Property | Use |
|---|---|
| `Name` | The name shown on the track and in the Add Track window |
| `TrackColor` | The track's color in the timeline |
| `Range` | The slider range of a number track (e.g. `new Vector2(0, 1)`) |
| `Offers(Component)` | Which objects show this track (e.g. only with a certain setting) |

```csharp
public override string Name => "Handle Size";
public override Vector2 Range => new(0f, 1f);
```

### Preview and Recording

When the preview stops, the values a track changed are written back to what they were before the preview. If you list the serialized fields the value is stored in, recording also puts keys on that track.

```csharp
public override void SerializedFields(Component target, List<(Object owner, string path)> fields) =>
    fields.Add((target, "m_Size"));
```

### When a Build Strips Code

A track is created from its saved class name. With IL2CPP and a Managed Stripping Level above Minimal, classes that no code uses directly can be removed. Mark the class with `[Preserve]` to keep it.

### When a Class Is Renamed

A track is saved with the full name of its class (namespace and assembly included).

* If you rename the class or its namespace, the track shows as `(missing)` and writes nothing.
* Rename it back, or add the track again.
