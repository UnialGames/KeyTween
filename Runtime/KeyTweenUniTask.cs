#if KEYTWEEN_UNITASK
using System.Threading;
using Cysharp.Threading.Tasks;

namespace KeyTweening
{
    /// <summary>
    /// UniTask integration, compiled only when UniTask (com.cysharp.unitask) is installed. The core API returns Unity's
    /// Awaitable; these return UniTask so states combine with UniTask.WhenAll / WhenAny, timeouts and the rest of UniTask.
    /// </summary>
    public static class KeyTweenUniTaskExtensions
    {
        /// <summary><see cref="KeyTween.PlayAsync"/> as a UniTask (same completion and cancellation).</summary>
        public static UniTask PlayUniTaskAsync(this KeyTween animation, string stateName,
            KeyTweenInterrupt interrupt = KeyTweenInterrupt.Cancel, CancellationToken cancellationToken = default) =>
            animation.PlayAsync(stateName, interrupt, cancellationToken).AsUniTask();

        /// <summary><see cref="KeyTween.PlayReversedAsync"/> as a UniTask.</summary>
        public static UniTask PlayReversedUniTaskAsync(this KeyTween animation, string stateName,
            KeyTweenInterrupt interrupt = KeyTweenInterrupt.Cancel, CancellationToken cancellationToken = default) =>
            animation.PlayReversedAsync(stateName, interrupt, cancellationToken).AsUniTask();

        /// <summary><see cref="KeyTween.WaitEventAsync"/> as a UniTask.</summary>
        public static UniTask<KeyTweenEventInfo> WaitEventUniTaskAsync(this KeyTween animation, string eventName,
            CancellationToken cancellationToken = default) =>
            animation.WaitEventAsync(eventName, cancellationToken).AsUniTask();
    }
}
#endif
