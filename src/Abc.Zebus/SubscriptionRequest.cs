using System;
using System.Collections.Generic;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus;

public class SubscriptionRequest
{
    private readonly HashSet<Subscription> _subscriptions = new();

    public IEnumerable<Subscription> Subscriptions => _subscriptions.AsReadOnlyEnumerable();

    public bool ThereIsNoHandlerButIKnowWhatIAmDoing { get; set; }

    internal SubscriptionRequestBatch? Batch { get; private set; }

    internal bool IsSubmitted { get; private set; }
    internal int? SubmissionSubscriptionsVersion { get; private set; }
    internal bool IsStartupRequest { get; private set; }

    public SubscriptionRequest(Subscription subscription)
    {
        _subscriptions.Add(subscription);
    }

    public SubscriptionRequest(IEnumerable<Subscription> subscriptions)
    {
        _subscriptions.AddRange(subscriptions);
    }

    public void AddToBatch(SubscriptionRequestBatch batch)
    {
        EnsureNotSubmitted();

        if (Batch != null)
            throw new InvalidOperationException("This subscription request is already part of a batch");

        batch.AddRequest(this);
        Batch = batch;
    }

    internal void MarkAsSubmitted(int subscriptionsVersion, bool isRunning)
    {
        EnsureNotSubmitted();
        IsSubmitted = true;
        SubmissionSubscriptionsVersion = subscriptionsVersion;
        IsStartupRequest = !isRunning;

        if (IsStartupRequest && Batch != null)
            throw new InvalidOperationException("Startup subscriptions should not be batched");
    }

    private void EnsureNotSubmitted()
    {
        if (IsSubmitted)
            throw new InvalidOperationException("This subscription request has already been submitted");
    }
}
