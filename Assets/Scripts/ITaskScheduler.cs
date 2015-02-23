using System;

public interface ITaskScheduler
{
    void QueueTask(Task task);
}
