using System;

namespace Abc.Zebus
{
    /// <summary>
    /// Specifies the startup subscription mode of the target message handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SubscriptionModeAttribute : Attribute
    {
        /// <summary>
        /// Specifies a <see cref="Zebus.SubscriptionMode"/> that should be used for all handled messages.
        /// </summary>
        public SubscriptionModeAttribute(SubscriptionMode subscriptionMode)
        {
            SubscriptionMode = subscriptionMode;
        }

        /// <summary>
        /// Specifies a startup subscriber (<see cref="IStartupSubscriber"/>) that should be used for all handled messages.
        /// </summary>
        public SubscriptionModeAttribute(Type startupSubscriberType)
        {
            if (!typeof(IStartupSubscriber).IsAssignableFrom(startupSubscriberType))
                throw new ArgumentException($"{nameof(startupSubscriberType)} must implement {nameof(IStartupSubscriber)}", nameof(startupSubscriberType));

            StartupSubscriberType = startupSubscriberType;
        }

        public SubscriptionMode? SubscriptionMode { get; }

        /// <summary>
        /// A type which implements the interface <see cref="IStartupSubscriber"/>
        /// </summary>
        public Type? StartupSubscriberType { get; }
    }
}
