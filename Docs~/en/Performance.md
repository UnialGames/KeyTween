# Performance

This page shows how KeyTween performs compared with Animator, DOTween and LitMotion, and what to keep in mind when you use it. The values were measured on 2026-10-15~16.

* All result tables: [`Benchmarks.md`](Benchmarks.md)

## 1. Conclusion

This section sums up the results measured with an IL2CPP release build and 2,000 objects.

### Results

The values in parentheses show the time above setting the same values directly from a script (the baseline), which is the cost of the method itself.

| | KeyTween | DOTween Pro | LitMotion | Animator | AnimationUI |
|---|---:|---:|---:|---:|---:|
| Looping pulse (ms) | 7.69 (+0.38) | 7.71 (+0.40) | **7.66 (+0.34)** | 10.16 (+2.85) | 9.31 (+2.00) |
| List entry (ms) | 1.70 (+0.04) | 1.59 (−0.06) | **1.53 (−0.13)** | 5.23 (+3.57) | 2.18 (+0.52) |
| Text wave, 100 objects (ms) | **0.61** | 2.11 | 2.05 | not supported | not supported |
| GC per frame (vs. baseline) | **0** | pulse 0, entry +11KB | **0** | +0.1KB | +16KB |

These values come from a single run, so each case varies by about ±5%. Differences of less than 0.1ms between methods are within that range.

### Strengths

* **Faster than Animator and AnimationUI.** With 2,000 pulses, KeyTween's own cost is about 1/5 to 1/7 of theirs (+0.38ms vs. +2.0~2.9ms).
* **On par with code tweens.** With 2,000 pulses, KeyTween is within 0.05ms of DOTween Pro (+0.40) and LitMotion (+0.34). Code tweens are libraries such as DOTween and LitMotion, in which effects are written in code.
* **Text effects are the fastest.** With 100 texts, KeyTween takes about 1/3 of the time of DOTween Pro and LitMotion.
* **No GC allocation during playback.** By contrast, DOTween allocates on every replay (11KB per frame for 2,000 list entries). AnimationUI also allocates every time because of its coroutines.

### Weak Spot: Starting a Play

This is the time it takes to restart 2,000 objects at once in list entry.

* KeyTween: 2.31ms
* LitMotion: 1.34ms
* DOTween: 3.89ms

It only becomes noticeable when thousands of plays start in the same frame. For details, see [The Cost of Starting a Play](#the-cost-of-starting-a-play) below.

### Why List Entry Shows Negative Values

The negative values in list entry, which are faster than the baseline, come from how the measurement works. The baseline script keeps setting values every frame after the effect ends, while the libraries stop after 0.4 s. During that period the baseline does more work, so values near 0 should be read as "no difference".

## 2. Method

All methods were compared under the same conditions.

### How Methods Are Compared

The same effect is applied to the same objects (N images, N texts) with each method.

* Looping pulse
* List entry (played again every 0.5 s)
* Text wave

The baseline (direct set) uses no animation system: one script sets the same values every frame. uGUI's redraw cost is the same in every method, so the difference from the baseline is the cost of each method.

### Environment

* Windows 64-bit player, 1280×720 windowed
* VSync off, no frame limit
* For each case, a 1 s warm-up, then every frame recorded for 4 s
* CPU: AMD Ryzen 7 7800X3D
* Unity: 6000.6.3f1

### Time and GC

* **Time**: frame times of release builds (Mono, IL2CPP)
* **GC**: measured separately with a development build. The allocation counter (`GC Allocated In Frame`) works only in development builds.

## 3. What to Know When Using It

These points affect performance on screens that use many KeyTweens.

### Cost When Not Playing

A KeyTween that is not playing does nothing each frame. Even with many KeyTweens on screen, only the ones that are playing cost anything.

### GC Allocation While Playing

Playing allocates no GC memory. This holds for looping states and for playing the same state again and again, and for states with child calls.

The exception is a state that calls a child state whose tracks an outer prefab overrides. Each time such a state plays, working out its length merges the overridden child state anew.

### With and Build

`With(state)` and `Build(state)` make new objects each time they are called. So calling `With(...)` again for every play allocates that much.

* Keep the `KeyTweenPlay` that `With` returns in a field and play it again. Reused, it allocates nothing when it plays.
* Call `Build` once to make the state, then play it with `Play("name")`.

### Tracks Evaluated with Burst

Tracks whose keys all use ease presets are gathered and evaluated together, in one Burst call for all playing tracks. The following tracks are evaluated one by one in managed code, so they are a little slower:

* AnimationCurve keys
* Punch and Shake keys
* Per-character tracks
* Random-character (scramble) reveal

If you put the same effect on thousands of objects, building it with ease presets is faster. `com.unity.burst` is installed along with KeyTween for this.

### The Cost of Starting a Play

Starting a play packs its keys for the Burst evaluation. Playing 2,000 again in one frame takes 2.31ms on IL2CPP.

* On a normal screen this does not show.
* If you start thousands in the same frame, spread them over a few frames.

### Text Effects

A text with per-character tracks is not laid out again every frame; only the per-character changes are applied to the first layout. It is laid out again only when:

* The text, size, color or number of visible characters changes
* The number of characters changes
* The text box size changes

Changing the text often while it plays costs a layout each time.

### Allocation on First Use

The first time a KeyTween plays, it reserves arrays for the Burst evaluation once. For 2,000 states this is about 1.2–1.4MB; after that, playing allocates nothing.

The setup allocation in the [benchmarks](Benchmarks.md#setup-allocation) (about 6.6MB for 2,000 KeyTweens) is a different number, because that measurement includes making the states in code.
