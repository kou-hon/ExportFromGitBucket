using System.Net.Http.Headers;
using System.Text.Json;

Console.WriteLine("argment:baseUrl, RepositoryPath(Owner/Repo), GitBucketToken");

string baseUrl = args[0];
string repoPath = args[1];
string token = args[2];
if (args.Length < 3)
{
    Console.WriteLine("Not enough arguments");
    Console.WriteLine("e.g. https://sampleUrl/gitbucket/ Owner/Repo hoghogeToken");
    return;
}

string gitBucketApiUrl = $"{baseUrl}/api/v3/repos/{repoPath}";

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);

//GitBucketからIssues/PullRequestsを取得(JSONElements)
//利用している環境だと同時接続10あたりからエラー発生し、スループット悪くなったので、同時接続5に制限している
Console.WriteLine($"Get from {gitBucketApiUrl}");
var issues = await GetIssuesAsync(client, gitBucketApiUrl, token, 5);
var jsonString = JsonSerializer.Serialize(issues, new JsonSerializerOptions { WriteIndented = true });

//ファイルに保存
var filePath = repoPath.Replace('/', '_') + $"_issues_{DateTime.Now:yyMMddhhmmss}.json";
using (var writer = new StreamWriter(filePath, false))
{
    await writer.WriteAsync(jsonString);
}
Console.WriteLine($"Saved to {filePath}");

static async Task<IEnumerable<JsonElement>> GetIssuesAsync(HttpClient client, string baseUrl, string token, int? concurrency = null)
{
    var issues = new List<JsonElement>();

    foreach (var issueType in new[] { "issues", "pulls" })
    {
        var url = $"{baseUrl}/{issueType}";
        foreach (var state in new[] { "open", "closed" })
        {
            var uriBuilder = new UriBuilder(url);

            var page = 0;
            while (true)
            {
                page++;

                uriBuilder.Query = $"state={state}&page={page}";
                Console.WriteLine($"Fetching {uriBuilder.Uri}");
                var response = await client.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var newIssues = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (newIssues == null || newIssues.Count == 0)
                    break;

                //以下取得したissueのコメントを更新していく
                var updateIssues = newIssues.ToList();

                var option = new ParallelOptions();
                if (concurrency.HasValue)
                {
                    option.MaxDegreeOfParallelism = concurrency.Value;
                }
                Parallel.ForEach(newIssues, option, issue =>
                {
                    if (issue.TryGetProperty("comments_url", out var commentsUrlProp))
                    {
                        var commentsUrl = commentsUrlProp.GetString();
                        if (!string.IsNullOrEmpty(commentsUrl))
                        {
                            int retry = 0;
                        Retry:
                            try
                            {
                                using var cts = new CancellationTokenSource();
                                cts.CancelAfter(10000);
                                using var commentResponse = client.GetAsync(commentsUrl, cts.Token).Result;
                                commentResponse.EnsureSuccessStatusCode();
                                var commentsJson = commentResponse.Content.ReadAsStringAsync().Result;
                                var comments = JsonSerializer.Deserialize<JsonElement>(commentsJson);
                                // JsonElementの加工のために、いったんDictionaryとして扱う
                                var dict = issue.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                                dict["comments"] = comments;

                                var updatedJson = JsonSerializer.Serialize(dict);
                                var updatedIssue = JsonSerializer.Deserialize<JsonElement>(updatedJson);
                                lock (updateIssues)
                                {
                                    updateIssues[updateIssues.IndexOf(issue)] = updatedIssue;
                                }
                            }
                            catch
                            {
                                Console.WriteLine($"Failed to fetch comments from {commentsUrl}(retry:{retry})");
                                retry++;
                                if (retry > 3) throw new Exception("Failed to fetch comments after 3 retries");
                                goto Retry;
                            }
                        }
                    }
                });

                issues.AddRange(updateIssues);
            }
        }
    }

    return issues.OrderBy(i => i.GetProperty("number").GetInt32());
}
