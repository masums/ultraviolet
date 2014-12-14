﻿using System;

namespace TwistedLogik.Ultraviolet.Layout.Animation
{
    /// <summary>
    /// Represents an animation between nullable signed byte values.
    /// </summary>
    public sealed class NullableSByteAnimation : Animation<SByte?>
    {
        /// <inheritdoc/>
        public override SByte? InterpolateValues(SByte? value1, SByte? value2, EasingFunction easing, Single factor)
        {
            if (value1 == null || value2 == null)
                return null;

            return Tweening.Tween(value1.GetValueOrDefault(), value2.GetValueOrDefault(), easing, factor);
        }
    }
}
