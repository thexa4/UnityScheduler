using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// Schedules Task execution on the Unity thread
/// </summary>
public class TaskScheduler : ITaskScheduler
{
    /// <summary>
    /// The default (first) instance of this TaskScheduler
    /// </summary>
    public static TaskScheduler Default
    {
        get
        {
            if (_instance == null)
                throw new InvalidOperationException("No default scheduler created");
            return _instance;
        }
    }
    private static TaskScheduler _instance;

    /// <summary>
    /// Wether this TaskScheduler is still running
    /// </summary>
    public bool IsRunning
    {
        get { return _running; }
        set
        {
            if (value == false)
                _running = value;
        }
    }

    private MonoBehaviour _container;
    private bool _running;
    private List<Action> _queue = new List<Action>();

    /// <summary>
    /// Creates a new TaskScheduler and binds it to a MonoBehaviour container
    /// </summary>
    /// <param name="container">The MonoBehaviour to bind to</param>
    public TaskScheduler(MonoBehaviour container)
    {
        _container = container;

        if (_instance == null)
            _instance = this;

        _running = true;
        container.StartCoroutine(ProcessTasks());
    }

    /// <summary>
    /// Creates a Delay Task on the Unity thread
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to delay for</param>
    /// <returns>A Task representing the delay</returns>
    internal Task CreateDelay(TimeSpan timeSpan)
    {
        return Run(Sleep(timeSpan));
    }

    IEnumerator Sleep(TimeSpan timeSpan)
    {
        yield return new WaitForSeconds((float)timeSpan.TotalSeconds);
    }

    private void Queue(Action action)
    {
        lock (_queue)
            _queue.Add(action);
    }

    /// <summary>
    /// Schedules a Task to run on this TaskScheduler
    /// </summary>
    /// <param name="task"></param>
    public void QueueTask(Task task)
    {
        Queue(task.Execution);
    }

    IEnumerator ProcessTasks()
    {
        while (true)
        {
            List<Action> copy;
            lock (_queue)
            {
                copy = _queue.ToList();
                _queue.Clear();
            }
            foreach (var action in copy)
                action();
            yield return null;
        }
    }

    IEnumerator WrapIEnumerable<TResult>(IEnumerator other, Task<TResult> task)
    {
        bool next = true;
        while (next)
        {
            try
            {
                next = other.MoveNext();
            }
            catch (Exception e)
            {
                task.SetException(e);
                yield break;
            }

            if (other.Current is Task.TaskWaitOrder)
            {
                var waitfor = (other.Current as Task.TaskWaitOrder).Task;
                waitfor.ContinueWith((t) =>
                    {
                        _container.StartCoroutine(WrapIEnumerable(other, task));
                    });
                yield break;
            }

            if (other.Current is Task.TaskResult<TResult>)
            {
                task.SetResult((other.Current as Task.TaskResult<TResult>).Result);
                yield break;
            }

            yield return other.Current;
        }

        task.SetResult(default(TResult));
    }

    IEnumerator WrapIEnumerable(IEnumerator other, Task task)
    {
        bool next = true;
        while (next)
        {
            try
            {
                next = other.MoveNext();
            }
            catch (Exception e)
            {
                task.SetException(e);
                yield break;
            }
            if (other.Current is Task.TaskWaitOrder)
            {
                var waitfor = (other.Current as Task.TaskWaitOrder).Task;
                waitfor.ContinueWith((t) => _container.StartCoroutine(WrapIEnumerable(other, task)));
                yield break;
            }

            yield return other.Current;
        }
        task.SetCompleted();
    }

    /// <summary>
    /// Runs a IEnumerator on the Unity thread
    /// </summary>
    /// <typeparam name="TResult">The return type of the IEnumerator</typeparam>
    /// <param name="enumerator">The enumerator to run</param>
    /// <returns>A Task representing the execution</returns>
    public Task<TResult> Run<TResult>(IEnumerator enumerator)
    {
        var result = new Task<TResult>(() => default(TResult));
        _container.StartCoroutine(WrapIEnumerable(enumerator, result));
        return result;
    }
    /// <summary>
    /// Runs a IEnumerator on the Unity thread
    /// </summary>
    /// <param name="enumerator">The enumerator to run</param>
    /// <returns>A Task representing the execution</returns>
    public Task Run(IEnumerator enumerator)
    {
        var result = new Task(() => {});
        _container.StartCoroutine(WrapIEnumerable(enumerator, result));
        return result;
    }
}
