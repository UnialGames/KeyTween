#if KEYTWEEN_R3
using System;
using System.Threading;
using R3;
using UnityEngine;

namespace KeyTweening
{
    /// <summary>
    /// R3 integration, compiled only when R3 (com.cysharp.r3) is installed: states and animation events as Observables,
    /// so they compose with the rest of a reactive UI (button clicks, model changes, throttling …).
    /// </summary>
    public static class KeyTweenR3Extensions
    {
        /// <summary>
        /// Plays the state when subscribed: one Unit when it reaches its end, then completes. It completes with an error
        /// (OperationCanceledException) when the state does not finish (taken over, played again, object disabled).
        /// Disposing the subscription stops the state where it is.
        /// </summary>
        public static Observable<Unit> PlayAsObservable(this KeyTween animation, string stateName) =>
            Play((animation, stateName), static (s, token) => s.animation.PlayAsync(s.stateName, token));

        /// <summary><see cref="PlayAsObservable(KeyTween, string)"/> from a given time (seconds on the state's timeline).</summary>
        public static Observable<Unit> PlayAsObservable(this KeyTween animation, string stateName, float time) =>
            Play((animation, stateName, time), static (s, token) => s.animation.PlayAsync(s.stateName, s.time, token));

        /// <summary><see cref="PlayAsObservable(KeyTween, string)"/> for a play set up with With.</summary>
        public static Observable<Unit> PlayAsObservable(this KeyTweenPlay play) =>
            Play(play, static (p, token) => p.PlayAsync(token));

        static Observable<Unit> Play<T>(T state, Func<T, CancellationToken, Awaitable> play) =>
            Observable.Create<Unit, (T, Func<T, CancellationToken, Awaitable>)>((state, play), static (observer, s) =>
            {
                var cancel = new CancellationTokenSource();
                Run(observer, s.Item2(s.Item1, cancel.Token), cancel.Token);
                return Disposable.Create(cancel, static c => { c.Cancel(); c.Dispose(); });
            });

        static async void Run(Observer<Unit> observer, Awaitable play, CancellationToken token)
        {
            try
            {
                await play;
                observer.OnNext(Unit.Default);
                observer.OnCompleted();
            }
            catch (OperationCanceledException e)
            {
                if (!token.IsCancellationRequested) observer.OnCompleted(Result.Failure(e));   // not our own dispose
            }
        }

        /// <summary>Every animation event raised by any state of this animation, while subscribed.</summary>
        public static Observable<KeyTweenEventInfo> EventsAsObservable(this KeyTween animation) =>
            Observable.Create<KeyTweenEventInfo, KeyTween>(animation, static (observer, a) =>
            {
                void OnEvent(in KeyTweenEventInfo e) => observer.OnNext(e);
                a.EventRaised += OnEvent;
                return Disposable.Create(() => { if (a != null) a.EventRaised -= OnEvent; });
            });

        /// <summary>The name of each state that starts playing (<see cref="KeyTween.StateStarted"/>), while subscribed.</summary>
        public static Observable<string> StateStartedAsObservable(this KeyTween animation) =>
            Observable.FromEvent<string>(h => animation.StateStarted += h, h => animation.StateStarted -= h);

        /// <summary>The name of each state that reaches its end or is completed (<see cref="KeyTween.StateCompleted"/>).</summary>
        public static Observable<string> StateCompletedAsObservable(this KeyTween animation) =>
            Observable.FromEvent<string>(h => animation.StateCompleted += h, h => animation.StateCompleted -= h);

        /// <summary>The name of each state that does not reach its end (<see cref="KeyTween.StateCanceled"/>).</summary>
        public static Observable<string> StateCanceledAsObservable(this KeyTween animation) =>
            Observable.FromEvent<string>(h => animation.StateCanceled += h, h => animation.StateCanceled -= h);

        /// <summary>The animation events of one name (e.g. "Shown").</summary>
        public static Observable<KeyTweenEventInfo> EventAsObservable(this KeyTween animation, string eventName) =>
            animation.EventsAsObservable().Where(eventName, static (e, name) => e.Name == name);
    }
}
#endif
