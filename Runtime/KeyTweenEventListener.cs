using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KeyTweening
{
    /// <summary>
    /// Connects animation events to UnityEvents in the Inspector, so designers can react without code
    /// (e.g. "Shown" → AudioSource.PlayOneShot). Custom [KeyTweenEvent] struct data is for code: use KeyTween.AddListener&lt;T&gt;.
    /// </summary>
    [AddComponentMenu("KeyTween/Event Listener")]
    public sealed class KeyTweenEventListener : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string eventName = "";
            public UnityEvent onRaised = new();
        }

        [Tooltip("The KeyTween whose events are listened to. Empty = the one on this GameObject.")]
        public KeyTween source;
        public List<Binding> bindings = new();

        KeyTween _subscribed;

        internal void OnEnable()
        {
            if (source == null) source = GetComponent<KeyTween>();
            _subscribed = source;
            if (_subscribed != null) _subscribed.EventRaised += OnEvent;
        }

        internal void OnDisable()
        {
            if (_subscribed != null) _subscribed.EventRaised -= OnEvent;
            _subscribed = null;
        }

        void OnEvent(in KeyTweenEventInfo e)
        {
            foreach (var binding in bindings)
                if (binding.eventName == e.Name) binding.onRaised.Invoke();
        }
    }
}
