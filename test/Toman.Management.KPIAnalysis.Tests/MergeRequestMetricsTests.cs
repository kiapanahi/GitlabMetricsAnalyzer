using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests;

/// <summary>
/// Tests for merge request metrics accuracy fixes (Issue: Fix Merge Request Metrics - Replace Approximations with Accurate GitLab Data)
/// </summary>
public class MergeRequestMetricsTests
{
    [Fact]
    public async Task CalculateCodeReviewMetricsAsync_Should_Count_Merged_MRs_Correctly()
    {
        // Arrange
        var mergeRequests = new List<RawMergeRequest>
        {
            // Merged MR
            new()
            {
                Id = 1,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 1,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature A",
                CreatedAt = DateTimeOffset.Now.AddDays(-5),
                MergedAt = DateTimeOffset.Now.AddDays(-1), // Merged
                ClosedAt = null,
                State = "merged",
                ChangesCount = 50,
                SourceBranch = "feature-a",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 1,
                FirstReviewAt = DateTimeOffset.Now.AddDays(-2),
                ReviewerIds = "[2]",
                IngestedAt = DateTimeOffset.Now
            },
            // Another merged MR
            new()
            {
                Id = 2,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 2,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature B",
                CreatedAt = DateTimeOffset.Now.AddDays(-3),
                MergedAt = DateTimeOffset.Now.AddHours(-6), // Merged
                ClosedAt = null,
                State = "merged",
                ChangesCount = 25,
                SourceBranch = "feature-b",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 2,
                FirstReviewAt = DateTimeOffset.Now.AddDays(-1),
                ReviewerIds = "[2,3]",
                IngestedAt = DateTimeOffset.Now
            },
            // Open MR (not merged)
            new()
            {
                Id = 3,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 3,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature C",
                CreatedAt = DateTimeOffset.Now.AddDays(-1),
                MergedAt = null, // Not merged
                ClosedAt = null,
                State = "opened",
                ChangesCount = 15,
                SourceBranch = "feature-c",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 0,
                FirstReviewAt = null,
                ReviewerIds = null,
                IngestedAt = DateTimeOffset.Now
            },
            // Closed but not merged MR
            new()
            {
                Id = 4,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 4,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature D",
                CreatedAt = DateTimeOffset.Now.AddDays(-4),
                MergedAt = null, // Not merged
                ClosedAt = DateTimeOffset.Now.AddDays(-2),
                State = "closed",
                ChangesCount = 30,
                SourceBranch = "feature-d",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 0,
                FirstReviewAt = DateTimeOffset.Now.AddDays(-3),
                ReviewerIds = null,
                IngestedAt = DateTimeOffset.Now
            }
        };

        var reviewedMRs = new List<RawMergeRequest>(); // Empty for this test

        // Create mock database context
        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new GitLabMetricsDbContext(options);

        var gitLabService = new Mock<IGitLabService>().Object;
        var logger = new Mock<ILogger<UserMetricsService>>().Object;
        var service = new UserMetricsService(context, gitLabService, logger);

        // Act - Use reflection to call the private async method
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeReviewMetricsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var result = await (Task<UserCodeReviewMetrics>)method!.Invoke(service, new object[]
            { mergeRequests, reviewedMRs, CancellationToken.None })!;

        // Assert
        Assert.Equal(4, result.MergeRequestsCreated); // 4 total MRs created
        Assert.Equal(2, result.MergeRequestsMerged); // 2 MRs merged (ID 1 and 2)
        Assert.Equal(0.5, result.MergeRequestMergeRate); // 2 merged / 4 created = 0.5 (50%)

        // Additional assertions
        Assert.Equal(0, result.MergeRequestsReviewed); // No reviewed MRs in this test
        Assert.Equal(3, result.ApprovalsReceived); // 1 + 2 + 0 + 0 from the 4 MRs
    }

    [Fact]
    public async Task CalculateCodeReviewMetricsAsync_Should_Handle_No_MRs()
    {
        // Arrange
        var mergeRequests = new List<RawMergeRequest>();
        var reviewedMRs = new List<RawMergeRequest>();

        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new GitLabMetricsDbContext(options);
        var gitLabService = new Mock<IGitLabService>().Object;
        var logger = new Mock<ILogger<UserMetricsService>>().Object;
        var service = new UserMetricsService(context, gitLabService, logger);

        // Act
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeReviewMetricsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var result = await (Task<UserCodeReviewMetrics>)method!.Invoke(service, new object[]
            { mergeRequests, reviewedMRs, CancellationToken.None })!;

        // Assert
        Assert.Equal(0, result.MergeRequestsCreated);
        Assert.Equal(0, result.MergeRequestsMerged);
        Assert.Equal(0, result.MergeRequestMergeRate); // 0 merged / 0 created = 0
    }

    [Fact]
    public async Task CalculateCodeReviewMetricsAsync_Should_Calculate_100_Percent_Merge_Rate()
    {
        // Arrange - All MRs are merged
        var mergeRequests = new List<RawMergeRequest>
        {
            new()
            {
                Id = 1,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 1,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature A",
                CreatedAt = DateTimeOffset.Now.AddDays(-2),
                MergedAt = DateTimeOffset.Now.AddDays(-1),
                ClosedAt = null,
                State = "merged",
                ChangesCount = 20,
                SourceBranch = "feature-a",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 1,
                FirstReviewAt = DateTimeOffset.Now.AddDays(-1),
                ReviewerIds = "[2]",
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 2,
                ProjectId = 100,
                ProjectName = "Test Project",
                MrId = 2,
                AuthorUserId = 1,
                AuthorName = "John Doe",
                Title = "Feature B",
                CreatedAt = DateTimeOffset.Now.AddDays(-3),
                MergedAt = DateTimeOffset.Now.AddHours(-12),
                ClosedAt = null,
                State = "merged",
                ChangesCount = 35,
                SourceBranch = "feature-b",
                TargetBranch = "main",
                ApprovalsRequired = 1,
                ApprovalsGiven = 1,
                FirstReviewAt = DateTimeOffset.Now.AddDays(-2),
                ReviewerIds = "[3]",
                IngestedAt = DateTimeOffset.Now
            }
        };

        var reviewedMRs = new List<RawMergeRequest>();

        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new GitLabMetricsDbContext(options);
        var gitLabService = new Mock<IGitLabService>().Object;
        var logger = new Mock<ILogger<UserMetricsService>>().Object;
        var service = new UserMetricsService(context, gitLabService, logger);

        // Act
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeReviewMetricsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var result = await (Task<UserCodeReviewMetrics>)method!.Invoke(service, new object[]
            { mergeRequests, reviewedMRs, CancellationToken.None })!;

        // Assert
        Assert.Equal(2, result.MergeRequestsCreated);
        Assert.Equal(2, result.MergeRequestsMerged);
        Assert.Equal(1.0, result.MergeRequestMergeRate); // 2 merged / 2 created = 1.0 (100%)
    }
}
