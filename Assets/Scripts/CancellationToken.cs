using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts
{
    /// <summary>
    /// Represents token that can be used to cancel a running task.
    /// </summary>
    public class CancellationToken
    {
        /// <summary>
        /// Wether the current task should be aborted
        /// </summary>
        public bool IsCancellationRequested
        {
            get { return _isCanceled; }
            set
            {
                lock (_registrations)
                {
                    if (!CanBeCanceled)
                        throw new InvalidOperationException("Can't set cancellation state twice");
                    
                    if(value == false)
                        return;
                }

                List<Action> actions;
                lock (_registrations)
                    actions = _registrations.ToList();

                foreach (var action in actions)
                    action();
            }
        }

        /// <summary>
        /// Wether it's possible to cancel this Token
        /// </summary>
        public bool CanBeCanceled { get { return !_isCanceled; } }

        private bool _isCanceled = false;
        private List<Action> _registrations = new List<Action>();

        /// <summary>
        /// Method that throws OperationCanceledException if the current Token is canceled
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            lock (_registrations)
                if (IsCancellationRequested)
                    throw new OperationCanceledException();
        }

        /// <summary>
        /// Registers an action to be called if this Token gets canceled
        /// </summary>
        /// <param name="callback"></param>
        public void Register(Action callback)
        {
            lock(_registrations)
            {
                if(!IsCancellationRequested)
                {
                    _registrations.Add(callback);
                    return;
                }
            }
            callback();
        }
    }
}
