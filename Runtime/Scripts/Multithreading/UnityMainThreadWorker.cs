using System;
using System.Collections.Concurrent;

namespace UnityCTVisualizer {
    /// <summary>
    ///     Dispatches jobs on Unity's main worker thread. This is useful for updating
    ///     Unity-Engine-related components (e.g., UIs) from a different thread. Attempting
    ///     such updates directly in their origin threads is not possible.
    /// </summary>
    public class UnityMainThreadWorker {
        private ConcurrentQueue<Action> m_jobs = new();

        public void TryRunJobs() {
            while (m_jobs.TryDequeue(out Action job)) {
                job.Invoke();
            }
        }

        public void AddJob(Action job) {
            m_jobs.Enqueue(job);
        }
    }
}
