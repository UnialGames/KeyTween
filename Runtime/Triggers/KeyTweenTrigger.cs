using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KeyTweening
{
    public enum KeyTweenEvent
    {
        OnEnable,
        PointerEnter,
        PointerExit,
        PointerDown,
        PointerUp,
        Click,
        Select,
        Deselect,
        ToggleOn,       // Toggle.isOn became true
        ToggleOff,      // Toggle.isOn became false
        Start,          // once, after the first OnEnable (Instantiate runs both: bind only one of them)
    }

    [Serializable]
    public sealed class KeyTweenBinding
    {
        public KeyTweenEvent evt;
        public string state;
    }

    /// <summary>Plays KeyTween states when UI events happen on this object.</summary>
    [RequireComponent(typeof(KeyTween))]
    public sealed class KeyTweenTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, ISelectHandler, IDeselectHandler
    {
        public List<KeyTweenBinding> bindings = new();

        KeyTween _animation;
        Selectable _selectable;
        Toggle _toggle;

        void Awake()
        {
            _animation = GetComponent<KeyTween>();
            _selectable = GetComponent<Selectable>();
            _toggle = GetComponent<Toggle>();
        }

        void OnEnable()
        {
            if (_toggle != null) _toggle.onValueChanged.AddListener(OnToggle);
            Fire(KeyTweenEvent.OnEnable, null);
        }

        void Start() => Fire(KeyTweenEvent.Start, null);

        void OnDisable()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnToggle);
        }

        void OnToggle(bool isOn) => Fire(isOn ? KeyTweenEvent.ToggleOn : KeyTweenEvent.ToggleOff, null);
        public void OnPointerEnter(PointerEventData e) => Fire(KeyTweenEvent.PointerEnter, e);
        public void OnPointerExit(PointerEventData e) => Fire(KeyTweenEvent.PointerExit, e);
        public void OnPointerDown(PointerEventData e) => Fire(KeyTweenEvent.PointerDown, e);
        public void OnPointerUp(PointerEventData e) => Fire(KeyTweenEvent.PointerUp, e);
        public void OnPointerClick(PointerEventData e) => Fire(KeyTweenEvent.Click, e);
        public void OnSelect(BaseEventData e) => Fire(KeyTweenEvent.Select, e as PointerEventData);
        public void OnDeselect(BaseEventData e) => Fire(KeyTweenEvent.Deselect, e as PointerEventData);

        internal void Fire(KeyTweenEvent evt, PointerEventData pointer)
        {
            // A non-interactable Selectable ignores input; OnEnable still plays.
            if (evt != KeyTweenEvent.OnEnable && evt != KeyTweenEvent.Start && _selectable != null && !_selectable.IsInteractable()) return;

            foreach (var b in bindings)
                if (b.evt == evt) _animation.Play(b.state, pointer);
        }
    }
}
