using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Abc.Zebus
{
    public class SubscriptionRequestBatch
    {
        private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
        private readonly List<SubscriptionRequest> _requests = new List<SubscriptionRequest>();

        internal void AddRequest(SubscriptionRequest request)
        {
            EnsureNotSubmitted();

            lock (_requests)
            {
                _requests.Add(request);
            }
        }

        public void Submit()
        {
            EnsureNotSubmitted();

            lock (_requests)
            {
                if (_requests.Any(i => !i.IsSubmitted))
                    throw new InvalidOperationException($"Not all requests in the batch have been submitted with {nameof(IBus)}.{nameof(IBus.SubscribeAsync)}");
            }

            _completionSource.SetResult(null);
        }

        internal Task WhenSubmittedAsync()
            => _completionSource.Task;

        internal IEnumerable<Subscription> ConsumeBatchSubscriptions()
        {
            lock (_requests)
            {
                var allSubscriptions = _requests.SelectMany(i => i.Subscriptions).ToList();
                _requests.Clear();
                return allSubscriptions;
            }
        }

        private void EnsureNotSubmitted()
        {
            if (_completionSource.Task.IsCompleted)
                throw new InvalidOperationException("This subscription batch has already been submitted");
        }
    }
}
