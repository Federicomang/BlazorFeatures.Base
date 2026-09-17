using System;
using System.Collections.Generic;
#if NET10_0_OR_GREATER
using System.Threading;
#endif
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions
{
    /// <summary>
    /// Coordinates asynchronous operations that must finish before a feature
    /// service scope can be disposed.
    /// </summary>
    public sealed class FeatureScopeLifetime
    {
#if NET10_0_OR_GREATER
        private readonly Lock _syncRoot = new();
#else
        private readonly object _syncRoot = new();
#endif
        private readonly TaskCompletionSource<object?> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<Exception> _exceptions = [];
        private int _pendingOperations = 1;
        private bool _registrationCompleted;
        private bool _hasDeferredOperations;

        /// <summary>
        /// Registers an asynchronous operation that may continue after the
        /// feature handler returns and still requires the feature scope.
        /// </summary>
        /// <remarks>
        /// The operation must be registered before the lifetime has completed.
        /// Operations registered by an already deferred operation are supported.
        /// </remarks>
        public void DeferDisposalUntil(Task operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            lock (_syncRoot)
            {
                if (_completion.Task.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "The feature scope lifetime has already completed.");
                }

                _hasDeferredOperations = true;
                _pendingOperations++;
            }

            _ = ObserveOperationAsync(operation);
        }

        internal bool HasDeferredOperations
        {
            get
            {
                lock (_syncRoot)
                    return _hasDeferredOperations;
            }
        }

        internal Task CompleteRegistration()
        {
            lock (_syncRoot)
            {
                if (_registrationCompleted)
                {
                    throw new InvalidOperationException(
                        "The feature scope lifetime registration has already completed.");
                }

                _registrationCompleted = true;
                CompleteOperationLocked(null);
                return _completion.Task;
            }
        }

        private async Task ObserveOperationAsync(Task operation)
        {
            Exception? exception = null;
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (Exception operationException)
            {
                exception = operationException;
            }

            lock (_syncRoot)
                CompleteOperationLocked(exception);
        }

        private void CompleteOperationLocked(Exception? exception)
        {
            if (exception != null)
                _exceptions.Add(exception);

            _pendingOperations--;
            if (_pendingOperations != 0)
                return;

            if (_exceptions.Count == 0)
                _completion.TrySetResult(null);
            else
                _completion.TrySetException(_exceptions);
        }
    }
}
