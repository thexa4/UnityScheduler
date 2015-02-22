using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class TaskScheduler
{
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

    public TaskScheduler(MonoBehaviour container)
    {
        _container = container;

        if (_instance == null)
            _instance = this;

        _running = true;
        container.StartCoroutine(ProcessTasks());
    }

    internal Task CreateDelay(TimeSpan timeSpan)
    {
        return Run(Sleep(timeSpan));
    }

    IEnumerator Sleep(TimeSpan timeSpan)
    {
        yield return new WaitForSeconds((float)timeSpan.TotalSeconds);
    }

    internal void Queue(Action action)
    {
        lock (_queue)
            _queue.Add(action);
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
        bool next;
        try
        {
            next = other.MoveNext();
        }
        catch (Exception e)
        {
            task.SetException(e);
            yield break;
        }

        while (next)
        {
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
        bool next;
        try
        {
            next = other.MoveNext();
        }
        catch (Exception e)
        {
            task.SetException(e);
            yield break;
        }

        while (next)
        {
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

    public Task<TResult> Run<TResult>(IEnumerator enumerator)
    {
        var result = new Task<TResult>(() => default(TResult));
        _container.StartCoroutine(WrapIEnumerable(enumerator, result));
        return result;
    }

    public Task Run(IEnumerator enumerator)
    {
        var result = new Task(() => {});
        _container.StartCoroutine(WrapIEnumerable(enumerator, result));
        return result;
    }
}
