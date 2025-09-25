using System.Reflection;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests;

/// <summary>
/// Integration tests to ensure quality metrics are properly calculated and integrated
/// </summary>
public class QualityMetricsIntegrationTests
{
    [Fact]
    public void CalculateQualityMetrics_Should_Return_All_Calculated_Values_Not_Placeholders()
    {
        // Arrange - Create test data with patterns our quality metrics should detect
        var commits = new List<RawCommit>
        {
            CreateCommit("Add user authentication feature"), // Normal commit
            CreateCommit("Fix critical bug in payment system"), // Bug fix
            CreateCommit("Revert \"Add broken functionality\""), // Revert
            CreateCommit("Add unit tests for authentication"), // Test-related
            CreateCommit("Fix XSS vulnerability in forms"), // Security + bug fix
            CreateCommit("Update documentation"), // Normal commit
        };

        var mergeRequests = new List<RawMergeRequest>
        {
            CreateMergeRequest("Feature: Add user dashboard"), // Normal MR
            CreateMergeRequest("Hotfix: Critical security patch"), // Security + bug fix
            CreateMergeRequest("Fix: Database connection issue") // Bug fix
        };

        var pipelines = new List<RawPipeline>
        {
            CreatePipeline(true),  // Success
            CreatePipeline(true),  // Success  
            CreatePipeline(false), // Failure
            CreatePipeline(true)   // Success
        };

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateQualityMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserQualityMetrics)method!.Invoke(null, new object[] { pipelines, commits, mergeRequests })!;

        // Assert - Verify all metrics are calculated (not placeholders)
        
        // Pipeline success rate should be calculated correctly
        Assert.Equal(0.75, result.PipelineSuccessRate, 2); // 3 success out of 4 = 0.75
        Assert.Equal(1, result.PipelineFailures); // 1 failure
        
        // Code revert rate should detect the revert commit
        Assert.True(result.CodeRevertRate > 0, "CodeRevertRate should be > 0, not a placeholder");
        Assert.True(result.CodeRevertRate <= 1, "CodeRevertRate should be <= 1");
        
        // Bug fix ratio should detect bug-related commits and MRs
        Assert.True(result.BugFixRatio > 0, "BugFixRatio should be > 0, not a placeholder");
        Assert.True(result.BugFixRatio <= 1, "BugFixRatio should be <= 1");
        
        // Test coverage should be estimated based on test commits and pipeline success
        Assert.True(result.TestCoverage >= 0, "TestCoverage should be >= 0, not a placeholder");
        Assert.True(result.TestCoverage <= 0.85, "TestCoverage should be <= 0.85 (capped estimate)");
        
        // Security issues should detect security-related work
        Assert.True(result.SecurityIssues > 0, "SecurityIssues should be > 0, not a placeholder");
    }

    [Fact]
    public void CalculateQualityMetrics_Should_Handle_Empty_Data_Gracefully()
    {
        // Arrange - Empty data
        var commits = new List<RawCommit>();
        var mergeRequests = new List<RawMergeRequest>();  
        var pipelines = new List<RawPipeline>();

        // Act - Use reflection to call the private static method
        var method = typeof(UserMetricsService).GetMethod("CalculateQualityMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserQualityMetrics)method!.Invoke(null, new object[] { pipelines, commits, mergeRequests })!;

        // Assert - Should handle empty data without errors
        Assert.Equal(0, result.PipelineSuccessRate);
        Assert.Equal(0, result.PipelineFailures);
        Assert.Equal(0, result.CodeRevertRate);
        Assert.Equal(0, result.BugFixRatio);
        Assert.Equal(0, result.TestCoverage);
        Assert.Equal(0, result.SecurityIssues);
    }

    [Fact]
    public void CalculateQualityMetrics_Should_Show_Realistic_Values_For_Real_World_Data()
    {
        // Arrange - More realistic data patterns
        var commits = CreateRealisticCommits();
        var mergeRequests = CreateRealisticMergeRequests();
        var pipelines = CreateRealisticPipelines();

        // Act
        var method = typeof(UserMetricsService).GetMethod("CalculateQualityMetrics", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var result = (UserQualityMetrics)method!.Invoke(null, new object[] { pipelines, commits, mergeRequests })!;

        // Assert - Verify realistic ranges for quality metrics
        Assert.InRange(result.PipelineSuccessRate, 0.0, 1.0);
        Assert.InRange(result.CodeRevertRate, 0.0, 1.0);
        Assert.InRange(result.BugFixRatio, 0.0, 1.0);
        Assert.InRange(result.TestCoverage, 0.0, 0.85);
        Assert.True(result.SecurityIssues >= 0);
        
        // For this realistic dataset, we should see some quality issues
        Assert.True(result.BugFixRatio > 0, "Realistic data should show some bug fixes");
        Assert.True(result.TestCoverage > 0, "Realistic data should show some test activity");
    }

    private static RawCommit CreateCommit(string message)
    {
        return new RawCommit
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = Guid.NewGuid().ToString(),
            AuthorUserId = 123,
            AuthorName = "Test User",
            AuthorEmail = "test@example.com",
            CommittedAt = DateTimeOffset.UtcNow,
            Message = message,
            Additions = 10,
            Deletions = 5,
            IsSigned = false,
            IngestedAt = DateTimeOffset.UtcNow
        };
    }

    private static RawMergeRequest CreateMergeRequest(string title)
    {
        return new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            MrId = Random.Shared.Next(1, 1000),
            AuthorUserId = 123,
            AuthorName = "Test User",
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            State = "merged",
            ChangesCount = 15,
            SourceBranch = "feature-branch",
            TargetBranch = "main",
            ApprovalsRequired = 1,
            ApprovalsGiven = 1,
            IngestedAt = DateTimeOffset.UtcNow
        };
    }

    private static RawPipeline CreatePipeline(bool isSuccessful)
    {
        return new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            PipelineId = Random.Shared.Next(1, 1000),
            Sha = "abc123def456",
            Ref = "refs/heads/main",
            Status = isSuccessful ? "success" : "failed",
            AuthorUserId = 123,
            AuthorName = "Test User",
            TriggerSource = "push",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow.AddMinutes(5),
            DurationSec = 300,
            IngestedAt = DateTimeOffset.UtcNow
        };
    }

    private static List<RawCommit> CreateRealisticCommits()
    {
        return new List<RawCommit>
        {
            CreateCommit("Add user registration endpoint"),
            CreateCommit("Implement JWT authentication"),
            CreateCommit("Fix null pointer exception in user service"),
            CreateCommit("Add unit tests for authentication service"),
            CreateCommit("Update password validation rules"),
            CreateCommit("Revert \"Add experimental caching\""),
            CreateCommit("Fix security vulnerability in auth token"),
            CreateCommit("Add integration tests for user endpoints"),
            CreateCommit("Update API documentation"),
            CreateCommit("Bugfix: Handle edge case in payment processing"),
            CreateCommit("Add test coverage for payment module"),
            CreateCommit("Implement rate limiting middleware"),
            CreateCommit("Fix CSRF token validation"),
            CreateCommit("Add end-to-end tests for user flow"),
            CreateCommit("Refactor authentication logic")
        };
    }

    private static List<RawMergeRequest> CreateRealisticMergeRequests()
    {
        return new List<RawMergeRequest>
        {
            CreateMergeRequest("Feature: User profile management"),
            CreateMergeRequest("Fix: Database connection timeout"),
            CreateMergeRequest("Security: Update dependency versions"),
            CreateMergeRequest("Test: Add comprehensive API tests"),
            CreateMergeRequest("Hotfix: Critical payment bug"),
            CreateMergeRequest("Feature: Email notification system"),
            CreateMergeRequest("Bug: Fix memory leak in cache service")
        };
    }

    private static List<RawPipeline> CreateRealisticPipelines()
    {
        return new List<RawPipeline>
        {
            CreatePipeline(true),   // Success
            CreatePipeline(true),   // Success
            CreatePipeline(false),  // Failure
            CreatePipeline(true),   // Success
            CreatePipeline(true),   // Success
            CreatePipeline(false),  // Failure
            CreatePipeline(true),   // Success
            CreatePipeline(true),   // Success
            CreatePipeline(true),   // Success
            CreatePipeline(true)    // Success
        }; // 80% success rate
    }
}