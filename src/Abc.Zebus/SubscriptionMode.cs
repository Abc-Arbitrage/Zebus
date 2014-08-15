namespace Abc.Zebus
{
    public enum SubscriptionMode
    {
        /// <summary>
        /// A subscription for the handler message type will be automatically performed on startup.
        /// This is the default mode for non-routable messages.
        /// </summary>
        Auto,
        /// <summary>
        /// The subscription for the handler message type must be manually performed with <code>IBus.Subscribe</code>.
        /// This is the default mode for routable messages.
        /// </summary>
        Manual,
    }
}