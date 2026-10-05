using System;
using Ghumante.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Motion
{
    /// <summary>A transform channel of a <see cref="MotionNode"/>.</summary>
    public enum MotionChannel
    {
        TranslateX = 0,
        TranslateY = 1,
        ScaleX = 2,
        ScaleY = 3,
        RotateDegrees = 4,
        Opacity = 5,
    }

    /// <summary>
    /// The animated transform of one element. Three layers are combined every frame:
    /// <list type="number">
    /// <item>a <b>base pose</b> per channel, set directly or driven by a <see cref="Tween"/> (entrances, pops);</item>
    /// <item><b>offsets</b> written each frame by idle loops (breathing, sway, parallax): added to translate
    /// and rotation, multiplied into scale and opacity;</item>
    /// <item>three <b>springs</b>: squash (press), wobble (degrees) and hop (pixels up).</item>
    /// </list>
    /// Only <c>style.translate</c>, <c>scale</c>, <c>rotate</c> and <c>opacity</c> are written, so layout
    /// never runs, and only the properties this node has been asked to animate: USS keeps the others (for
    /// example a pill's <c>:active</c> translate sink). Nothing allocates per frame. Create nodes with
    /// <see cref="UiAnimator.Node"/>.
    /// </summary>
    public sealed class MotionNode
    {
        private const int ChannelCount = 6;
        private const float WriteEpsilon = 0.0004f;

        [Flags]
        private enum Owned
        {
            None = 0,
            Translate = 1,
            Scale = 2,
            Rotate = 4,
            Opacity = 8,
        }

        private readonly float[] _rest = { 0f, 0f, 1f, 1f, 0f, 1f };
        private readonly float[] _base = { 0f, 0f, 1f, 1f, 0f, 1f };
        private readonly Tween[] _tweens = new Tween[ChannelCount];
        private readonly float[] _tweenStart = new float[ChannelCount];
        private int _tweening;
        private Owned _owned;
        private bool _dirty;

        private float _offsetX;
        private float _offsetY;
        private float _offsetRotate;
        private float _offsetScaleX = 1f;
        private float _offsetScaleY = 1f;
        private float _offsetOpacity = 1f;

        private float _writtenX;
        private float _writtenY;
        private float _writtenScaleX = 1f;
        private float _writtenScaleY = 1f;
        private float _writtenRotate;
        private float _writtenOpacity = 1f;
        private Owned _writtenOnce;

        /// <summary>Press squash: 0 at rest, 1 fully pressed, negative while it stretches back.</summary>
        public Spring Squash = new Spring(SpringParams.Press);

        /// <summary>Rotation wobble in degrees.</summary>
        public Spring Wobble = new Spring(SpringParams.Wobble);

        /// <summary>Hop height in pixels (positive is up).</summary>
        public Spring Hop = new Spring(SpringParams.Hop);

        internal MotionNode(VisualElement element)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        public VisualElement Element { get; private set; }

        /// <summary>How far a full press squashes: 0.08 gives scale 1.06 x 0.92 (with the default gain).</summary>
        public float SquashAmount { get; set; } = 0.08f;

        /// <summary>Share of the height change that goes into width (see <see cref="Squash"/>).</summary>
        public float SquashWidthGain { get; set; } = 0.75f;

        /// <summary>True while a tween runs or a spring has not settled.</summary>
        public bool IsMoving
        {
            get { return _tweening != 0 || !Squash.IsSettled() || !Wobble.IsSettled(0.01f) || !Hop.IsSettled(0.05f); }
        }

        /// <summary>The current base value of a channel (without offsets and springs).</summary>
        public float Get(MotionChannel channel)
        {
            return _base[(int)channel];
        }

        public bool IsTweening(MotionChannel channel)
        {
            return (_tweening & (1 << (int)channel)) != 0;
        }

        /// <summary>
        /// The value a channel returns to (default: no translation, scale 1, no rotation, opaque). Set it when
        /// USS gives the element a static transform that the animation must keep, e.g. a tilted ribbon tail.
        /// </summary>
        public void SetRest(MotionChannel channel, float value)
        {
            int c = (int)channel;
            _rest[c] = value;
            if (!IsTweening(channel)) _base[c] = value;
            Own(channel);
        }

        /// <summary>Jumps a channel to <paramref name="value"/>, cancelling its tween.</summary>
        public void Set(MotionChannel channel, float value)
        {
            int c = (int)channel;
            _tweening &= ~(1 << c);
            _base[c] = value;
            Own(channel);
        }

        /// <summary>Jumps every channel back to its rest value and stops tweens and springs.</summary>
        public void Settle()
        {
            _tweening = 0;
            for (int c = 0; c < ChannelCount; c++) _base[c] = _rest[c];
            Squash.Snap(0f);
            Wobble.Snap(0f);
            Hop.Snap(0f);
            ClearOffsets();
            _dirty = true;
        }

        /// <summary>Additive translation from idle loops and parallax, in pixels.</summary>
        public void SetOffset(float x, float y)
        {
            if (x == _offsetX && y == _offsetY) return;
            _offsetX = x;
            _offsetY = y;
            _owned |= Owned.Translate;
            _dirty = true;
        }

        /// <summary>Additive rotation from idle loops, in degrees.</summary>
        public void SetOffsetRotate(float degrees)
        {
            if (degrees == _offsetRotate) return;
            _offsetRotate = degrees;
            _owned |= Owned.Rotate;
            _dirty = true;
        }

        /// <summary>Multiplicative scale from idle loops.</summary>
        public void SetOffsetScale(float scaleX, float scaleY)
        {
            if (scaleX == _offsetScaleX && scaleY == _offsetScaleY) return;
            _offsetScaleX = scaleX;
            _offsetScaleY = scaleY;
            _owned |= Owned.Scale;
            _dirty = true;
        }

        /// <summary>Multiplicative opacity from idle loops.</summary>
        public void SetOffsetOpacity(float opacity)
        {
            if (opacity == _offsetOpacity) return;
            _offsetOpacity = opacity;
            _owned |= Owned.Opacity;
            _dirty = true;
        }

        public void ClearOffsets()
        {
            _offsetX = 0f;
            _offsetY = 0f;
            _offsetRotate = 0f;
            _offsetScaleX = 1f;
            _offsetScaleY = 1f;
            _offsetOpacity = 1f;
            _dirty = true;
        }

        /// <summary>Squashes (true) or springs back with an overshoot (false).</summary>
        public void Press(bool down)
        {
            Squash.Target = down ? 1f : 0f;
            _owned |= Owned.Scale;
            _dirty = true;
        }

        /// <summary>Starts or adds to a rotational wobble (degrees per second).</summary>
        public void KickWobble(float degreesPerSecond)
        {
            Wobble.Kick(degreesPerSecond);
            _owned |= Owned.Rotate;
            _dirty = true;
        }

        /// <summary>Throws the element up (pixels per second); it lands with a bounce or two.</summary>
        public void KickHop(float pixelsPerSecond)
        {
            Hop.Kick(pixelsPerSecond);
            _owned |= Owned.Translate;
            _dirty = true;
        }

        internal void Play(MotionChannel channel, Tween tween, float now)
        {
            int c = (int)channel;
            _tweens[c] = tween;
            _tweenStart[c] = now;
            _tweening |= 1 << c;
            _base[c] = tween.Sample(0f);
            Own(channel);
        }

        /// <summary>Advances tweens and springs and writes changed styles. Returns false when idle.</summary>
        internal bool Update(float now, float dt)
        {
            if (_tweening != 0)
            {
                for (int c = 0; c < ChannelCount; c++)
                {
                    int bit = 1 << c;
                    if ((_tweening & bit) == 0) continue;
                    float elapsed = now - _tweenStart[c];
                    _base[c] = _tweens[c].Sample(elapsed);
                    if (_tweens[c].IsComplete(elapsed)) _tweening &= ~bit;
                }
                _dirty = true;
            }
            if (!Squash.IsSettled())
            {
                Squash.Step(dt);
                if (Squash.IsSettled()) Squash.Snap(Squash.Target);
                _dirty = true;
            }
            if (!Wobble.IsSettled(0.01f))
            {
                Wobble.Step(dt);
                if (Wobble.IsSettled(0.01f)) Wobble.Snap(Wobble.Target);
                _dirty = true;
            }
            if (!Hop.IsSettled(0.05f))
            {
                Hop.Step(dt);
                if (Hop.IsSettled(0.05f)) Hop.Snap(Hop.Target);
                _dirty = true;
            }
            if (!_dirty) return false;
            _dirty = false;
            Write();
            return true;
        }

        /// <summary>Writes the current pose immediately (used before the first frame is drawn).</summary>
        internal void WriteNow()
        {
            _dirty = false;
            Write();
        }

        private void Own(MotionChannel channel)
        {
            switch (channel)
            {
                case MotionChannel.TranslateX:
                case MotionChannel.TranslateY:
                    _owned |= Owned.Translate;
                    break;
                case MotionChannel.ScaleX:
                case MotionChannel.ScaleY:
                    _owned |= Owned.Scale;
                    break;
                case MotionChannel.RotateDegrees:
                    _owned |= Owned.Rotate;
                    break;
                default:
                    _owned |= Owned.Opacity;
                    break;
            }
            _dirty = true;
        }

        private void Write()
        {
            IStyle style = Element.style;
            if ((_owned & Owned.Translate) != 0)
            {
                float x = _base[0] + _offsetX;
                float y = _base[1] + _offsetY - Hop.Value;
                if (Changed(x, _writtenX) || Changed(y, _writtenY) || (_writtenOnce & Owned.Translate) == 0)
                {
                    _writtenX = x;
                    _writtenY = y;
                    _writtenOnce |= Owned.Translate;
                    style.translate = new Translate(new Length(x, LengthUnit.Pixel), new Length(y, LengthUnit.Pixel));
                }
            }
            if ((_owned & Owned.Scale) != 0)
            {
                float squashX, squashY;
                Core.Motion.Squash.Scale(Squash.Value * SquashAmount, SquashWidthGain, out squashX, out squashY);
                float sx = _base[2] * _offsetScaleX * squashX;
                float sy = _base[3] * _offsetScaleY * squashY;
                if (Changed(sx, _writtenScaleX) || Changed(sy, _writtenScaleY) || (_writtenOnce & Owned.Scale) == 0)
                {
                    _writtenScaleX = sx;
                    _writtenScaleY = sy;
                    _writtenOnce |= Owned.Scale;
                    style.scale = new Scale(new Vector3(sx, sy, 1f));
                }
            }
            if ((_owned & Owned.Rotate) != 0)
            {
                float r = _base[4] + _offsetRotate + Wobble.Value;
                if (Changed(r, _writtenRotate) || (_writtenOnce & Owned.Rotate) == 0)
                {
                    _writtenRotate = r;
                    _writtenOnce |= Owned.Rotate;
                    style.rotate = new Rotate(new Angle(r, AngleUnit.Degree));
                }
            }
            if ((_owned & Owned.Opacity) != 0)
            {
                float o = _base[5] * _offsetOpacity;
                o = o < 0f ? 0f : (o > 1f ? 1f : o);
                if (Changed(o, _writtenOpacity) || (_writtenOnce & Owned.Opacity) == 0)
                {
                    _writtenOpacity = o;
                    _writtenOnce |= Owned.Opacity;
                    style.opacity = o;
                }
            }
        }

        private static bool Changed(float a, float b)
        {
            float d = a - b;
            return d > WriteEpsilon || d < -WriteEpsilon;
        }
    }
}
