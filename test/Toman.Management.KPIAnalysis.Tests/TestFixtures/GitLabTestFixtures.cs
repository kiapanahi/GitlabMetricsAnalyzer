using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.Tests.TestFixtures;

/// <summary>
/// Deterministic test fixtures for GitLab data covering comprehensive edge cases.
/// All data uses fixed seeds to ensure reproducible test outcomes.
/// </summary>
public static class GitLabTestFixtures
{
    /// <summary>
    /// Fixed date for deterministic test data generation
    /// </summary>
    public static readonly DateTime FixedBaseDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates test users with various patterns for comprehensive testing
    /// </summary>
    public static List<GitLabUser> CreateTestUsers()
    {
        return new List<GitLabUser>
        {
            new GitLabUser
            {
                Id = 1,
                Username = "alice.developer",
                Email = "alice@example.com",
                Name = "Alice Developer",
                State = "active",
                CreatedAt = FixedBaseDate.AddDays(-365),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 2,
                Username = "bob.reviewer",
                Email = "bob@example.com",
                Name = "Bob Reviewer",
                State = "active",
                CreatedAt = FixedBaseDate.AddDays(-300),
                IsAdmin = false,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 3,
                Username = "charlie.maintainer",
                Email = "charlie@example.com",
                Name = "Charlie Maintainer",
                State = "active",
                CreatedAt = FixedBaseDate.AddDays(-200),
                IsAdmin = true,
                CanCreateGroup = true,
                CanCreateProject = true,
                TwoFactorEnabled = true,
                External = false,
                PrivateProfile = false
            },
            new GitLabUser
            {
                Id = 4,
                Username = "deployment.bot",
                Email = "bot@example.com",
                Name = "Deployment Bot",
                State = "active",
                CreatedAt = FixedBaseDate.AddDays(-100),
                IsAdmin = false,
                CanCreateGroup = false,
                CanCreateProject = false,
                TwoFactorEnabled = false,
                External = true, // Bot user
                PrivateProfile = false
            }
        };
    }

    /// <summary>
    /// Creates test projects with different configurations
    /// </summary>
    public static List<GitLabProject> CreateTestProjects()
    {
        var users = CreateTestUsers();
        return new List<GitLabProject>
        {
            new GitLabProject
            {
                Id = 1,
                Name = "main-service",
                NameWithNamespace = "company/main-service",
                Path = "main-service",
                PathWithNamespace = "company/main-service",
                Description = "Main production service with comprehensive workflows",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/main-service",
                ForksCount = 5,
                StarCount = 15,
                CreatedAt = FixedBaseDate.AddDays(-180),
                LastActivityAt = FixedBaseDate.AddDays(-1),
                Owner = users[0] // Alice
            },
            new GitLabProject
            {
                Id = 2,
                Name = "legacy-system",
                NameWithNamespace = "company/legacy-system",
                Path = "legacy-system",
                PathWithNamespace = "company/legacy-system",
                Description = "Legacy system with irregular patterns",
                DefaultBranch = "master", // Old default branch
                Visibility = "private",
                Archived = false,
                WebUrl = "https://gitlab.example.com/company/legacy-system",
                ForksCount = 0,
                StarCount = 2,
                CreatedAt = FixedBaseDate.AddDays(-400),
                LastActivityAt = FixedBaseDate.AddDays(-7),
                Owner = users[2] // Charlie
            },
            new GitLabProject
            {
                Id = 3,
                Name = "archived-project",
                NameWithNamespace = "company/archived-project",
                Path = "archived-project",
                PathWithNamespace = "company/archived-project",
                Description = "Archived project for testing exclusion logic",
                DefaultBranch = "main",
                Visibility = "private",
                Archived = true, // Archived project
                WebUrl = "https://gitlab.example.com/company/archived-project",
                ForksCount = 0,
                StarCount = 0,
                CreatedAt = FixedBaseDate.AddDays(-500),
                LastActivityAt = FixedBaseDate.AddDays(-90),
                Owner = users[1] // Bob
            }
        };
    }

    /// <summary>
    /// Creates test commits covering various edge cases
    /// </summary>
    public static List<RawCommit> CreateTestCommits()
    {
        var users = CreateTestUsers();
        var commits = new List<RawCommit>();

        // Regular development commits
        commits.Add(new RawCommit
        {
            ProjectId = 1,
            ProjectName = "main-service",
            CommitId = "abc123def456",
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            AuthorEmail = "alice@example.com",
            CommittedAt = FixedBaseDate.AddDays(-10),
            Message = "feat: add user authentication endpoint",
            Additions = 150,
            Deletions = 25,
            IngestedAt = FixedBaseDate,
            ParentCount = 1
        });

        // Merge commit
        commits.Add(new RawCommit
        {
            ProjectId = 1,
            ProjectName = "main-service",
            CommitId = "merge456789",
            AuthorUserId = 2,
            AuthorName = "bob.reviewer",
            AuthorEmail = "bob@example.com",
            CommittedAt = FixedBaseDate.AddDays(-8),
            Message = "Merge branch 'feature/auth' into 'main'",
            Additions = 0,
            Deletions = 0,
            IngestedAt = FixedBaseDate,
            ParentCount = 2 // Merge commit
        });

        // Revert commit
        commits.Add(new RawCommit
        {
            ProjectId = 1,
            ProjectName = "main-service",
            CommitId = "revert789",
            AuthorUserId = 3,
            AuthorName = "charlie.maintainer",
            AuthorEmail = "charlie@example.com",
            CommittedAt = FixedBaseDate.AddDays(-6),
            Message = "Revert \"feat: add user authentication endpoint\"",
            Additions = 25,
            Deletions = 150,
            IngestedAt = FixedBaseDate,
            ParentCount = 1
        });

        // Bot commit (should be excluded)
        commits.Add(new RawCommit
        {
            ProjectId = 1,
            ProjectName = "main-service",
            CommitId = "bot123456",
            AuthorUserId = 4,
            AuthorName = "deployment.bot",
            AuthorEmail = "bot@example.com",
            CommittedAt = FixedBaseDate.AddDays(-5),
            Message = "chore: automated version bump to v1.2.3",
            Additions = 5,
            Deletions = 5,
            IngestedAt = FixedBaseDate,
            ParentCount = 1
        });

        // Large refactoring commit
        commits.Add(new RawCommit
        {
            ProjectId = 1,
            ProjectName = "main-service",
            CommitId = "refactor999",
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            AuthorEmail = "alice@example.com",
            CommittedAt = FixedBaseDate.AddDays(-15),
            Message = "refactor: restructure authentication module",
            Additions = 800,
            Deletions = 650,
            IngestedAt = FixedBaseDate,
            ParentCount = 1
        });

        return commits;
    }

    /// <summary>
    /// Creates test merge requests covering comprehensive edge cases
    /// </summary>
    public static List<RawMergeRequest> CreateTestMergeRequests()
    {
        var mergeRequests = new List<RawMergeRequest>();

        // Standard merged MR
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 101,
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            Title = "feat: add user authentication endpoint",
            CreatedAt = FixedBaseDate.AddDays(-12),
            MergedAt = FixedBaseDate.AddDays(-8),
            State = "merged",
            ChangesCount = 5,
            SourceBranch = "feature/auth",
            TargetBranch = "main",
            ApprovalsRequired = 2,
            ApprovalsGiven = 2,
            FirstReviewAt = FixedBaseDate.AddDays(-10),
            ReviewerIds = "2,3",
            IngestedAt = FixedBaseDate,
            Labels = "enhancement,backend",
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/101"
        });

        // Draft/WIP MR
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 102,
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            Title = "Draft: experimental feature implementation",
            CreatedAt = FixedBaseDate.AddDays(-5),
            State = "opened",
            ChangesCount = 8,
            SourceBranch = "feature/experimental",
            TargetBranch = "main",
            ApprovalsRequired = 2,
            ApprovalsGiven = 0,
            ReviewerIds = "2",
            IngestedAt = FixedBaseDate,
            Labels = "experimental,draft",
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/102"
        });

        // Hotfix MR
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 103,
            AuthorUserId = 3,
            AuthorName = "charlie.maintainer",
            Title = "hotfix: critical security vulnerability patch",
            CreatedAt = FixedBaseDate.AddDays(-3),
            MergedAt = FixedBaseDate.AddDays(-3).AddHours(2), // Quick merge
            State = "merged",
            ChangesCount = 2,
            SourceBranch = "hotfix/security-patch",
            TargetBranch = "main",
            ApprovalsRequired = 1,
            ApprovalsGiven = 1,
            FirstReviewAt = FixedBaseDate.AddDays(-3).AddHours(1),
            ReviewerIds = "2",
            IngestedAt = FixedBaseDate,
            Labels = "hotfix,security,critical",
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/103"
        });

        // Revert MR
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 104,
            AuthorUserId = 3,
            AuthorName = "charlie.maintainer",
            Title = "Revert \"feat: add user authentication endpoint\"",
            CreatedAt = FixedBaseDate.AddDays(-6),
            MergedAt = FixedBaseDate.AddDays(-6).AddHours(1),
            State = "merged",
            ChangesCount = 5,
            SourceBranch = "revert/auth-rollback",
            TargetBranch = "main",
            ApprovalsRequired = 1,
            ApprovalsGiven = 2,
            FirstReviewAt = FixedBaseDate.AddDays(-6).AddMinutes(30),
            ReviewerIds = "1,2",
            IngestedAt = FixedBaseDate,
            Labels = "revert,rollback",
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/104"
        });

        // Conflicted MR (closed without merge)
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 105,
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            Title = "feature: advanced user permissions",
            CreatedAt = FixedBaseDate.AddDays(-20),
            ClosedAt = FixedBaseDate.AddDays(-18),
            State = "closed",
            ChangesCount = 12,
            SourceBranch = "feature/permissions",
            TargetBranch = "main",
            ApprovalsRequired = 2,
            ApprovalsGiven = 0,
            ReviewerIds = "2,3",
            IngestedAt = FixedBaseDate,
            Labels = "feature,blocked",
            HasConflicts = true, // Conflicted MR
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/105"
        });

        // Squash merge MR (many commits, squashed to one)
        mergeRequests.Add(new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MrId = 106,
            AuthorUserId = 2,
            AuthorName = "bob.reviewer",
            Title = "refactor: clean up authentication module",
            CreatedAt = FixedBaseDate.AddDays(-25),
            MergedAt = FixedBaseDate.AddDays(-20),
            State = "merged",
            ChangesCount = 15,
            SourceBranch = "refactor/auth-cleanup",
            TargetBranch = "main",
            ApprovalsRequired = 2,
            ApprovalsGiven = 2,
            FirstReviewAt = FixedBaseDate.AddDays(-23),
            ReviewerIds = "1,3",
            IngestedAt = FixedBaseDate,
            Labels = "refactor,cleanup",
            CommitsCount = 25, // Many commits that got squashed
            WebUrl = "https://gitlab.example.com/company/main-service/-/merge_requests/106"
        });

        return mergeRequests;
    }

    /// <summary>
    /// Creates test pipelines covering flaky runs, failures, and success patterns
    /// </summary>
    public static List<RawPipeline> CreateTestPipelines()
    {
        var pipelines = new List<RawPipeline>();

        // Successful pipeline
        pipelines.Add(new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "main-service",
            PipelineId = 1001,
            Status = "success",
            Ref = "main",
            Sha = "abc123def456",
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            TriggerSource = "push",
            CreatedAt = FixedBaseDate.AddDays(-10),
            UpdatedAt = FixedBaseDate.AddDays(-10).AddMinutes(15),
            StartedAt = FixedBaseDate.AddDays(-10).AddMinutes(2),
            FinishedAt = FixedBaseDate.AddDays(-10).AddMinutes(15),
            DurationSec = 13 * 60, // 13 minutes
            Environment = "production",
            IngestedAt = FixedBaseDate
        });

        // Failed pipeline
        pipelines.Add(new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "main-service",
            PipelineId = 1002,
            Status = "failed",
            Ref = "feature/experimental",
            Sha = "fail456789",
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            TriggerSource = "push",
            CreatedAt = FixedBaseDate.AddDays(-5),
            UpdatedAt = FixedBaseDate.AddDays(-5).AddMinutes(8),
            StartedAt = FixedBaseDate.AddDays(-5).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-5).AddMinutes(8),
            DurationSec = 7 * 60, // 7 minutes
            Environment = "development",
            IngestedAt = FixedBaseDate
        });

        // Flaky pipeline (initially failed, then succeeded on retry)
        pipelines.Add(new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "main-service",
            PipelineId = 1003,
            Status = "failed",
            Ref = "feature/auth",
            Sha = "flaky123456",
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            TriggerSource = "push",
            CreatedAt = FixedBaseDate.AddDays(-12),
            UpdatedAt = FixedBaseDate.AddDays(-12).AddMinutes(10),
            StartedAt = FixedBaseDate.AddDays(-12).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-12).AddMinutes(10),
            DurationSec = 9 * 60, // 9 minutes
            Environment = "development",
            IngestedAt = FixedBaseDate
        });

        // Successful retry of flaky pipeline
        pipelines.Add(new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "main-service",
            PipelineId = 1004,
            Status = "success",
            Ref = "feature/auth",
            Sha = "flaky123456", // Same SHA as flaky pipeline
            AuthorUserId = 1,
            AuthorName = "alice.developer",
            TriggerSource = "web", // Manual retry
            CreatedAt = FixedBaseDate.AddDays(-12).AddMinutes(15), // Retry
            UpdatedAt = FixedBaseDate.AddDays(-12).AddMinutes(30),
            StartedAt = FixedBaseDate.AddDays(-12).AddMinutes(16),
            FinishedAt = FixedBaseDate.AddDays(-12).AddMinutes(30),
            DurationSec = 14 * 60, // 14 minutes
            Environment = "development",
            IngestedAt = FixedBaseDate
        });

        // Pipeline scheduled trigger
        pipelines.Add(new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "main-service",
            PipelineId = 1005,
            Status = "success",
            Ref = "main",
            Sha = "scheduled123",
            AuthorUserId = 4,
            AuthorName = "deployment.bot",
            TriggerSource = "schedule",
            CreatedAt = FixedBaseDate.AddDays(-7),
            UpdatedAt = FixedBaseDate.AddDays(-7).AddMinutes(20),
            StartedAt = FixedBaseDate.AddDays(-7).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-7).AddMinutes(20),
            DurationSec = 19 * 60, // 19 minutes
            Environment = "production",
            IngestedAt = FixedBaseDate
        });

        // Legacy system pipeline with minimal data
        pipelines.Add(new RawPipeline
        {
            ProjectId = 2,
            ProjectName = "legacy-system",
            PipelineId = 2001,
            Status = "success",
            Ref = "master",
            Sha = "legacy456789",
            AuthorUserId = 3,
            AuthorName = "charlie.maintainer",
            TriggerSource = "push",
            CreatedAt = FixedBaseDate.AddDays(-7),
            UpdatedAt = FixedBaseDate.AddDays(-7).AddMinutes(5),
            StartedAt = FixedBaseDate.AddDays(-7).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-7).AddMinutes(5),
            DurationSec = 4 * 60, // 4 minutes
            Environment = null, // No environment configured in legacy
            IngestedAt = FixedBaseDate
        });

        return pipelines;
    }

    /// <summary>
    /// Creates test jobs covering various pipeline job scenarios
    /// </summary>
    public static List<RawJob> CreateTestJobs()
    {
        var jobs = new List<RawJob>();

        // Successful test job
        jobs.Add(new RawJob
        {
            ProjectId = 1,
            JobId = 10001,
            PipelineId = 1001,
            Name = "unit-tests",
            Status = "success",
            DurationSec = 120, // 2 minutes
            StartedAt = FixedBaseDate.AddDays(-10).AddSeconds(30),
            FinishedAt = FixedBaseDate.AddDays(-10).AddMinutes(2).AddSeconds(30),
            RetriedFlag = false
        });

        // Failed test job (flaky)
        jobs.Add(new RawJob
        {
            ProjectId = 1,
            JobId = 10003,
            PipelineId = 1003,
            Name = "integration-tests",
            Status = "failed",
            DurationSec = 180, // 3 minutes
            StartedAt = FixedBaseDate.AddDays(-12).AddSeconds(45),
            FinishedAt = FixedBaseDate.AddDays(-12).AddMinutes(3).AddSeconds(45),
            RetriedFlag = false
        });

        // Successful retry of flaky job
        jobs.Add(new RawJob
        {
            ProjectId = 1,
            JobId = 10004,
            PipelineId = 1004,
            Name = "integration-tests",
            Status = "success",
            DurationSec = 175, // Slightly different duration
            StartedAt = FixedBaseDate.AddDays(-12).AddMinutes(15).AddSeconds(42),
            FinishedAt = FixedBaseDate.AddDays(-12).AddMinutes(18).AddSeconds(17),
            RetriedFlag = true // This is a retry
        });

        // Build job
        jobs.Add(new RawJob
        {
            ProjectId = 1,
            JobId = 10005,
            PipelineId = 1001,
            Name = "compile",
            Status = "success",
            DurationSec = 90, // 1.5 minutes
            StartedAt = FixedBaseDate.AddDays(-10).AddSeconds(15),
            FinishedAt = FixedBaseDate.AddDays(-10).AddMinutes(1).AddSeconds(45),
            RetriedFlag = false
        });

        // Deploy job (failed but different pipeline)
        jobs.Add(new RawJob
        {
            ProjectId = 1,
            JobId = 10006,
            PipelineId = 1002,
            Name = "deploy-staging",
            Status = "failed",
            DurationSec = 30,
            StartedAt = FixedBaseDate.AddDays(-5).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-5).AddMinutes(1).AddSeconds(30),
            RetriedFlag = false
        });

        // Legacy system job
        jobs.Add(new RawJob
        {
            ProjectId = 2,
            JobId = 20001,
            PipelineId = 2001,
            Name = "test",
            Status = "success",
            DurationSec = 60, // 1 minute
            StartedAt = FixedBaseDate.AddDays(-7).AddMinutes(1),
            FinishedAt = FixedBaseDate.AddDays(-7).AddMinutes(2),
            RetriedFlag = false
        });

        return jobs;
    }

    /// <summary>
    /// Creates test merge request notes covering various discussion patterns
    /// </summary>
    public static List<RawMergeRequestNote> CreateTestMergeRequestNotes()
    {
        var notes = new List<RawMergeRequestNote>();

        // Regular review comment
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 101,
            NoteId = 2001,
            Body = "This looks good overall, but could you add some unit tests for the new authentication logic?",
            AuthorId = 2,
            AuthorName = "bob.reviewer",
            CreatedAt = FixedBaseDate.AddDays(-10),
            UpdatedAt = FixedBaseDate.AddDays(-10),
            System = false,
            Resolvable = true,
            Resolved = false,
            ResolvedById = null,
            ResolvedBy = null,
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        // System note for approval
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 101,
            NoteId = 2002,
            Body = "approved this merge request",
            AuthorId = 2,
            AuthorName = "bob.reviewer",
            CreatedAt = FixedBaseDate.AddDays(-9),
            UpdatedAt = FixedBaseDate.AddDays(-9),
            System = true, // System note
            Resolvable = false,
            Resolved = false,
            ResolvedById = null,
            ResolvedBy = null,
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        // Response from author
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 101,
            NoteId = 2003,
            Body = "Good point! I've added comprehensive unit tests in commit abc789. Please take another look.",
            AuthorId = 1,
            AuthorName = "alice.developer",
            CreatedAt = FixedBaseDate.AddDays(-9).AddHours(2),
            UpdatedAt = FixedBaseDate.AddDays(-9).AddHours(2),
            System = false,
            Resolvable = false,
            Resolved = false,
            ResolvedById = null,
            ResolvedBy = null,
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        // Resolved discussion thread
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 101,
            NoteId = 2004,
            Body = "Perfect! The tests look comprehensive. Thanks for the quick fix.",
            AuthorId = 2,
            AuthorName = "bob.reviewer",
            CreatedAt = FixedBaseDate.AddDays(-9).AddHours(3),
            UpdatedAt = FixedBaseDate.AddDays(-9).AddHours(3),
            System = false,
            Resolvable = true,
            Resolved = true, // Resolved discussion
            ResolvedById = 2,
            ResolvedBy = "Bob Reviewer",
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        // Critical feedback on hotfix
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 103,
            NoteId = 2005,
            Body = "LGTM - this security fix is critical and well-tested. Approving for immediate merge.",
            AuthorId = 2,
            AuthorName = "bob.reviewer",
            CreatedAt = FixedBaseDate.AddDays(-3).AddHours(1),
            UpdatedAt = FixedBaseDate.AddDays(-3).AddHours(1),
            System = false,
            Resolvable = false,
            Resolved = false,
            ResolvedById = null,
            ResolvedBy = null,
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        // System note for merge
        notes.Add(new RawMergeRequestNote
        {
            ProjectId = 1,
            ProjectName = "main-service",
            MergeRequestIid = 103,
            NoteId = 2006,
            Body = "merged",
            AuthorId = 3,
            AuthorName = "charlie.maintainer",
            CreatedAt = FixedBaseDate.AddDays(-3).AddHours(2),
            UpdatedAt = FixedBaseDate.AddDays(-3).AddHours(2),
            System = true, // System merge note
            Resolvable = false,
            Resolved = false,
            ResolvedById = null,
            ResolvedBy = null,
            NoteableType = "MergeRequest",
            IngestedAt = FixedBaseDate
        });

        return notes;
    }

    /// <summary>
    /// Creates all test fixtures in a consistent, deterministic way
    /// Each property returns fresh instances to avoid Entity Framework tracking conflicts
    /// </summary>
    public static class CompleteFixture
    {
        public static List<GitLabUser> Users => CreateTestUsers();
        public static List<GitLabProject> Projects => CreateTestProjects();
        public static List<RawCommit> Commits => CreateTestCommits();
        public static List<RawMergeRequest> MergeRequests => CreateTestMergeRequests();
        public static List<RawPipeline> Pipelines => CreateTestPipelines();
        public static List<RawJob> Jobs => CreateTestJobs();
        public static List<RawMergeRequestNote> Notes => CreateTestMergeRequestNotes();
    }
}
