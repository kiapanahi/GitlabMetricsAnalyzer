using System.Reflection;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests;

/// <summary>
/// Tests for UserMetricsService focused on Active Projects metric accuracy (Issue #9)
/// </summary>
public class UserMetricsServiceTests
{
    [Fact]
    public void CalculateCodeContributionMetrics_Should_Count_Distinct_Projects()
    {
        // Arrange
        var commits = new List<RawCommit>
        {
            new()
            {
                Id = 1,
                ProjectId = 100, // Project A
                ProjectName = "Project A",
                CommitId = "commit1",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                AuthorEmail = "john@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "Fix bug",
                Additions = 10,
                Deletions = 5,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 2,
                ProjectId = 100, // Same project A
                ProjectName = "Project A",
                CommitId = "commit2",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                AuthorEmail = "john@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "Add feature",
                Additions = 20,
                Deletions = 0,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 3,
                ProjectId = 200, // Project B
                ProjectName = "Project B",
                CommitId = "commit3",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                AuthorEmail = "john@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "Refactor code",
                Additions = 15,
                Deletions = 10,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 4,
                ProjectId = 300, // Project C
                ProjectName = "Project C",
                CommitId = "commit4",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                AuthorEmail = "john@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "Update docs",
                Additions = 5,
                Deletions = 2,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            }
        };

        var fromDate = DateTimeOffset.Now.AddDays(-30);
        var toDate = DateTimeOffset.Now;

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeContributionMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserCodeContributionMetrics)method!.Invoke(null, new object[] { commits, fromDate, toDate })!;

        // Assert - Verify the fix: distinct project count instead of commit count
        Assert.Equal(4, result.TotalCommits); // 4 commits total
        Assert.Equal(3, result.FilesModified); // 3 distinct projects (100, 200, 300) - THIS IS THE FIX!
        Assert.Equal(50, result.TotalLinesAdded); // 10 + 20 + 15 + 5
        Assert.Equal(17, result.TotalLinesDeleted); // 5 + 0 + 10 + 2
        Assert.Equal(67, result.TotalLinesChanged); // 50 + 17
    }

    [Fact]
    public void CalculateCodeContributionMetrics_Should_Return_Zero_Projects_For_Empty_Commits()
    {
        // Arrange
        var commits = new List<RawCommit>();
        var fromDate = DateTimeOffset.Now.AddDays(-30);
        var toDate = DateTimeOffset.Now;

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeContributionMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserCodeContributionMetrics)method!.Invoke(null, new object[] { commits, fromDate, toDate })!;

        // Assert
        Assert.Equal(0, result.TotalCommits);
        Assert.Equal(0, result.FilesModified); // Should be 0 distinct projects
        Assert.Equal(0, result.TotalLinesAdded);
        Assert.Equal(0, result.TotalLinesDeleted);
        Assert.Equal(0, result.TotalLinesChanged);
    }

    [Fact]
    public void CalculateCodeContributionMetrics_Should_Count_Single_Project_Multiple_Commits()
    {
        // Arrange - Multiple commits in the same project
        var commits = new List<RawCommit>
        {
            new()
            {
                Id = 1,
                ProjectId = 100,
                ProjectName = "Single Project",
                CommitId = "commit1",
                AuthorUserId = 1,
                AuthorName = "Jane Doe",
                AuthorEmail = "jane@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "First commit",
                Additions = 10,
                Deletions = 0,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 2,
                ProjectId = 100, // Same project
                ProjectName = "Single Project",
                CommitId = "commit2",
                AuthorUserId = 1,
                AuthorName = "Jane Doe",
                AuthorEmail = "jane@example.com",
                CommittedAt = DateTimeOffset.Now.AddDays(-1),
                Message = "Second commit",
                Additions = 5,
                Deletions = 2,
                IsSigned = false,
                IngestedAt = DateTimeOffset.Now
            }
        };

        var fromDate = DateTimeOffset.Now.AddDays(-30);
        var toDate = DateTimeOffset.Now;

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateCodeContributionMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserCodeContributionMetrics)method!.Invoke(null, new object[] { commits, fromDate, toDate })!;

        // Assert - Before fix this would be 2 (commit count), now should be 1 (distinct projects)
        Assert.Equal(2, result.TotalCommits); // 2 commits total
        Assert.Equal(1, result.FilesModified); // 1 distinct project - MAIN ASSERTION FOR THE FIX
    }

    [Fact]
    public void CalculateQualityMetrics_Should_Calculate_Correct_Pipeline_Counts()
    {
        // Arrange - Create test pipeline data
        var pipelines = new List<RawPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = 100,
                ProjectName = "Test Project",
                PipelineId = 1001,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                TriggerSource = "push",
                CreatedAt = DateTimeOffset.Now.AddDays(-1),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                StartedAt = DateTimeOffset.Now.AddDays(-1),
                FinishedAt = DateTimeOffset.Now.AddDays(-1).AddMinutes(5),
                DurationSec = 300,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 2,
                ProjectId = 100,
                ProjectName = "Test Project",
                PipelineId = 1002,
                Sha = "def456",
                Ref = "main",
                Status = "failed",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                TriggerSource = "push",
                CreatedAt = DateTimeOffset.Now.AddDays(-1),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                StartedAt = DateTimeOffset.Now.AddDays(-1),
                FinishedAt = DateTimeOffset.Now.AddDays(-1).AddMinutes(3),
                DurationSec = 180,
                IngestedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = 3,
                ProjectId = 100,
                ProjectName = "Test Project",
                PipelineId = 1003,
                Sha = "ghi789",
                Ref = "feature",
                Status = "success",
                AuthorUserId = 1,
                AuthorName = "John Doe",
                TriggerSource = "push",
                CreatedAt = DateTimeOffset.Now.AddDays(-1),
                UpdatedAt = DateTimeOffset.Now.AddDays(-1),
                StartedAt = DateTimeOffset.Now.AddDays(-1),
                FinishedAt = DateTimeOffset.Now.AddDays(-1).AddMinutes(10),
                DurationSec = 600,
                IngestedAt = DateTimeOffset.Now
            }
        };

        var commits = new List<RawCommit>();
        var mergeRequests = new List<RawMergeRequest>();

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateQualityMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserQualityMetrics)method!.Invoke(null, new object[] { pipelines, commits, mergeRequests })!;

        // Assert - Verify the fix: should be actual counts, not rate * 100
        Assert.Equal(3, result.TotalPipelines); // Total pipelines count
        Assert.Equal(2, result.SuccessfulPipelines); // Successful pipelines count 
        Assert.Equal(1, result.PipelineFailures); // Failed pipelines count
        Assert.Equal(2.0/3.0, result.PipelineSuccessRate, 2); // Success rate should be 2/3 ≈ 0.67
        Assert.Equal(6.0, result.AveragePipelineDurationMinutes, 1); // Average duration should be (5+3+10)/3 = 6 minutes
    }
    
    // TODO: Add test for CalculateCodeReviewMetricsAsync when we can properly mock the DbContext
}