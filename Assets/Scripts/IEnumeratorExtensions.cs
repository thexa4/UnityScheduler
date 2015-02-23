using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class IEnumeratorExtensions
{
    /// <summary>
    /// Runs an IEnumerable as coroutine
    /// </summary>
    /// <typeparam name="TResult">The type of the return parameter</typeparam>
    /// <param name="enumerator">The enumerator to run</param>
    /// <returns>A Task representing this enumerator</returns>
    public static Task<TResult> Run<TResult>(this IEnumerator enumerator)
    {
        var t = TaskScheduler.Default.Run<TResult>(enumerator);
        return t;
    }

    /// <summary>
    /// Runs an IEnumerable as coroutine
    /// </summary>
    /// <param name="enumerator">The enumerator to run</param>
    /// <returns>A Task representing this enumerator</returns>
    public static Task Run(this IEnumerator enumerator)
    {
        var t = TaskScheduler.Default.Run(enumerator);
        return t;
    }
}