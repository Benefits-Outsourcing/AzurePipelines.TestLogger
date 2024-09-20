using System;
using System.Collections.Generic;
using System.Linq;
using AzurePipelines.TestLogger.Json;

namespace AzurePipelines.TestLogger
{
    public class ApiClientV3 : ApiClient
    {
        public ApiClientV3(string collectionUri, string teamProject, string apiVersionString)
            : base(collectionUri, teamProject, apiVersionString)
        {
        }

        public override string GetTestResults(
            Dictionary<string, TestResultParent> testCaseTestResults,
            IEnumerable<IGrouping<string, ITestResult>> testResultsByParent,
            DateTime completedDate)
        {
            // https://docs.microsoft.com/en-us/azure/devops/integrate/previous-apis/test/results?view=tfs-2015#update-test-results-for-a-test-run
            return "[ " + string.Join(", ", testResultsByParent.Select(x =>
            {
                TestResultParent parent = testCaseTestResults[x.Key];
                return string.Join(", ", x.Select(y =>
                {
                    Dictionary<string, object> testResultProperties = GetTestResultProperties(y);
                    testResultProperties.Add("TestResult", new Dictionary<string, object> { { "Id", parent.Id } });
                    testResultProperties.Add("id", y.TestCaseId);

                    return testResultProperties.ToJson();
                }));
            })) + " ]";
        }
    }
}