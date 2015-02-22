using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts
{
    public class CancellationToken
    {
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
        public bool CanBeCanceled { get { return !_isCanceled; } }

        private bool _isCanceled = false;
        private List<Action> _registrations = new List<Action>();

        public void ThrowIfCancellationRequested()
        {
            lock (_registrations)
                if (IsCancellationRequested)
                    throw new OperationCanceledException();
        }

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
