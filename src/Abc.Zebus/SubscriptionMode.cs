namespace Abc.Zebus;

/// <summary>
/// Specifies the startup subscription mode for a message handler.
/// Must be configured using <see cref="SubscriptionModeAttribute"/>.
/// </summary>
public enum SubscriptionMode
{
    /// <summary>
    /// A subscription for the handler message type will be automatically performed on startup (using <c>Subscription.Any&lt;Message&gt;</c>).
    /// This is the default mode for non-routable messages.
    /// </summary>
    /// <remarks>
    /// If you need to configure the startup subscription, consider using <see cref="IStartupSubscriber"/>.
    /// </remarks>
    Auto,
    /// <summary>
    /// The subscription for the handler message type must be manually performed with <code>IBus.Subscribe</code>.
    /// This is the default mode for routable messages.
    /// </summary>
    Manual,
}
