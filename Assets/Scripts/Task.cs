using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Assets.Scripts;
using System.Linq;

public class Task
{
    public TaskCreationOptions CreationOptions { get; protected set; }
    public Exception Exception { get; protected set; }
    public bool IsCanceled { get; protected set; }
    public bool IsCompleted { get { return Status == TaskStatus.RanToCompletion; } }
    public bool IsFaulted { get { return Status == TaskStatus.Faulted; } }
    public TaskStatus Status { get; protected set; }

    protected CancellationToken _cancellationToken;
    protected Action _action;
    protected Scheduler _scheduler;
    protected List<Action> _continuations = new List<Action>();
    protected object _lockObject = new object();

    public Task(Action action, CancellationToken cancellationToken, TaskCreationOptions creationOptions)
    {
        Status = TaskStatus.Created;
        _action = action;
        _cancellationToken = cancellationToken;
        CreationOptions = creationOptions;
        _scheduler = Scheduler.Default;
    }

    public Task(Action action, CancellationToken cancellationToken) : this(action, cancellationToken, TaskCreationOptions.None) { }
    public Task(Action action, TaskCreationOptions creationOptions) : this(action, null, creationOptions) { }
    public Task(Action action) : this(action, null, TaskCreationOptions.None) { }

    public void Start(Scheduler scheduler)
    {
        lock (_lockObject)
        {
            if (Status != TaskStatus.Created)
                throw new InvalidOperationException("Task already scheduled");

            Status = TaskStatus.WaitingToRun;
        }
        scheduler.Queue(() =>
        {
            lock(_lockObject)
                Status = TaskStatus.Running;
            try
            {
                if (_cancellationToken != null)
                    _cancellationToken.ThrowIfCancellationRequested();

                _action();
                
            }
            catch (Exception e)
            {
                lock (_lockObject)
                {
                    Exception = e;
                    if (e is OperationCanceledException)
                        Status = TaskStatus.Canceled;
                    else
                        Status = TaskStatus.Faulted;
                }
            }
        });
    }

    public void Start()
    {
        Start(_scheduler);
    }

    protected void AddContinuation(Action action)
    {
        lock(_lockObject)
        {
            if (Status == TaskStatus.RanToCompletion)
                action();
            else
            {
                _continuations.Add(action);
            }
        }
    }

    public Task ContinueWith(Action<Task> action, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions, Scheduler scheduler)
    {
        var task = new Task(() => action(this), cancellationToken, TaskCreationOptions.None) { _scheduler = scheduler };
        AddContinuation(() =>
            {
                //TODO: add ContinuationOptions
                task.Start();
            });
        return task;
    }

    internal void SetException(Exception e)
    {
        lock (_lockObject)
        {
            if (Status == TaskStatus.Running || Status == TaskStatus.Created || Status == TaskStatus.WaitingToRun)
            {
                Exception = e;
                Status = TaskStatus.Faulted;
            }
            else
                throw new InvalidOperationException("Can't set exception on non-running task.");
        }
    }

    internal void SetCompleted()
    {
        List<Action> continuations;
        lock(_lockObject)
        {
            if (Status != TaskStatus.Running && Status != TaskStatus.Created && Status != TaskStatus.WaitingToRun)
                throw new InvalidOperationException("Can't set completion on already completed task.");
            Status = TaskStatus.RanToCompletion;
            continuations = _continuations.ToList();
        }

        foreach (var action in continuations)
            action();
    }

    public Task ContinueWith(Action<Task> action)
    {
        return ContinueWith(action, null, TaskContinuationOptions.None, _scheduler);
    }
}

public class Task<TResult> : Task
{
    private TResult _result;
    public TResult Result
    {
        get
        {
            lock(_lockObject)
            {
                if (Status == TaskStatus.Faulted)
                    throw Exception;
                if (Status == TaskStatus.RanToCompletion)
                    return _result;
                throw new InvalidOperationException("Unable to return Result from uncompleted Task");
            }
        }
    }

    public Task(Func<TResult> action, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(() => {}, cancellationToken, creationOptions)
    {
        this._action = () => _result = action();
    }

    public Task(Func<TResult> action, CancellationToken cancellationToken) : this(action, cancellationToken, TaskCreationOptions.None) { }
    public Task(Func<TResult> action, TaskCreationOptions creationOptions) : this(action, null, creationOptions) { }
    public Task(Func<TResult> action) : this(action, null, TaskCreationOptions.None) { }
}
