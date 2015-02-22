using UnityEngine;
using System.Collections;
using System;

public class RootContainer : MonoBehaviour {

	// Use this for initialization
	void Start () {
        Debug.Log("Start!");

        var taskScheduler = new TaskScheduler(this);

        var t = Create("max", "pass");
        t.ContinueWith((j) =>
        {
            throw j.Exception;
        });

	}

	IEnumerator CoLogin(string username, string password)
    {
        yield return Task.Delay(TimeSpan.FromSeconds(1));
        yield return Task.SetResult<string>("Verysecretaccesstoken!");
    }

    IEnumerator CreateAccount(string username, string password, string accesstoken)
    {
        yield return Task.Delay(TimeSpan.FromSeconds(1));
        throw new UnauthorizedAccessException("Access token not secret enough");
    }

    IEnumerator CoLoginAndCreate(string username, string password)
    {
        var accessToken = CoLogin(username, password).Run<string>();
        yield return accessToken.CoWait();
        var account = CreateAccount(username, password, accessToken.Result).Run();
        yield return account.CoWait();
    }

    Task Create(string username, string password)
    {
        return CoLoginAndCreate(username, password).Run();
    }

	// Update is called once per frame
	void Update () {
	
	}
}
