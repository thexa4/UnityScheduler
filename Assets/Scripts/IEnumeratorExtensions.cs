using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class IEnumeratorExtensions
{
    public static Task<TResult> Run<TResult>(this IEnumerator enumerator)
    {
        var t = TaskScheduler.Default.Run<TResult>(enumerator);
        t.Start();
        return t;
    }

    public static Task Run(this IEnumerator enumerator)
    {
        var t = TaskScheduler.Default.Run(enumerator);
        t.Start();
        return t;
    }
}