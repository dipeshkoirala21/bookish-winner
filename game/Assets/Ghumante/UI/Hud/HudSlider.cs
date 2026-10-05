using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>
    /// A small horizontal slider in the HUD style (a cream track, a yellow fill, a white knob) for the development-only
    /// time-of-day control. Drag or tap anywhere on the track; the value maps linearly onto [<see cref="Min"/>,
    /// <see cref="Max"/>]. Built in code (no UXML element), writes only layout-free styles (the fill's scale and the
    /// knob's translate).
    /// </summary>
    public sealed class HudSlider
    {
        private const int NoPointer = -1;
        private const float KnobSize = 40f;

        private readonly VisualElement _track;
        private readonly VisualElement _fill;
        private readonly VisualElement _knob;
        private int _pointer = NoPointer;
        private float _value;

        public HudSlider(VisualElement parent, string name, float min, float max, float value)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!(max > min)) throw new ArgumentException("max must be greater than min");
            Min = min;
            Max = max;
            _track = new VisualElement { name = name };
            _track.AddToClassList("gh-hud-slider");
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("gh-hud-slider__fill");
            _knob = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob.AddToClassList("gh-hud-slider__knob");
            _track.Add(_fill);
            _track.Add(_knob);
            parent.Add(_track);
            _track.RegisterCallback<PointerDownEvent>(OnDown);
            _track.RegisterCallback<PointerMoveEvent>(OnMove);
            _track.RegisterCallback<PointerUpEvent>(OnUp);
            _track.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            _track.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            SetValueWithoutNotify(value);
        }

        /// <summary>Raised with the new value while the player drags.</summary>
        public event Action<float> ValueChanged;

        public float Min { get; private set; }
        public float Max { get; private set; }

        public float Value
        {
            get { return _value; }
        }

        public VisualElement Element
        {
            get { return _track; }
        }

        /// <summary>Moves the knob without raising <see cref="ValueChanged"/> (the world's clock ticking on).</summary>
        public void SetValueWithoutNotify(float value)
        {
            if (float.IsNaN(value)) return;
            _value = Mathf.Clamp(value, Min, Max);
            Layout();
        }

        private void OnDown(PointerDownEvent evt)
        {
            if (_pointer != NoPointer) return;
            _pointer = evt.pointerId;
            _track.CapturePointer(evt.pointerId);
            Pick(evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == _pointer) Pick(evt.localPosition.x);
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointer) return;
            if (_track.HasPointerCapture(_pointer)) _track.ReleasePointer(_pointer);
            _pointer = NoPointer;
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            _pointer = NoPointer;
        }

        private void OnGeometry(GeometryChangedEvent evt)
        {
            Layout();
        }

        private void Pick(float x)
        {
            float width = _track.layout.width;
            if (float.IsNaN(width) || width < 1f) return;
            float t = Mathf.Clamp01(x / width);
            _value = Min + (Max - Min) * t;
            Layout();
            Action<float> handler = ValueChanged;
            if (handler != null) handler(_value);
        }

        private void Layout()
        {
            float t = (_value - Min) / (Max - Min);
            float width = _track.layout.width;
            _fill.style.scale = new Scale(new Vector3(Mathf.Max(0.001f, t), 1f, 1f));
            if (!float.IsNaN(width)) _knob.style.translate = new Translate(new Length(t * width - KnobSize * 0.5f, LengthUnit.Pixel), new Length(0f, LengthUnit.Pixel));
        }
    }
}
