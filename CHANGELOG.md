# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [0.2.1] - 2026-10-29

KeyTween Editor: copy and paste keys, state tabs you can rename, duplicate and reorder, stretch handles for selected keys. The runtime and playback are unchanged.

### Added
- KeyTween Editor: copy and paste keys (Ctrl+C / Ctrl+V on the timeline, or the context menu). Pasted at the playhead (or where you right-click, **Paste Keys Here**, also after the end) with their spacing, onto the track of the same object and kind (made when missing); in another prefab, onto the object at the same path from the root.
- KeyTween Editor: state tabs can be renamed (double-click), duplicated, reordered (drag, or Move Left / Right) and deleted from the tab (right-click). A state is not renamed to an empty name or one another state has.
- KeyTween Editor: selecting two or more keys at different times shows a box around them with a handle at each end. Dragging a handle stretches or shrinks the selected keys around the other end, like the Animation window.

### Removed
- KeyTween Editor: the key inspector's **Capture Current Value** button. Record (●) puts what you change in the scene into keys.

### Fixed
- KeyTween Editor: a box selection can start anywhere in the key area (the event and child call lanes, the empty space below the last track), like the Animation window. Before, it started only on a track's lane, so dragging from below the tracks did nothing.

## [0.2.0] - 2026-10-28

Tracks for game objects (not only UI), custom tracks, and playback that follows `Time.timeScale`. KeyTweens that must keep moving while the game is paused (timeScale 0), such as a pause menu, need **Time** set to `Unscaled` (see Changed).

### Added
- Documentation site: https://unialgames.github.io/KeyTween/ (the documents and an API reference). The Package Manager's **Documentation** link opens it, and the READMEs link to it.
- README and API document: awaiting several KeyTweens together (`PlayAsync` in turn, `Task.WhenAll`, `UniTask.WhenAll`).
- Tracks for objects that are not UI: Position, World Position, World Rotation (Transform), Color and Alpha (SpriteRenderer), Material tracks for a Renderer's color, number and vector properties (through a MaterialPropertyBlock, the material asset is not changed), Light (Color, Intensity, Range, Spot Angle), Camera (Field of View, Near Clip, Far Clip, Viewport Rect) and AudioSource (Volume, Pitch).
- UI tracks: Offset Min and Max (RectTransform), UV Rect (RawImage), Effect Color and Distance (Shadow, Outline), Softness and Padding (RectMask2D), Value (Slider), Scroll Position (ScrollRect), Min, Preferred and Flexible Size (LayoutElement).
- TextMeshPro tracks: Word Spacing, Line Spacing, Outline Width, Outline Color, Face Dilate, Underlay Color. Material tracks use an instance of the text's material; the editor preview puts the original back.
- Custom tracks: derive from `KeyTweenFloatTrack<T>` (or the Vector2, Vector3, Color and Vector4 versions) and implement `Get` and `Set`; the track appears in the Add Track window for objects with `T`.

### Changed
- **Playback now follows `Time.timeScale` by default**, like Animator, DOTween and LitMotion: pausing the game (timeScale 0) pauses KeyTween animations. Each KeyTween has **Time** (`timeMode`): `Scaled` (default) or `Unscaled` (real time, the old behavior, for pause menus and loading screens). It is a normal serialized field, so a prefab instance can override it. Existing prefabs load as `Scaled`; set the ones that must move while paused to `Unscaled`.
- When states overlap, a new state takes over the shared channels of another kind of track on the same value: an Alpha state takes only the a of a playing Color track, which keeps writing r, g, b; a Color state stops a playing Alpha track. The same holds for Char Color and Char Alpha, and Offset Min/Max take over Position and Size.
- The Add Track window offers Sound only on the root. A sound track still picks its AudioSource in its inspector (**Audio Source (optional)**).
- Recording puts a change on a track the state already has for that field: World Position, World Rotation, Offset Min/Max, a SpriteRenderer's Alpha, Scroll Position (moving the Content) and Number (the number in the text). Before, it made a second Position, Rotation, Size or Color track. Editing a text no longer records a Reveal key. Light, Camera, AudioSource, Shadow, LayoutElement and UIEffect values are recorded too.
- Each track kind is defined in one place (`KeyTweenTrackType`): reading, writing, the editor's field, preview restore and recording. `KeyTween.Read` and `KeyTween.Write` still work for built-in properties.

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
