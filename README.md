[![.NET](https://github.com/kou-hon/ExportFromGitBucket/actions/workflows/dotnet.yml/badge.svg)](https://github.com/kou-hon/ExportFromGitBucket/actions/workflows/dotnet.yml)
[![.NET Build and Release](https://github.com/kou-hon/ExportFromGitBucket/actions/workflows/BuildAndRelease.yml/badge.svg)](https://github.com/kou-hon/ExportFromGitBucket/actions/workflows/BuildAndRelease.yml)

# ExportFromGitBucket
Issue/PullRequest取得

GitHubに移管した際、CommitコメントとのリンクがおかしくなるのでGitHubへIssue/PullRequestのデータも移管したい

## 事前準備

リポジトリアクセス可能なgitbucketのTokenを取得しておくこと

## 使い方

```
$ ExportFromGitBucket.exe https://hogehoge.com/gitbucket/ OWNER/REPO tokensomething
```

## 出力

OWNER_Repo_issues_250918030836.json
