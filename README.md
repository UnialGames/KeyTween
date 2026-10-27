# KeyTween

Keyframe UI animation for Unity uGUI. Author animations on a timeline, store them in the prefab, and play them by name.

[![Unity 6000.0+](https://img.shields.io/badge/unity-6000.0%2B-000.svg)](https://unity.com/releases/editor/archive)
[![Release](https://img.shields.io/github/v/release/UnialGames/KeyTween)](https://github.com/UnialGames/KeyTween/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

[Document](https://unialgames.github.io/KeyTween/) · [한국어 README](README_KO.md)

<img src="Docs~/media/rpg/rpg-summon.gif" width="640" alt="RPG sample: summon">

## Overview

KeyTween lets you build UI animation the way you build an Animator clip: by placing keys on a timeline. The animation is stored **inside the prefab** as named states such as `Hover`, `Open` and `Close`, so there are no clip or controller assets.

You preview animations without entering Play mode and choose when each state plays without writing code. When code is needed, one line plays a state and awaits it.

Videos: [RPG Sample](https://youtu.be/79Aw-O2kEgA) · [Button hover in the editor](https://youtu.be/13JJNE4_FwI) · [Overriding a button inside a popup](https://youtu.be/rXO_kNexiGc)

## Documentation

* [Installation](https://unialgames.github.io/KeyTween/articles/en/installation.html): requirements, Package Manager install, pinning a version, importing samples
* [Quick Start](https://unialgames.github.io/KeyTween/articles/en/quick-start.html): your first hover animation in the editor, and playing it from code
* [Editor](https://unialgames.github.io/KeyTween/articles/en/concepts.html): the KeyTween window, states, tracks and keys, Play On, nested prefabs, preview, saving, recording
* [Scripting](https://unialgames.github.io/KeyTween/articles/en/playing-and-awaiting.html): playing and awaiting states, runtime end values, animation events
* [Scroll List](https://unialgames.github.io/KeyTween/articles/en/scroll-list.html): a virtualized list with item animations and helper components
* [Screen Transitions](https://unialgames.github.io/KeyTween/articles/en/screens.html): page, modal and sheet containers, lifecycle, shared elements
* [Integrations](https://unialgames.github.io/KeyTween/articles/en/unitask.html): UniTask, R3, UIEffect
* [Performance](https://unialgames.github.io/KeyTween/articles/en/performance.html): measurements against other tools, and what to know when using it
* [API Reference](https://unialgames.github.io/KeyTween/api/KeyTweening.html): every public type and member, from the code's XML comments

## Features

* Timeline editor window, Add Track picker, key inspector and recording
* Preview in Edit mode, Prefab mode and Play mode
* States stored in the prefab, tracks that point straight at its components
* Play On: playing a state on pointer enter, click, enable and more, without code
* One-line playback with `await anim.PlayAsync("Open")` (Unity `Awaitable`) and clear rules for unfinished plays
* Nested prefabs: calling a child prefab's state, overriding its tracks only inside the parent
* TextMeshPro text effects: typewriter, number count, per-character tracks
* Scroll list and screen transition (page, modal, sheet) components
* Optional integrations: UniTask, R3, UIEffect
* Zero cost while idle, zero GC while playing, ease tracks evaluated with Burst

## Installation

This section explains how to install KeyTween in your project.

### Requirements

* Unity 6000.0 or later
* Installed along with KeyTween
  * `com.unity.ugui` 2.0.0 (includes TextMeshPro)
  * `com.unity.mathematics` 1.3.2
  * `com.unity.burst` 1.8.25

### Install via Package Manager (Recommended)

Install KeyTween from its Git URL in the Package Manager.

1. Open **Window > Package Manager**.
2. Click the **+** button and select **Add package from git URL...**.
3. Enter the following URL:

```text
https://github.com/UnialGames/KeyTween.git
```

Alternatively, add the following line to the `dependencies` block of `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unialgames.keytween": "https://github.com/UnialGames/KeyTween.git"
  }
}
```

### Updating

The URL above always points to the latest release. When a new version is out, select KeyTween in the Package Manager and click **Update**.

### Installing a specific version

Add `#` and a version tag to the URL to stay on that version. All versions are listed on the [Releases](https://github.com/UnialGames/KeyTween/releases) page.

```text
https://github.com/UnialGames/KeyTween.git#v0.1.3
```

* A pinned version does not change when you click **Update**.
* To change the version, edit the tag in `Packages/manifest.json`.
* To follow the latest release again, remove `#v…` from the URL.

### Importing samples

1. Select KeyTween in the Package Manager.
2. Open the **Samples** tab and click **Import** next to a sample.

Samples are copied into your project. After updating the package, import them again to get the new version.

## Getting Started

A *state* is a named animation, such as `Hover`, stored in the `KeyTween` component of a prefab.

### Making a hover effect in the editor

This button grows to 1.1× and becomes lighter when the pointer enters it, with no code.

1. Open the button prefab, then open **Window > KeyTween Editor**.
2. Click **Add KeyTween to 'Button'**.
3. Click **+** in the state tabs and name the new state `Hover`.
4. Right-click the timeline, choose **Add Track...**, pick **Scale** and **Color**, and click **Add 2**.
5. Set the Scale end key to `1.1` and the Color end key to a lighter color.
6. Drag the ruler or press **▶** to preview.
7. Set the state's **Play On** to `PointerEnter`.

![The KeyTween window with the Hover and Unhover states](Docs~/media/editor/editor-window.png)

For details on each step, see [Adding Tracks](https://unialgames.github.io/KeyTween/articles/en/adding-tracks.html), [Editing Keys](https://unialgames.github.io/KeyTween/articles/en/editing-keys.html) and [Play On](https://unialgames.github.io/KeyTween/articles/en/play-on.html).

### Playing a state from code

Reference the `KeyTween` component and play a state by name.

```csharp
using System;
using KeyTweening;
using UnityEngine;

public class Popup : MonoBehaviour
{
    [SerializeField] KeyTween anim;

    public async Awaitable Open() => await anim.PlayAsync("Open");

    public async Awaitable Close()
    {
        try { await anim.PlayAsync("Close", cancellationToken: destroyCancellationToken); }
        catch (OperationCanceledException) { return; }   // reopened while closing: keep the popup
        Destroy(gameObject);
    }
}
```

For details, see [Playing and Awaiting](https://unialgames.github.io/KeyTween/articles/en/playing-and-awaiting.html).

## Samples

Import a sample from the **Samples** tab of KeyTween in the Package Manager. Both need the Input System.

* **RPG Sample**: a complete mobile RPG UI. Every motion is a state in a prefab.
* **UI Components**: buttons, hover, toggles, a popup, text effects and 18 scroll list examples.

| Heroes → detail → upgrade | Adventure → battle → victory |
|---|---|
| ![Heroes](Docs~/media/rpg/rpg-heroes.gif) | ![Battle](Docs~/media/rpg/rpg-battle.gif) |
| **Ranking: a long list** | **Mail and daily rewards** |
| ![Ranking](Docs~/media/rpg/rpg-ranking.gif) | ![Mail](Docs~/media/rpg/rpg-mail.gif) |

## Performance

IL2CPP release build, 2,000 objects. In parentheses: the time above setting the same values directly from a script.

| | KeyTween | DOTween Pro | LitMotion | Animator | AnimationUI |
|---|---:|---:|---:|---:|---:|
| Looping pulse (ms) | 7.69 (+0.38) | 7.71 (+0.40) | 7.66 (+0.34) | 10.16 (+2.85) | 9.31 (+2.00) |
| List entry (ms) | 1.70 (+0.04) | 1.59 (−0.06) | 1.53 (−0.13) | 5.23 (+3.57) | 2.18 (+0.52) |
| Text wave, 100 objects (ms) | **0.61** | 2.11 | 2.05 | not supported | not supported |

For details, see [Performance](https://unialgames.github.io/KeyTween/articles/en/performance.html).

## License

* KeyTween: [MIT License](LICENSE.md)
* RPG sample icons: [game-icons.net](https://game-icons.net), CC BY 3.0 (authors in `Samples/RPG/Art/CREDITS.md`)
* Sample font: Passion One, SIL OFL 1.1
