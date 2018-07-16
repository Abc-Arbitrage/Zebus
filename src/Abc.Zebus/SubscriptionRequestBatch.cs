using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Abc.Zebus
{
    public class SubscriptionRequestBatch
    {
        private readonly TaskCompletionSource<object> _submitCompletionSource = new TaskCompletionSource<object>();
        private readonly TaskCompletionSource<object> _registerCompletionSource = new TaskCompletionSource<object>();
        private readonly List<SubscriptionRequest> _requests = new List<SubscriptionRequest>();
        private bool _isConsumed;

        internal void AddRequest(SubscriptionRequest request)
        {
            EnsureNotSubmitted();

            lock (_requests)
            {
                _requests.Add(request);
            }
        }

        public async Task SubmitAsync()
        {
            EnsureNotSubmitted();

            lock (_requests)
            {
                if (_requests.Any(i => !i.IsSubmitted))
                    throw new InvalidOperationException($"Not all requests in the batch have been submitted with {nameof(IBus)}.{nameof(IBus.SubscribeAsync)}");
            }

            _submitCompletionSource.SetResult(null);

            await WhenRegistrationCompletedAsync().ConfigureAwait(false);
        }

        internal Task WhenSubmittedAsync()
            => _submitCompletionSource.Task;

        internal void NotifyRegistrationCompleted(Exception exception)
        {
            if (exception != null)
                _registerCompletionSource.SetException(exception);
            else
                _registerCompletionSource.SetResult(null);
        }

        internal async Task WhenRegistrationCompletedAsync()
        {
            lock (_requests)
            {
                if (_requests.Count == 0)
                    return;
            }

            await _registerCompletionSource.Task.ConfigureAwait(false);
        }

        [CanBeNull]
        internal IEnumerable<Subscription> TryConsumeBatchSubscriptions()
        {
            lock (_requests)
            {
                if (_isConsumed)
                    return null;

                _isConsumed = true;
                return _requests.SelectMany(i => i.Subscriptions).ToList();
            }
        }

        private void EnsureNotSubmitted()
        {
            if (_submitCompletionSource.Task.IsCompleted)
                throw new InvalidOperationException("This subscription batch has already been submitted");
        }
    }
}
