using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;

namespace TaskTest
{
    [TestClass]
    public class SchedulerIntegration
    {
        class MonoBehaviour
        {
            IEnumerator i;
            public MonoBehaviour()
            {
            }

            public new UnityEngine.Coroutine StartCoroutine_Auto(IEnumerator enumerator)
            {
                i = enumerator;
                return null;
            }
        }

        [TestMethod]
        public void TestMethod1()
        {
            MonoBehaviour r = new MonoBehaviour();
            TaskScheduler s = new TaskScheduler(r);

            
        }
    }
}
