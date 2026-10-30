# Authoring Compared

This page compares the steps needed to build the same effect with each tool. Every comparison is based on work actually done in this repository.

* `Benchmarks/Assets/Bench/*Driver.cs`: code that implements the same three effects (pulse, list entry and text wave) with each method
* `Assets/Reference`: recreations of the Unity-AnimationUI and UnityScreenNavigator demos
* `Assets/KeyTween/Samples/RPG`: the RPG sample

For the other tools, only the features used in this work are described. Features that were not tried are marked "Not checked". Performance is covered separately on the [Performance](Performance.md) page.

## 1. Making one effect — button pulse (scale + color, looping)

This section uses a pulse effect, in which a button's scale and color change repeatedly, to compare the authoring steps and the code needed to run it with each tool.

### At a Glance

| Tool | Made in | Code to run |
|---|---|---|
| **KeyTween** | KeyTween window | **0 lines** (1 line from code) |
| Animator | Animation window + Animator window | 0 lines (default state) |
| DOTween Pro | Inspector (`DOTweenAnimation`) or code | **2 lines** |
| LitMotion | Code | **2 statements (4 lines)** |
| AnimationUI | Inspector (`AnimationUI` sequence list) | **1 line** to loop |

### Making It with KeyTween

The effect is made in the KeyTween window.

1. Add `KeyTween` to the button.
2. Make a state "Pulse" and turn on **Loop**.
3. Pick Scale and Color in the Add Track picker.
4. Add keys at 0 / 0.5 / 1 s and set the ease to InOutSine.
5. Set **Play On** to OnEnable.

The state plays when the object is enabled, so the code to run is **0 lines**. From code, it takes 1 line.

```csharp
anim.Play("Pulse");
```

### States, Tracks and Keys in KeyTween

These are the three concepts used in the steps above.

* **State**: one named effect
* **Track**: a row in the state that changes a single value such as scale or color
* **Key**: the value at a point in time on a track

Creating states, adding tracks and editing keys all happen in the KeyTween window shown below.

![The KeyTween window, with the state tabs at the top, the track list on the left, keys on the timeline in the middle and the key inspector on the right.](../media/editor/editor-window.png)

### Making It with Animator

The effect is made in the Animation window and the Animator window.

1. Click **Create** in the Animation window. This creates 2 assets: a clip and a controller.
2. Record the keys with **Record**.
3. Check the clip's **Loop Time**.
4. Check the default state in the controller.

The default state plays on its own, so the code to run is 0 lines. To switch to another effect, use states and transitions, or call `Play`.

### Making It with DOTween Pro

The effect is made in the inspector (`DOTweenAnimation`) or in code.

* **Inspector**: Add one component per effect (1 for scale + 1 for color). Set Loops to −1 and Yoyo.
* **Code**: The **2 lines** below.

```csharp
DOScale(...).SetLoops(-1, Yoyo)
DOColor(...)...
```

### Making It with LitMotion

The effect is made in code only. You create one Motion per value and bind it to the target, and the whole pulse takes **2 statements (4 lines)**. The scale part looks like this.

```csharp
LMotion.Create(...).WithLoops(-1, Yoyo).BindToLocalScale(rt)
```

### Making It with AnimationUI

Add steps to the `AnimationUI` sequence list in the inspector.

1. Scale and color step
2. Wait
3. Step back
4. Wait

There is no loop feature, so looping takes **1 line** of code.

```csharp
OnAnimationEnded += Play
```

## 2. Work per effect

In the benchmark project, every method was implemented in code so that all of them could be measured under the same conditions. In real use, however, the difference that matters is whether an effect **can be made in the editor**.

### Where Each Effect Is Made

The table shows where and how each effect is made with each tool.

| Effect | KeyTween | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| Looping pulse | Editor (window) | Editor (window) | Inspector or 2 lines of code | 2 statements of code | Inspector (4-step sequence) + 1 line of loop code |
| List entry (current position → 0) | Editor, Start = Current Value | Editor, start value fixed in the clip | 2 lines of code (`From`) | 2 statements of code | Inspector |
| Text wave (per character) | **Editor** (Char track + Stagger) | Not possible | Code: `DOTweenTMPAnimator` + a loop over the characters | Code: a loop of `BindToTMPCharPositionY` per character | Not possible |
| Counting a number (currency) | Editor (Number track) + 1 line for the end value (`With(...).WithEndValue`) | Not checked | Code | Code | Not possible |
| Shared element transition | Editor (state) + 1 line for the partner's keys (`KeyTweenSharedElement`) | Not checked | Code | Code | Not possible |
| UIEffect values (dissolve etc.) | **Editor** (14 UIEffect tracks) | Editor (records serialized fields) | Code (`DOTween.To`) | Code (`Bind`) | Not possible |

## 3. Feature comparison (what was checked in this repository)

Only features checked directly in this repository are listed.

### Symbols in the Table

* ✅: supported
* ❌: not supported
* Not checked: a feature that was not tried

### Feature Table

| Feature | KeyTween | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| Keyframe editing in the editor | ✅ Timeline window | ✅ Animation window | Inspector values (not keyframes) | ❌ (separate packages not checked) | Sequence list (start, duration) |
| Recording | ✅ ● (Scene/Inspector changes → keys) | ✅ Record | ❌ | ❌ | ❌ |
| Preview in Edit mode | ✅ Play, scrub, loop | ✅ | Not checked | ❌ | Not checked |
| Keeping values changed in Play mode | ✅ Auto Save · Save | Edits in the Animation window are kept | ❌ | ❌ | Not checked |
| Start from the current value | ✅ Start = Current Value | ❌ (clip values) | ✅ when `From` is omitted | ✅ | ❌ |
| Reverse play | ✅ `PlayReversed` (a current-value start also returns to its original place) | Speed −1 (parameter) | ✅ `PlayBackwards` | Not checked | ✅ `PlayReversed` |
| Loop | ✅ State Loop | ✅ Loop Time | ✅ | ✅ | ❌ (Play again) |
| Per character | ✅ Editor | ❌ | Code | Code | ❌ |
| Event and sound tracks | ✅ | Events ✅ | Callbacks (code) | Callbacks (code) | ✅ Event and sound steps |
| Screen transition containers | ✅ Page, Modal, Sheet | ❌ | ❌ | ❌ | ❌ |
| await | ✅ `Awaitable`, UniTask optional | ❌ | ✅ (`AsyncWaitForCompletion`) | ✅ | ❌ |
| Required packages | Unity packages only (uGUI, Mathematics, Burst) | Built in | DOTween (Pro is paid) | LitMotion (+ Burst, Collections) | AnimationUI source |

## 4. Work when changing it

This section compares the work needed to change an effect that already exists.

### Work per Change

"Code tweens" refers to libraries such as DOTween and LitMotion, in which effects are written in code.

| What to change | KeyTween | Code tweens (DOTween, LitMotion) | Animator |
|---|---|---|---|
| Duration and ease | Drag keys or pick an ease in the window, preview at once | Edit code → compile → check in Play mode | Move keys in the Animation window (ease through curve tangents) |
| Adding an effect | Add one state | Add methods and fields | Add a clip, a state and transitions |
| Slightly different per prefab | Child state override (in the current prefab) | Branch in code or expose values | Override Controller |
| Designers do it themselves | ✅ No code | ❌ (partly possible with the DOTween Pro inspector) | ✅ |

## 5. Summary

This section sums up each tool and the performance comparison.

### KeyTween

You build effects in the KeyTween window without writing code, and you can preview them immediately. The following features used to require a code tween and are also available in the editor.

* Starting from the current value
* Per-character effects
* await

### Performance

The own cost of 2,000 pulses was compared in an IL2CPP build. The own cost is the time a method takes beyond setting the same values directly from a script.

* KeyTween: +0.38ms
* DOTween Pro: +0.40ms
* LitMotion: +0.34ms
* Animator: +2.85ms
* AnimationUI: +2.00ms

KeyTween is on par with DOTween Pro and LitMotion, and much lighter than Animator and AnimationUI.

* Text effects are the fastest: 0.61ms for 100 texts, compared with about 2.1ms for code tweens.
* KeyTween makes no GC allocations during playback.

For details, see [Performance](Performance.md).

### Code Tweens (LitMotion, DOTween)

LitMotion has the lowest cost for starting a play. However, because the effect lives in code, every preview or change requires a compile.

### Animator

You can author and record effects in the editor, but Animator is the heaviest option for UI effects. It lacks the following features.

* Per-character effects
* Starting from the current value
