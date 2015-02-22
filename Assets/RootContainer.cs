using UnityEngine;
using System.Collections;
using System;

public class RootContainer : MonoBehaviour {
    bool error = false;
	// Use this for initialization
	void Start () {
        Debug.Log("Start!");

        var taskScheduler = new TaskScheduler(this);
        error = true;
        var t = Test().Run();

        Action<Task> a = (task) =>
        {
            Debug.Log("start");
            task.Check();
            Debug.Log("hi");
        };

        var next = t.ContinueWith(a);
	}

    IEnumerator Test()
    {
        if(error)
            throw new Exception();
        yield return Task.Delay(TimeSpan.Zero).CoWait();
    }

	// Update is called once per frame
	void Update () {
	
	}
}
