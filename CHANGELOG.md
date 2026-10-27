# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.1.3] - 2026-10-27

### Changed
- README: the install URL without a tag follows the latest release (update with **Update** in the Package Manager); a new section explains installing a specific version (`#v0.1.3`), which **Update** does not move.

## [0.1.2] - 2026-10-27

Simpler play API. Code that passed `KeyTweenInterrupt` or called `PlayState` needs a small change (below).

### Changed
- A state that is played takes the properties it animates from states already playing, where they are, and continues from there; a state left with no properties stops (its `PlayAsync` is canceled). This was the default (`KeyTweenInterrupt.Cancel`) and is now the only behavior.
- `Play(string)` and `PlayReversed(string)` take one argument, so they are listed for UnityEvents such as a Button's OnClick. `Play(string, PointerEventData)` passes the pointer for tracks that start from it.

### Removed
- `KeyTweenInterrupt`, the `interrupt` parameter of `Play`, `PlayAsync`, `PlayReversed`, `PlayReversedAsync`, `PlayUniTaskAsync`, `PlayReversedUniTaskAsync` and `PlayAsObservable`, and the Interrupt setting of Play On. To finish a state before the next one, call `Complete("state")` first: its end pose, its remaining events, its await completing.
- `PlayState` and `PlayStateReversed`: use `Play` and `PlayReversed`. UnityEvents saved with the old names must be picked again (the samples are updated).

## [0.1.1] - 2026-10-27

### Fixed
- Importing a sample into a project without TMP Essential Resources left every label invisible. KeyTween now imports them automatically when a sample is imported into such a project (the same as Window › TextMeshPro › Import TMP Essential Resources).

## [0.1.0] - 2026-10-23

First public release.

### Added
- `KeyTween` component: keyframe states stored in the prefab (no clip or controller assets), each a set of tracks on the prefab's own components.
- KeyTween Editor window: state tabs, timeline with tracks and keys, Add Track window, key inspector, recording (●), animation event and sound tracks, child prefab state calls.
- Live preview in Edit, Prefab and Play mode; values return when the preview stops.
- Saving: Auto Save and Save (Ctrl+S); edits made on running copies in Play mode are written back to the source prefab.
- Tracks: scale, rotation, position, size, anchors, pivot, active, color, alpha, CanvasGroup alpha, fill, camera, TMP font size, character spacing, typewriter reveal, number count-up, and per-character scale, position, rotation, color and alpha.
- Arrival: 36 eases, AnimationCurve, Punch and Shake.
- Nested prefabs: a parent state calls a child prefab's state and overrides its tracks in the current prefab only.
- Play On triggers: OnEnable, Start, pointer enter/exit/down/up, click, select/deselect, toggle on/off.
- API: `Play`, `PlayAsync` (Unity `Awaitable`), `PlayReversed`, `Complete`, `Stop`, `Sample`, `SetEndValue`, animation events.
- Scroll list: virtualized list, grid, masonry, carousel, infinite loop, pages, banners, wheel picker, section headers, chat and accordion layouts.
- Screen transitions: Page, Modal and Sheet containers with lifecycle callbacks and shared element transitions.
- Performance: no cost while idle, no GC allocation while playing, eased tracks evaluated in batches with Burst.
- Optional integrations, enabled when installed: UniTask, R3, UIEffect.
- Samples: RPG Sample, UI Components.
