﻿using System;
using System.Collections.Generic;
using TwistedLogik.Nucleus;

namespace TwistedLogik.Ultraviolet.UI.Presentation.Animations
{
    /// <summary>
    /// Represents a storyboard target's collection of animations.
    /// </summary>
    public sealed partial class StoryboardTargetAnimationCollection : IEnumerable<KeyValuePair<StoryboardTargetAnimationKey, AnimationBase>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StoryboardTargetAnimationCollection"/> class.
        /// </summary>
        /// <param name="target">The storyboard target that owns the collection.</param>
        public StoryboardTargetAnimationCollection(StoryboardTarget target)
        {
            Contract.Require(target, "target");

            this.target = target;
        }

        /// <summary>
        /// Adds the specified animation to this collection.
        /// </summary>
        /// <param name="property">The name of the dependency property to which the animation applies.</param>
        /// <param name="animation">The animation to add to the collection.</param>
        /// <returns><c>true</c> if the animation was added to the collection; otherwise, <c>false</c>.</returns>
        public Boolean Add(String property, AnimationBase animation)
        {
            Contract.RequireNotEmpty(property, "property");
            Contract.Require(animation, "animation");

            var name = new UvmlName(property);
            var key = new StoryboardTargetAnimationKey(name);
            return Add(key, animation);
        }

        /// <summary>
        /// Adds the specified animation to this collection.
        /// </summary>
        /// <param name="property">The name of the dependency property to which the animation applies.</param>
        /// <param name="animation">The animation to add to the collection.</param>
        /// <returns><c>true</c> if the animation was added to the collection; otherwise, <c>false</c>.</returns>
        public Boolean Add(UvmlName property, AnimationBase animation)
        {
            Contract.Require(animation, "animation");

            var key = new StoryboardTargetAnimationKey(property);
            return Add(key, animation);
        }

        /// <summary>
        /// Adds the specified animation to this collection.
        /// </summary>
        /// <param name="key">The key that identifies the animation to add to the collection.</param>
        /// <param name="animation">The animation to add to the collection.</param>
        /// <returns><c>true</c> if the animation was added to the collection; otherwise, <c>false</c>.</returns>
        public Boolean Add(StoryboardTargetAnimationKey key, AnimationBase animation)
        {
            Contract.Require(animation, "animation");

            Remove(key);

            if (animation.Target != null)
                animation.Target.Animations.Remove(animation);

            animation.Target = Target;
            animations.Add(key, animation);
            if (Target.Storyboard != null)
            {
                Target.Storyboard.RecalculateDuration();
            }
            return true;
        }

        /// <summary>
        /// Removes the animation for the specified property from this collection.
        /// </summary>
        /// <param name="property">The name of the property to remove from the collection.</param>
        /// <returns><c>true</c> if the animation was removed from the collection; otherwise, <c>false</c>.</returns>
        public Boolean Remove(String property)
        {
            Contract.RequireNotEmpty(property, "property");

            var name = new UvmlName(property);
            var key = new StoryboardTargetAnimationKey(name);
            return Remove(key);
        }

        /// <summary>
        /// Removes the animation for the specified property from this collection.
        /// </summary>
        /// <param name="property">The name of the property to remove from the collection.</param>
        /// <returns><c>true</c> if the animation was removed from the collection; otherwise, <c>false</c>.</returns>
        public Boolean Remove(UvmlName property)
        {
            var key = new StoryboardTargetAnimationKey(property);
            return Remove(key);
        }

        /// <summary>
        /// Removes the animation for the specified property from this collection.
        /// </summary>
        /// <param name="key">The key that identifies the animation to remove from the collection.</param>
        /// <returns><c>true</c> if the animation was removed from the collection; otherwise, <c>false</c>.</returns>
        public Boolean Remove(StoryboardTargetAnimationKey key)
        {
            AnimationBase animation;
            if (!animations.TryGetValue(key, out animation))
                return false;

            animations.Remove(key);
            animation.Target = null;

            if (Target.Storyboard != null)
            {
                Target.Storyboard.RecalculateDuration();
            }

            return true;
        }

        /// <summary>
        /// Removes an animation from this collection.
        /// </summary>
        /// <param name="animation">The animation to remove from the collection.</param>
        /// <returns><c>true</c> if the animation was removed from the collection; otherwise, <c>false</c>.</returns>
        public Boolean Remove(AnimationBase animation)
        {
            Contract.Require(animation, "animation");

            var key   = default(StoryboardTargetAnimationKey);
            var found = false;

            foreach (var kvp in animations)
            {
                if (kvp.Value == animation)
                {
                    key   = kvp.Key;
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            return Remove(key);
        }

        /// <summary>
        /// Gets a value indicating whether the collection contains an animation on the specified property.
        /// </summary>
        /// <param name="property">The name of the property to evaluate.</param>
        /// <returns><c>true</c> if this collection contains an animation on the specified property; otherwise, <c>false</c>.</returns>
        public Boolean ContainsKey(String property)
        {
            Contract.RequireNotEmpty(property, "property");

            var name = new UvmlName(property);
            var key = new StoryboardTargetAnimationKey(name);
            return animations.ContainsKey(key);
        }

        /// <summary>
        /// Gets a value indicating whether the collection contains an animation on the specified property.
        /// </summary>
        /// <param name="property">The name of the property to evaluate.</param>
        /// <returns><c>true</c> if this collection contains an animation on the specified property; otherwise, <c>false</c>.</returns>
        public Boolean ContainsKey(UvmlName property)
        {
            var key = new StoryboardTargetAnimationKey(property);
            return animations.ContainsKey(key);
        }

        /// <summary>
        /// Gets a value indicating whether the collection contains an animation on the specified property.
        /// </summary>
        /// <param name="key">The animation key to evaluate.</param>
        /// <returns><c>true</c> if this collection contains an animation on the specified property; otherwise, <c>false</c>.</returns>
        public Boolean ContainsKey(StoryboardTargetAnimationKey key)
        {
            return animations.ContainsKey(key);
        }

        /// <summary>
        /// Gets a value indicating whether the collection contains the specified animation.
        /// </summary>
        /// <param name="animation">The animation to evaluate.</param>
        /// <returns><c>true</c> if this collection contains the specified animation; otherwise, <c>false</c>.</returns>
        public Boolean ContainsValue(AnimationBase animation)
        {
            Contract.Require(animation, "animation");

            return animations.ContainsValue(animation);
        }

        /// <summary>
        /// Gets the storyboard target that owns the collection.
        /// </summary>
        public StoryboardTarget Target
        {
            get { return target; }
        }

        /// <summary>
        /// Gets the number of animations in the collection.
        /// </summary>
        public Int32 Count
        {
            get { return animations.Count; }
        }

        // Property values.
        private readonly StoryboardTarget target;

        // State values.
        private readonly Dictionary<StoryboardTargetAnimationKey, AnimationBase> animations = 
            new Dictionary<StoryboardTargetAnimationKey, AnimationBase>();
    }
}
