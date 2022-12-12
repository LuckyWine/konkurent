using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;


namespace CustomThreadPool
{
    public class MyThreadPool : IThreadPool
    {
        private List<Thread> workers = new List<Thread>();
        private Dictionary<int, WorkStealingQueue<Action>> localQueues = new Dictionary<int, WorkStealingQueue<Action>>();
        private Queue<Action> generalQueue = new Queue<Action>();
        private long processedTaskCount;
        
        public long GetTasksProcessedCount()
            => processedTaskCount;

		public void EnqueueAction(Action action)
        {
            var id = Thread.CurrentThread.ManagedThreadId;
            if (localQueues.ContainsKey(id))
                localQueues[id].LocalPush(action);
            else
            {
                lock (generalQueue)
                {
                    generalQueue.Enqueue(action);
                    Monitor.Pulse(generalQueue);
                }
            }
        }

		private void CreateWorker()
        {
            var id = Thread.CurrentThread.ManagedThreadId;
            while (true)
            {
                Action task = null;
                if (localQueues[id].LocalPop(ref task))
                {
                    task();
                    Interlocked.Increment(ref processedTaskCount);
                }

                else
                {
                    lock (generalQueue)
                    {
                        if (generalQueue.TryDequeue(out task))
                            localQueues[id].LocalPush(task);
                        else if (!localQueues.Any(worker => worker.Key != id && !worker.Value.IsEmpty))
                            Monitor.Wait(generalQueue);
                    }

                    if (task is null)
                    {
                        var stealingQueue = localQueues
                            .Where(worker => worker.Key != id && !worker.Value.IsEmpty)
                            .Select(worker => worker.Value).FirstOrDefault();
                        if (stealingQueue is null || !stealingQueue.TrySteal(ref task))
                            continue;
                        localQueues[id].LocalPush(task);
                    }
                }
            }
        }

        public MyThreadPool()
        {
            for (var i = 0; i < Environment.ProcessorCount * 2; i++)
                workers.Add(new Thread(CreateWorker) { IsBackground = true });
            foreach (var worker in workers)
                localQueues[worker.ManagedThreadId] = new WorkStealingQueue<Action>();
            foreach (var worker in workers)
                worker.Start();
        }
    }
}
