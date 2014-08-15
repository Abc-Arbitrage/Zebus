using System;

namespace Abc.Zebus
{
    /// <summary>
    /// Specifies the subscription mode of the target message handler.
    /// </summary>
    public class SubscriptionModeAttribute : Attribute
    {
        public SubscriptionModeAttribute(SubscriptionMode subscriptionMode)
        {
            SubscriptionMode = subscriptionMode;
        }

        public SubscriptionMode SubscriptionMode { get; private set; }
    }
}