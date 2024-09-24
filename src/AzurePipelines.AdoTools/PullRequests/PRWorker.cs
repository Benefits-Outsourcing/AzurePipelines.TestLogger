using AzurePipelines.TestLogger;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AzurePipelines.AdoTools.PullRequests
{
    internal class PRWorker(IApiClient client)
    {
        private readonly string orgUrl = "https://dev.azure.com/wtw-bda-outsourcing-product";
        private readonly string projectName = "BenefitConnect";

        public async Task Report(string userEmail, string repositoryName = "ESS")
        {
            GitHttpClient gitClient = client.GetClient<GitHttpClient>();

            // Get the repository
            var repositories = await gitClient.GetRepositoriesAsync(projectName);
            var repository = repositories.FirstOrDefault(repo => repo.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

            if (repository == null)
            {
                Console.WriteLine($"Repository '{repositoryName}' not found.");
                return;
            }

            // Convert memberEmails to a HashSet for efficient lookup
            HashSet<string> memberSet = new([userEmail], StringComparer.OrdinalIgnoreCase);

            // Get pull requests
            var pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, new GitPullRequestSearchCriteria { Status = PullRequestStatus.Completed, MinTime = DateTime.UtcNow.AddDays(-120) });

            // Initialize a dictionary to count reviews per user
            Dictionary<string, int> reviewCounts = new Dictionary<string, int>();
            Dictionary<string, int> prCounts = new Dictionary<string, int>();

            foreach (var pr in pullRequests)
            {

                if (pr.CreatedBy.UniqueName.ToLower().Contains(userEmail))
                {
                    if (prCounts.ContainsKey(pr.CreatedBy.UniqueName))
                    {
                        prCounts[pr.CreatedBy.UniqueName]++;
                    }
                    else
                    {
                        prCounts[pr.CreatedBy.UniqueName] = 1;
                    }
                }

                // Get the PR reviews
                var reviews = await gitClient.GetPullRequestReviewersAsync(repository.ProjectReference.Id, repository.Id, pr.PullRequestId);

                foreach (var review in reviews)
                {
                    // Check if the reviewer is in the list of specified members
                    if (memberSet.Any(x => review.UniqueName.Contains(x)))
                    {
                        // Increment the review count for the user
                        if (reviewCounts.ContainsKey(review.UniqueName))
                        {
                            reviewCounts[review.UniqueName]++;
                        }
                        else
                        {
                            reviewCounts[review.UniqueName] = 1;
                        }
                    }
                }
            }

            // Print the review counts for members
            foreach (var kvp in reviewCounts)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }

            // Print the PR counts for members
            foreach (var kvp in prCounts)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }
        }
    }
}
