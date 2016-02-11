// Copyright (c) Lokad 2009 
// https://github.com/Lokad/lokad-shared-libraries
// This code is released under the terms of the new BSD licence

using System;
using System.Threading;

namespace Abc.Zebus.Util
{
    /// <summary>
    /// Class that allows action to be executed, when it is disposed
    /// </summary>
    [Serializable]
    internal sealed class DisposableAction : IDisposable
    {
        private Action _action;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisposableAction"/> class.
        /// </summary>
        /// <param name="action">The action.</param>
        public DisposableAction(Action action)
        {
            _action = action;
        }

        /// <summary>
        /// Executes the action
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _action, null)?.Invoke();
        }
    }
}
