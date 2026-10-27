# Optional Integrations — UniTask · R3 · UIEffect

The KeyTween package works without any third-party library. When one of the libraries below **is installed** in the project, its integration code compiles automatically. When it is not installed, the integration code is left out of the build, and there is nothing to configure.

This is the same approach that LitMotion and VContainer take for their UniTask integrations.

| Library | Turned on when (asmdef versionDefines) | Adds |
|---|---|---|
| UniTask | `com.cysharp.unitask` 2.0.0 or later → `KEYTWEEN_UNITASK` | 3 extension methods |
| R3 | `com.cysharp.r3` 1.0.0 or later → `KEYTWEEN_R3` | 3 extension methods |
| UIEffect | `com.coffee.ui-effect` 5.0.0 or later → `KEYTWEEN_UIEFFECT` | 14 tracks (assembly `KeyTween.UIEffect`) |

## UniTask

When UniTask (`com.cysharp.unitask` 2.0.0 or later) is installed, KeyTween gains extension methods for awaiting plays and events as UniTasks.

| Method | Description |
|---|---|
| `PlayUniTaskAsync(stateName, ct)` | The UniTask version of `PlayAsync`. |
| `PlayReversedUniTaskAsync(stateName, ct)` | Plays the state reversed. |
| `WaitEventUniTaskAsync(eventName, ct)` | Waits until the next animation event is raised. |

### Awaiting Several Plays Together

When opening a popup, use `WhenAll` to play the popup and its backdrop together.

```csharp
await UniTask.WhenAll(popup.PlayUniTaskAsync("Open"), dim.PlayUniTaskAsync("Show"));
```

The completion and cancellation rules are the same as for `PlayAsync` ([API](API.md#how-a-play-ends)). Internally, the `Awaitable` is converted with `AsUniTask()`.

### Receiving a Cancel as a Value

When closing a popup, use `SuppressCancellationThrow` to receive a cancel as a value instead of an exception.

```csharp
var canceled = await popup.PlayUniTaskAsync("Close").SuppressCancellationThrow();
```

### Setting a Time Limit

For a slow state, apply `Timeout`, then call `Complete` when the time runs out.

```csharp
try { await anim.PlayUniTaskAsync("Intro").Timeout(TimeSpan.FromSeconds(3)); }
catch (TimeoutException) { anim.Complete("Intro"); }
```

### Waiting for an Event

Use `WaitEventUniTaskAsync` to wait for an animation event.

```csharp
var info = await anim.WaitEventUniTaskAsync("Shown");
```

### Cleaning Up with the Object

In every case, pass `destroyCancellationToken` so that the work is cleaned up when the object is destroyed.

```csharp
await popup.PlayUniTaskAsync("Open", destroyCancellationToken);
```

## R3

When R3 (`com.cysharp.r3` 1.0.0 or later) is installed, KeyTween gains extension methods that expose plays and animation events as observables.

| Method | Description |
|---|---|
| `PlayAsObservable(stateName)` | Starts playing on subscribe, emits one `Unit` when the play ends, and completes. |
| `EventsAsObservable()` | Emits every animation event. |
| `EventAsObservable(eventName)` | Emits only the events with that name. |

### Installing R3

R3 needs both the Unity package and the core R3 DLL.

1. In the Package Manager, install the Unity package from the following Git URL.

   ```text
   https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity
   ```

2. Install the core R3 DLL from NuGet. You can use a tool such as NuGetForUnity.

### Observing the End of a Play

`PlayAsObservable` emits one `Unit` when the play ends. Block repeated clicks with `ThrottleFirst`.

```csharp
open.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5))
    .SelectMany(_ => popup.PlayAsObservable("Open"))
    .Subscribe(_ => Debug.Log("opened")).AddTo(this);
```

* If the play cannot finish, it completes with `OperationCanceledException` ([API](API.md#how-a-play-ends)).
* Disposing the subscription stops the play where it is.

### Keeping Only the Last Play

For a close button, use `Switch` to keep only the last play.

```csharp
close.OnClickAsObservable()
    .Select(_ => popup.PlayAsObservable("Close"))
    .Switch()
    .Subscribe().AddTo(this);
```

### Observing Events

`EventAsObservable` emits only the events with that name. To receive every event, use `EventsAsObservable`.

```csharp
popup.EventAsObservable("Shown").Subscribe(e => Debug.Log(e.Time)).AddTo(this);
```

## UIEffect

When UIEffect (`com.coffee.ui-effect` 5.x) is installed, the Add Track window shows **14 UIEffect tracks** for any object that has UIEffect. The tracks are named `UIEffect · …` and match the Timeline tracks that UIEffect provides.

### Added Tracks

| Track | Range |
|---|---|
| Tone Intensity, Color Intensity, Sampling Intensity, Transition Rate (dissolve etc.), Edge Shiny Rate, Gradation Intensity, Detail Intensity | 0 ~ 1 |
| Gradation Offset | −1 ~ 1 |
| Gradation Rotation | 0 ~ 360 |
| Gradation Scale | 0.01 ~ 10 |
| Color, Transition Color, Edge Color, Detail Color | color |

### Editing the Tracks

Like any other track, these tracks support the following.

* Keys
* Eases
* Start (Current etc.)
* Preview
* Reversed play
* Looping

Number tracks are edited with a slider for their range, and color tracks with a color field.

### If UIEffect Is Removed

If UIEffect is removed, these tracks no longer appear in the list. Tracks that were already saved do nothing, and no errors occur.

### How the Integration Works

The core assembly does not reference UIEffect.

* `KeyTweenProperty` holds only the property names.
* The integration assembly registers reading, writing and ranges with `KeyTweenExternalProperties.Register`.

Other libraries can be integrated the same way. For details, see Design.

## Checking in the Development Repository

The integration code is checked two ways: without the libraries, and with them installed.

### The main Branch

These libraries are not installed on the `main` branch. So CI checks that the package compiles without the integration code.

### Integration Branches

Tests with the libraries installed run on the following branches.

* `integration/unitask`
* `integration/r3`
* `integration/uieffect`

These branches follow new library versions and changes on `main` automatically every day. For details, see Development.
