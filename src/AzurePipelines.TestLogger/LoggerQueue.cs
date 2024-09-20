using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AzurePipelines.TestLogger
{
    public class LoggerQueue
    {
        private readonly AsyncProducerConsumerCollection<ITestResult> _queue = new AsyncProducerConsumerCollection<ITestResult>();
        private readonly Task _consumeTask;
        private readonly CancellationTokenSource _consumeTaskCancellationSource = new CancellationTokenSource();

        private readonly IApiClient _apiClient;
        private readonly int _buildId;
        private readonly string _agentName;
        private readonly string _jobName;
        private readonly bool _isPipeline;

        // public for testing
        public Dictionary<string, TestResultParent> Parents { get; } = new Dictionary<string, TestResultParent>();
        public DateTime StartedDate { get; private set; }
        public int RunId { get; set; }
        public string Source { get; set; }

        private readonly int Iteration;
        private readonly int MaxIteration;

        public LoggerQueue(IApiClient apiClient, int runId, string agentName, string jobName, int iteration, int maxIteration, bool isPipeline = false)
        {
            _apiClient = apiClient;
            _agentName = agentName;
            _jobName = jobName;
            RunId = runId;
            _isPipeline = isPipeline;
            Iteration = iteration;
            MaxIteration = maxIteration;
            _consumeTask = ConsumeItemsAsync(_consumeTaskCancellationSource.Token);
        }

        public void Enqueue(ITestResult testResult) => _queue.Add(testResult);

        public void Flush(VstpTestRunComplete testRunComplete)
        {
            // Cancel any idle consumers and let them return
            _queue.Cancel();

            // Any active consumer will circle back around and batch post the remaining queue
            _consumeTask.Wait(TimeSpan.FromSeconds(60));

            // Update the run and parents to a completed state
            SendTestsCompleted(testRunComplete, _consumeTaskCancellationSource.Token).Wait(TimeSpan.FromSeconds(60));

            // Cancel any active HTTP requests if still hasn't finished flushing
            _consumeTaskCancellationSource.Cancel();
            if (!_consumeTask.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Cancellation didn't happen quickly");
            }
        }

        private async Task ConsumeItemsAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    ITestResult[] nextItems = await _queue.TakeAsync().ConfigureAwait(false);

                    if (nextItems == null || nextItems.Length == 0)
                    {
                        // Queue is canceling and is empty
                        return;
                    }

                    await _apiClient.AddTestCases(RunId, nextItems).ConfigureAwait(false);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal error in LoggerQueue.ConsumeItemsAsync");
                    Console.WriteLine(ex);
                    Console.WriteLine(ex.StackTrace);
                    throw;
                }
            }
        }

        private async Task SendTestsCompleted(VstpTestRunComplete testRunComplete, CancellationToken cancellationToken)
        {
            if (!_isPipeline)
            {
                await _apiClient.MarkTestRunCompleted(RunId, testRunComplete.Aborted, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}