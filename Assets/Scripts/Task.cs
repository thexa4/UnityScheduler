using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Assets.Scripts;
using System.Linq;

/// <summary>
/// Represents a discrete amount of work that can be scheduled
/// </summary>
public class Task
{
    /// <summary>
    /// Class used to return values from IEnumerators
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public class TaskResult<TResult>
    {
        /// <summary>
        /// Creates a new return value
        /// </summary>
        /// <param name="result">The value to return</param>
        public TaskResult(TResult result)
        {
            Result = result;
        }

        /// <summary>
        /// The return value
        /// </summary>
        public TResult Result { get; private set; }
    }

    /// <summary>
    /// Represents an await instruction in an IEnumerable
    /// </summary>
    public class TaskWaitOrder
    {
        /// <summary>
        /// Creates a new await instruction
        /// </summary>
        /// <param name="task">The Task to wait on</param>
        public TaskWaitOrder(Task task)
        {
            Task = task;
        }

        /// <summary>
        /// The Task to wait on
        /// </summary>
        public Task Task { get; private set; }
    }

    public TaskCreationOptions CreationOptions { get; protected set; }
    /// <summary>
    /// The exception thrown by the action
    /// </summary>
    public Exception Exception { get; protected set; }
    /// <summary>
    /// Wether the given CancellationToken was triggered
    /// </summary>
    public bool IsCanceled { get { return Status == TaskStatus.Canceled; } }
    /// <summary>
    /// Wether the action has run to completion
    /// </summary>
    public bool IsCompleted { get { return Status == TaskStatus.RanToCompletion || Status == TaskStatus.Faulted || Status == TaskStatus.Canceled; } }
    /// <summary>
    /// Wether the action has thrown an Exception
    /// </summary>
    public bool IsFaulted { get { return Status == TaskStatus.Faulted; } }
    /// <summary>
    /// The status of the current Task
    /// </summary>
    public TaskStatus Status { get; protected set; }

    protected CancellationToken _cancellationToken;
    protected Action _action;
    protected TaskScheduler _scheduler;
    protected List<Action> _continuations = new List<Action>();
    protected object _lockObject = new object();

    internal Action Execution { get; set; }

    /// <summary>
    /// Creates a new Task
    /// </summary>
    /// <param name="action">The action to run</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    /// <param name="creationOptions">The CreationOptions</param>
    public Task(Action action, CancellationToken cancellationToken, TaskCreationOptions creationOptions)
    {
        Status = TaskStatus.Created;
        _action = action;
        _cancellationToken = cancellationToken;
        CreationOptions = creationOptions;
        _scheduler = TaskScheduler.Default;

        Execution = () =>
        {
            lock (_lockObject)
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

            RunContinuations();
        };
    }

    /// <summary>
    /// Creates a new Task
    /// </summary>
    /// <param name="action">The action to run</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    public Task(Action action, CancellationToken cancellationToken) : this(action, cancellationToken, TaskCreationOptions.None) { }
    /// <summary>
    /// Creates a new Task
    /// </summary>
    /// <param name="action">The action to run</param>
    /// <param name="creationOptions">The CreationOptions</param>
    public Task(Action action, TaskCreationOptions creationOptions) : this(action, null, creationOptions) { }
    /// <summary>
    /// Creates a new Task
    /// </summary>
    /// <param name="action">The action to run</param>
    public Task(Action action) : this(action, null, TaskCreationOptions.None) { }

    /// <summary>
    /// Starts the Task on a given scheduler
    /// </summary>
    /// <param name="scheduler">The TaskScheduler to use</param>
    public void Start(TaskScheduler scheduler)
    {
        lock (_lockObject)
        {
            if (Status != TaskStatus.Created)
                throw new InvalidOperationException("Task already scheduled");

            Status = TaskStatus.WaitingToRun;
        }
        scheduler.QueueTask(this);
    }

    /// <summary>
    /// Starts the Task on the default TaskScheduler
    /// </summary>
    public void Start()
    {
        Start(_scheduler);
    }

    /// <summary>
    /// Creates a TaskWaitOrder for this Task
    /// </summary>
    /// <returns>The TaskWaitOrder</returns>
    public TaskWaitOrder CoWait()
    {
        return new TaskWaitOrder(this);
    }

    /// <summary>
    /// Creates a new yieldable return value
    /// </summary>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="result">The return value</param>
    /// <returns>A TaskResult representing a value</returns>
    public static TaskResult<TResult> SetResult<TResult>(TResult result)
    {
        return new TaskResult<TResult>(result);
    }

    /// <summary>
    /// Throws an exception if the task has faulted
    /// </summary>
    public void Check()
    {
        if (IsFaulted)
            throw Exception;
    }

    /// <summary>
    /// Schedules a continuation for this Task
    /// </summary>
    /// <param name="action">The action to perform</param>
    protected void AddContinuation(Action action)
    {
        lock(_lockObject)
        {
            if (IsCompleted)
                action();
            else
            {
                _continuations.Add(action);
            }
        }
    }

    /// <summary>
    /// Schedules a continuation for this Task
    /// </summary>
    /// <param name="action">The action to perform</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    /// <param name="continuationOptions">The ContinuationOptions to use</param>
    /// <param name="scheduler">The scheduler to run the continuation on</param>
    /// <returns>A Task representing the continuation</returns>
    public Task ContinueWith(Action<Task> action, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions, TaskScheduler scheduler)
    {
        var task = new Task(() => action(this), cancellationToken, TaskCreationOptions.None) { _scheduler = scheduler };
        AddContinuation(() =>
            {
                //TODO: add ContinuationOptions
                task.Start();
            });
        return task;
    }

    /// <summary>
    /// Schedules a continuation for this Task
    /// </summary>
    /// <param name="action">The action to perform</param>
    /// <returns>A Task representing the continuation</returns>
    public Task ContinueWith(Action<Task> action)
    {
        return ContinueWith(action, null, TaskContinuationOptions.None, _scheduler);
    }

    internal void SetException(Exception e)
    {
        lock (_lockObject)
        {
            if (!IsCompleted)
            {
                Exception = e;
                Status = TaskStatus.Faulted;
            }
            else
                throw new InvalidOperationException("Can't set exception on non-running task.");
        }

        RunContinuations();
    }

    protected void RunContinuations()
    {
        List<Action> continuations;
        lock (_lockObject)
            continuations = _continuations.ToList();
        Debug.Log("continue");
        foreach (var action in continuations)
            action();
    }

    internal void SetCompleted()
    {   
        lock(_lockObject)
        {
            if (Status != TaskStatus.Running && Status != TaskStatus.Created && Status != TaskStatus.WaitingToRun)
                throw new InvalidOperationException("Can't set completion on already completed task.");
            Status = TaskStatus.RanToCompletion;
        }

        RunContinuations();
    }

    /// <summary>
    /// Creates a Task that delays for a fixed TimeSpan
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to delay for</param>
    /// <returns>A Task representing a delay</returns>
    public static Task Delay(TimeSpan timeSpan)
    {
        return TaskScheduler.Default.CreateDelay(timeSpan);
    }
}

/// <summary>
/// Represents a Task with a return value
/// </summary>
/// <typeparam name="TResult">The type of the return value</typeparam>
public class Task<TResult> : Task
{
    private TResult _result;
    /// <summary>
    /// The return value of the function
    /// </summary>
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

    /// <summary>
    /// Creates a new Task with a return value
    /// </summary>
    /// <param name="action">The function to perform</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    /// <param name="creationOptions">The CreationOptions to use</param>
    public Task(Func<TResult> action, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(() => {}, cancellationToken, creationOptions)
    {
        this._action = () => _result = action();
    }
    /// <summary>
    /// Creates a new Task with a return value
    /// </summary>
    /// <param name="action">The function to perform</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    public Task(Func<TResult> action, CancellationToken cancellationToken) : this(action, cancellationToken, TaskCreationOptions.None) { }
    /// <summary>
    /// Creates a new Task with a return value
    /// </summary>
    /// <param name="action">The function to perform</param>
    /// <param name="creationOptions">The CreationOptions to use</param>
    public Task(Func<TResult> action, TaskCreationOptions creationOptions) : this(action, null, creationOptions) { }
    /// <summary>
    /// Creates a new Task with a return value
    /// </summary>
    /// <param name="action">The function to perform</param>
    public Task(Func<TResult> action) : this(action, null, TaskCreationOptions.None) { }

    internal void SetResult(TResult result)
    {
        lock (_lockObject)
        {
            if (IsCompleted)
                throw new InvalidOperationException("Can't set result on already completed task.");

            _result = result;
            Status = TaskStatus.RanToCompletion;
        }
        RunContinuations();
    }

    /// <summary>
    /// Creates a continuation on the current Task
    /// </summary>
    /// <param name="action">The action to perform</param>
    /// <param name="cancellationToken">The CancellationToken to use</param>
    /// <param name="continuationOptions">The ContinuationOptions to use</param>
    /// <param name="scheduler">The TaskScheduler to perform the continuation on</param>
    public void ContinueWith(Action<Task<TResult>> action, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions, TaskScheduler scheduler)
    {
        base.ContinueWith((t) => action(t as Task<TResult>), cancellationToken, continuationOptions, scheduler);
    }
    /// <summary>
    /// Creates a continuation on the current Task
    /// </summary>
    /// <param name="action">The action to perform</param>
    public void ContinueWith(Action<Task<TResult>> action)
    {
        ContinueWith(action, null, TaskContinuationOptions.None, _scheduler);
    }
}