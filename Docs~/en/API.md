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
    public void Play(string stateName, float time);  // plays from that time (seconds)
    public Awaitable PlayAsync(string stateName, CancellationToken cancellationToken = default);
    public Awaitable PlayAsync(string stateName, float time, CancellationToken cancellationToken = default);
    public void PlayReversed(string stateName);      // also listed in UnityEvents
    public void PlayReversed(string stateName, float time);   // from that time back to 0 s
    public Awaitable PlayReversedAsync(string stateName, CancellationToken cancellationToken = default);
    public Awaitable PlayReversedAsync(string stateName, float time, CancellationToken cancellationToken = default);

    public void Complete(string stateName);         // jumps a playing state to its end
    public void Stop(string stateName);             // stops the state where it is
    public void Stop(string stateName, float time); // stops it and shows the pose at that time (without playing)

    public KeyTweenPlay With(string stateName);             // sets up one play in code ("Setting Up a Play in Code")
    public KeyTweenStateBuilder Build(string stateName);    // makes a state in code ("Making a State in Code")

    public bool IsPlaying(string stateName);
    public bool HasState(string stateName);
    public float Duration(string stateName);        // length including overrides; 0 if there is no such state
}
```

| Method | Behavior |
|---|---|
| `Play` | Starts playing. With a time, plays from that time. |
| `PlayAsync` | Starts playing and waits until the play ends. |
| `PlayReversed` | Plays from the end (or the given time) back to the start. |
| `Complete` | Jumps a playing state to its end pose. |
| `Stop` | Stops the state where it is. With a time, stops it and shows the pose at that time. |

Times are in **seconds** on the state's timeline (the same unit as `Duration`). A time below 0 or past the length is clamped to 0 or the end.

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

### Playing from a Given Time

Pass a time to `Play` or `PlayAsync` to play from that time. The pose at that time is written at once, and only the rest of the state plays.

```csharp
anim.Play("Intro", 1.5f);                     // from 1.5 s
await anim.PlayAsync("Intro", 1.5f, destroyCancellationToken);
```

* Events, sounds and child calls before that time are not raised. Those exactly at the start time are raised, as those at 0 s are when playing from 0 s.
* Pass a time to `PlayReversed` or `PlayReversedAsync` to play from that time back toward 0 s.

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

### Stopping at a Given Time

Pass a time to `Stop` to stop the state if it is playing (a pending `PlayAsync` is canceled) and write its pose at that time. It writes the pose even when the state is not playing.

```csharp
anim.Stop("Open", anim.Duration("Open"));                     // straight to the end pose, without animation
anim.Stop("Reveal", progress * anim.Duration("Reveal"));      // every frame from a value such as a scroll position (progress: 0 to 1)
```

* No timer runs, and no events or sounds are raised.
* You can call it every frame to drive the pose with something other than time, such as a scroll position or a pull distance.
* The old name `Sample(state, time)` still does the same but is `[Obsolete]` and will be removed in the next version.

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

| Case | `PlayAsync` result | Callback | Remaining events | Remaining sounds |
|---|---|---|---|---|
| Reaches the end | completes | On Complete | all raised | all played |
| `Complete` called | completes | On Complete | raised in order | not played |
| Another state takes all of its properties | canceled | On Cancel | not raised | not played |
| The same state is played again | earlier play canceled | the earlier play's On Cancel | not raised | not played |
| Object disabled | canceled | On Cancel | not raised | not played |
| `Stop` called, token canceled | canceled | On Cancel | not raised | not played |

A play that does not reach its end ends as **canceled**. This makes "continue only when the play really finished" the default behavior.

### Start and End Callbacks

You can receive the start and end of a play without `await`. Connect them per state in **Callbacks** in the editor (On Start, On Complete, On Cancel, see the [Guide](Guide.md#calling-functions-at-the-start-and-end-callbacks)), or receive every state through the component's events in code.

```csharp
public event Action<string> StateStarted;     // the state's name. After the first pose, before the events at the start time
public event Action<string> StateCompleted;   // reached the end, or Complete. Before PlayAsync completes
public event Action<string> StateCanceled;    // did not reach the end (the table above)
```

```csharp
anim.StateCompleted += state => { if (state == "Close") gameObject.SetActive(false); };
```

* Each play starts with `StateStarted` and ends with **exactly one** of `StateCompleted` and `StateCanceled`.
* At the same moment, the state's UnityEvent (On Start and so on) is called first, then the component's event.
* A callback that throws is logged, and the play and the other callbacks go on.
* Stopping the state inside its On Start means the play does not start, and its `PlayAsync` is canceled.
* `Stop(state, time)`, which writes a pose without playing, and the editor preview call no callbacks.

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

## Setting Up a Play in Code (With)

Make the motion in the editor, and set in code only what changes from play to play: the target number of a currency or power rating, a different arrival for the situation, what to do when it finishes.

Set up a play with `With(state)`, then play it with one of the `Play` methods. The calls chain like LitMotion's `WithEase`.

```csharp
// "Count" state on the gold label: a Number (Count) track, Start = Current Value, 0.6 s OutQuad (made in the editor)
count.With("Count").WithEndValue(goldLabel, KeyTweenProperty.TextNumber, gold).Play();

// Opening a popup: arrive with OutBack this time, and take input once it is open
await popup.With("Open")
    .WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack)
    .WithOnComplete(EnableInput)
    .PlayAsync(destroyCancellationToken);
```

### What You Can Change

The track is found by state name, target and property, and what changes is its **last key** (the end value and the segment that arrives there).

| Method | Changes |
|---|---|
| `WithEndValue(target, property, value)` | The last key's value |
| `WithEase(target, property, ease)` | The last key's arrival becomes Ease, with that ease |
| `WithCurve(target, property, curve)` | The arrival becomes Curve, with that AnimationCurve |
| `WithPunch(target, property, frequency, damping)`, `WithShake(...)` | The arrival becomes Punch / Shake, with those values |
| `WithTime(time)` | Plays from that time in seconds (the same as `Play(state, time)`) |
| `WithEventsBefore()` | With `WithTime`: the animation events before that time are raised too, in order, at the start (sounds and child calls are skipped) |
| `WithOnStart`, `WithOnComplete`, `WithOnCancel` | Callbacks for this play only, called after the state's On Start and so on |
| `WithOnLoop` | For a looping state, each time a lap ends |

* A Material track is found by its shader property name, as in `WithEase(renderer, "_BaseColor", ease)`, and a custom track by its class, as in `WithEase<MyTrack>(target, ease)`.
* A value is interpreted exactly like a key value, so the Relative To Rest and Size Relative settings apply.
* If no track matches, it logs a warning and skips that change. The state still plays.

### For That Play Only

The changes go in when the play starts and are put back when it ends, whether it completes or is canceled. The next `Play` uses the values set in the editor. The saved data is not changed.

You can keep what `With` returns and play it again as often as you like. Reused, it does not allocate when it plays.

```csharp
KeyTweenPlay _open;

void Awake() => _open = popup.With("Open").WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack);
public void Show() => _open.Play();
```

### End Values for States Played Elsewhere

You cannot use `With` for a state that something other than your code plays, such as Play On or a screen transition. Set its end value beforehand with `SetEndValue` instead. The value then applies to every later play.

```csharp
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, Vector4 value);
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, float value);   // sets x only
```

```csharp
// Result screen: when the screen transition plays "Enter", the gold counts up to this number
result.SetEndValue("Enter", goldLabel, KeyTweenProperty.TextNumber, reward);
```

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

## Making a State in Code (Build)

States are usually made in the editor, but you can also make one in code alone. Use this when the targets are known only at runtime, or for a small animation you do not want to make in the editor.

Stack tracks and keys with `Build(state)`, then add the state with `Add`, or add and play it with one of the `Play` methods.

```csharp
anim.Build("Pop")
    .Track(button.transform, KeyTweenProperty.Scale)       // starts a track; the Keys until the next Track go on it
        .Key(0f, Vector3.one)
        .Key(0.15f, Vector3.one * 1.2f, KeyTweenEase.OutBack)  // the segment arriving at this key uses OutBack
        .Key(0.3f, Vector3.one, KeyTweenEase.InOutSine)
    .Track(label, KeyTweenProperty.GraphicAlpha)
        .Key(0f, 0f).Key(0.2f, 1f)
    .Event(0.15f, "Peak")
    .OnComplete(() => Debug.Log("popped"))
    .Play();
```

| Method | What it does |
|---|---|
| `Track(target, property)` | Starts a track of a built-in property. A Material track is `Track(renderer, "_BaseColor")`, a custom track `Track<MyTrack>(target)` |
| `From(start)` | The track's Start. Key Value (the first key) by default; Current Value (the value when it plays) or Pointer |
| `Key(time, value, ease)` | One key. `ease` is how the segment arriving at this key moves (OutSine by default, as in the editor). The value is a `Vector4` or a `float`; a `Vector3`, `Vector2` or `Color` works too |
| `KeyCurve`, `KeyPunch`, `KeyShake` | A key whose arrival is Curve, Punch or Shake |
| `Event(time, name)`, `Sound(time, clip, volume)` | An animation event and a sound |
| `Loop()` | Makes it a looping state |
| `OnStart`, `OnComplete`, `OnCancel` | The state's callbacks (every play of it) |
| `Add()` | Adds the state. Then play it by name, as in `anim.Play("Pop")` |
| `Play()`, `PlayAsync()`, `PlayReversed()`, `PlayReversedAsync()` | Adds it and plays it |
| `With()` | Adds it and goes on to set up a play (`With`) |

* The state exists only at runtime. It is not saved, and you can use `Play`, `Stop`, `Complete` and `With` with it like a state made in the editor.
* Keys are placed in time order whatever order you add them in.
* A state with the same name is replaced. If that state is playing, it stops and its pending `PlayAsync` is canceled.
* Calling `Key` before `Track` throws right away.

### Adding a State Object

You can also make a `KeyTweenState` yourself and add it with `AddState`. `Build` uses this method, and so do screen containers when they add their default transitions ([Screens](Screens.md)).

```csharp
public void AddState(KeyTweenState state);
```

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
