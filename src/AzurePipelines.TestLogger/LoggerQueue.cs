using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly bool _groupTestResultsByClassName;
        private readonly bool _isReRun;
        private readonly bool _isPipeline;

        // public for testing
        public Dictionary<string, TestResultParent> Parents { get; } = new Dictionary<string, TestResultParent>();
        public DateTime StartedDate { get; private set; }
        public int RunId { get; set; }
        public string Source { get; set; }

        public LoggerQueue(IApiClient apiClient, int runId, string agentName, string jobName, bool groupTestResultsByClassName = true, bool isReRun = false, bool isPipeline = false)
        {
            _apiClient = apiClient;
            _agentName = agentName;
            _jobName = jobName;
            _groupTestResultsByClassName = groupTestResultsByClassName;
            RunId = runId;
            _isReRun = isReRun;
            _isPipeline = isPipeline;
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

                    //await SendResultsAsync(nextItems, cancellationToken).ConfigureAwait(false);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal error in LoggerQueue.ConsumeItemsAsync");
                    Console.WriteLine(ex);
                    Console.WriteLine(ex.StackTrace);
                    return;
                }
            }
        }


        private async Task SendResultsAsync(ITestResult[] testResults, CancellationToken cancellationToken)
        {
            // Group results by their parent
            IEnumerable<IGrouping<string, ITestResult>> testResultsByParent = GroupTestResultsByParent(testResults);

            // Create any required parent nodes
            await CreateParents(testResultsByParent, cancellationToken).ConfigureAwait(false);

            // Update parents with the test results
            await SendTestResults(testResultsByParent, cancellationToken).ConfigureAwait(false);
        }



        public IEnumerable<IGrouping<string, ITestResult>> GroupTestResultsByParent(ITestResult[] testResults) =>
            testResults.GroupBy(x =>
            {
                // Namespace.ClassName.MethodName
                string name = x.FullyQualifiedName;

                if (Source != null && name.StartsWith(Source + "."))
                {
                    // remove the namespace
                    name = name.Substring(Source.Length + 1);
                }

                // At this point, name should always have at least one '.' to represent the Class.Method
                if (!_groupTestResultsByClassName)
                {
                    return name;
                }

                // We need to start at the opening method if there is one
                int startIndex = name.IndexOf('(');
                if (startIndex < 0)
                {
                    startIndex = name.Length - 1;
                }

                // remove the method name to get just the class name
                return name.Substring(0, name.LastIndexOf('.', startIndex));
            });

        public async Task CreateParents(IEnumerable<IGrouping<string, ITestResult>> testResultsByParent, CancellationToken cancellationToken)
        {
            // Find the parents that don't exist
            string[] parentsToAdd = testResultsByParent
                .Select(x => x.Key)
                .Where(x => !Parents.ContainsKey(x))
                .ToArray();

            // Batch an add operation and record the new parent IDs
            DateTime startedDate = DateTime.UtcNow;
            if (parentsToAdd.Length > 0)
            {
                int[] parents = await _apiClient.AddTestCases(RunId, parentsToAdd, startedDate, Source, cancellationToken).ConfigureAwait(false);
                for (int i = 0; i < parents.Length; i++)
                {
                    Parents.Add(parentsToAdd[i], new TestResultParent(parents[i], startedDate));
                }
            }
        }

        private Task SendTestResults(IEnumerable<IGrouping<string, ITestResult>> testResultsByParent, CancellationToken cancellationToken)
        {
            return _apiClient.UpdateTestResults(RunId, Parents, testResultsByParent, cancellationToken);
        }

        private async Task SendTestsCompleted(VstpTestRunComplete testRunComplete, CancellationToken cancellationToken)
        {

            // Mark all parents as completed (but only if we actually created a parent)
            //if (RunId != 0)
            //{
            //    await _apiClient.UpdateTestResults(RunId, testRunComplete, cancellationToken);

            //    await _apiClient.MarkTestRunCompleted(RunId, testRunComplete.Aborted, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
            //}

            if(!_isPipeline) {
                await _apiClient.MarkTestRunCompleted(RunId, testRunComplete.Aborted, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}