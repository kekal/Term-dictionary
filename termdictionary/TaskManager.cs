using System;
using System.Collections.Generic;
using System.Threading;

namespace termdictionary
{
    public class TaskManager
    {
        public readonly List<ActElement> _actList = new List<ActElement>();

        private readonly object _locker = new object();

        public TaskManager()
        {
            var taskManagerThread = new Thread(ActCycle) { Name = "mythread", IsBackground = true };
            taskManagerThread.Start();
        }

        public void Add(Action threadAct, Action mainDispAct = null)
        {
            lock (_locker)
            {
                _actList.Add(new ActElement(threadAct, mainDispAct));
            }
        }


        private void ActCycle()
        {
            while (true)
            {
                if (_actList.Count > 0)
                {
                    lock (_locker)
                    {
                        var currentActElement = _actList[0];
                        _actList.RemoveAt(0);

                        if (currentActElement.ThreadAct != null) currentActElement.ThreadAct();
                        if (currentActElement.InMainAct != null) MainWindow.InMainDispatch(currentActElement.InMainAct);
                    }
                }
                Thread.Sleep(5);
            }
        }
    }

    public class ActElement
    {
        public readonly Action ThreadAct;
        public readonly Action InMainAct;

        public ActElement(Action threadAct, Action inMainAct)
        {
            ThreadAct = threadAct;
            InMainAct = inMainAct;
        }
    }
}
