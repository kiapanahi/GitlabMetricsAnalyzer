using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

public sealed class GitLabMetricsDbContext(DbContextOptions<GitLabMetricsDbContext> options) : DbContext(options)
{

    // Dimensions
    public DbSet<DimUser> DimUsers => Set<DimUser>();
    public DbSet<DimBranch> DimBranches => Set<DimBranch>();
    public DbSet<DimRelease> DimReleases => Set<DimRelease>();

    // Raw Snapshots
    public DbSet<RawCommit> RawCommits => Set<RawCommit>();
    public DbSet<RawMergeRequest> RawMergeRequests => Set<RawMergeRequest>();
    public DbSet<RawMergeRequestNote> RawMergeRequestNotes => Set<RawMergeRequestNote>();
    public DbSet<RawIssueNote> RawIssueNotes => Set<RawIssueNote>();
    public DbSet<RawPipeline> RawPipelines => Set<RawPipeline>();
    public DbSet<RawJob> RawJobs => Set<RawJob>();
    public DbSet<RawIssue> RawIssues => Set<RawIssue>();

    // Derived Facts
    public DbSet<FactMergeRequest> FactMergeRequests => Set<FactMergeRequest>();
    public DbSet<FactPipeline> FactPipelines => Set<FactPipeline>();
    public DbSet<FactGitHygiene> FactGitHygiene => Set<FactGitHygiene>();
    public DbSet<FactRelease> FactReleases => Set<FactRelease>();
    public DbSet<FactUserMetrics> FactUserMetrics => Set<FactUserMetrics>();

    // Operational
    public DbSet<IngestionState> IngestionStates => Set<IngestionState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDimensions(modelBuilder);
        ConfigureRawTables(modelBuilder);
        ConfigureFactTables(modelBuilder);
        ConfigureOperationalTables(modelBuilder);
    }

    private static void ConfigureDimensions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DimUser>(entity =>
        {
            entity.ToTable("dim_user");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.IsBot).HasColumnName("is_bot");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(64);
        });

        modelBuilder.Entity<DimBranch>(entity =>
        {
            entity.ToTable("dim_branch");
            entity.HasKey(e => new { e.ProjectId, e.Branch });
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Branch).HasColumnName("branch").HasMaxLength(255);
            entity.Property(e => e.ProtectedFlag).HasColumnName("protected_flag");
        });

        modelBuilder.Entity<DimRelease>(entity =>
        {
            entity.ToTable("dim_release");
            entity.HasKey(e => new { e.ProjectId, e.TagName });
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.TagName).HasColumnName("tag_name").HasMaxLength(255);
            entity.Property(e => e.ReleasedAt).HasColumnName("released_at");
            entity.Property(e => e.SemverValid).HasColumnName("semver_valid");
        });
    }

    private static void ConfigureRawTables(ModelBuilder modelBuilder)
    {
        var jsonDocumentComparer = new ValueComparer<JsonDocument?>(
            (left, right) => JsonDocumentConverters.AreEqual(left, right),
            value => JsonDocumentConverters.GetHashCode(value),
            value => JsonDocumentConverters.Clone(value));

        modelBuilder.Entity<RawCommit>(entity =>
        {
            entity.ToTable("raw_commit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(255);
            entity.Property(e => e.CommitId).HasColumnName("commit_id").HasMaxLength(40);
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name").HasMaxLength(255);
            entity.Property(e => e.AuthorEmail).HasColumnName("author_email").HasMaxLength(255);
            entity.Property(e => e.CommittedAt).HasColumnName("committed_at");
            entity.Property(e => e.Message).HasColumnName("message").HasColumnType("text");
            entity.Property(e => e.Additions).HasColumnName("additions");
            entity.Property(e => e.Deletions).HasColumnName("deletions");
            entity.Property(e => e.IsSigned).HasColumnName("is_signed");
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");

            // Indexes for performance
            entity.HasIndex(e => e.CommittedAt).HasDatabaseName("idx_raw_commit_committed_at");
            entity.HasIndex(e => e.AuthorUserId).HasDatabaseName("idx_raw_commit_author");
            entity.HasIndex(e => e.IngestedAt).HasDatabaseName("idx_raw_commit_ingested_at");
            entity.HasIndex(e => new { e.ProjectId, e.CommitId }).HasDatabaseName("idx_raw_commit_project_commit").IsUnique();
        });

        modelBuilder.Entity<RawMergeRequest>(entity =>
        {
            entity.ToTable("raw_mr");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(255);
            entity.Property(e => e.MrId).HasColumnName("mr_id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name").HasMaxLength(255);
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.MergedAt).HasColumnName("merged_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.ChangesCount).HasColumnName("changes_count");
            entity.Property(e => e.SourceBranch).HasColumnName("source_branch").HasMaxLength(255);
            entity.Property(e => e.TargetBranch).HasColumnName("target_branch").HasMaxLength(255);
            entity.Property(e => e.ApprovalsRequired).HasColumnName("approvals_required");
            entity.Property(e => e.ApprovalsGiven).HasColumnName("approvals_given");
            entity.Property(e => e.FirstReviewAt).HasColumnName("first_review_at");
            entity.Property(e => e.ReviewerIds).HasColumnName("reviewer_ids").HasColumnType("text");
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");

            // Indexes for performance
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_raw_mr_created_at");
            entity.HasIndex(e => e.MergedAt).HasDatabaseName("idx_raw_mr_merged_at");
            entity.HasIndex(e => e.AuthorUserId).HasDatabaseName("idx_raw_mr_author");
            entity.HasIndex(e => e.State).HasDatabaseName("idx_raw_mr_state");
            entity.HasIndex(e => e.IngestedAt).HasDatabaseName("idx_raw_mr_ingested_at");
            entity.HasIndex(e => new { e.ProjectId, e.MrId }).HasDatabaseName("idx_raw_mr_project_mr").IsUnique();
        });

        modelBuilder.Entity<RawPipeline>(entity =>
        {
            entity.ToTable("raw_pipeline");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(255);
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.Sha).HasColumnName("sha").HasMaxLength(40);
            entity.Property(e => e.Ref).HasColumnName("ref").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name").HasMaxLength(255);
            entity.Property(e => e.TriggerSource).HasColumnName("trigger_source").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
            entity.Property(e => e.Environment).HasColumnName("environment").HasMaxLength(100);
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");

            // Indexes for performance
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_raw_pipeline_created_at");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_raw_pipeline_status");
            entity.HasIndex(e => e.AuthorUserId).HasDatabaseName("idx_raw_pipeline_author");
            entity.HasIndex(e => e.Ref).HasDatabaseName("idx_raw_pipeline_ref");
            entity.HasIndex(e => e.IngestedAt).HasDatabaseName("idx_raw_pipeline_ingested_at");
            entity.HasIndex(e => new { e.ProjectId, e.PipelineId }).HasDatabaseName("idx_raw_pipeline_project_pipeline").IsUnique();
        });

        modelBuilder.Entity<RawJob>(entity =>
        {
            entity.ToTable("raw_job");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.RetriedFlag).HasColumnName("retried_flag");

            // Unique constraint on project + job id
            entity.HasIndex(e => new { e.ProjectId, e.JobId }).HasDatabaseName("idx_raw_job_project_job").IsUnique();
        });

        modelBuilder.Entity<RawIssue>(entity =>
        {
            entity.ToTable("raw_issue");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.IssueId).HasColumnName("issue_id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.AssigneeUserId).HasColumnName("assignee_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.ReopenedCount).HasColumnName("reopened_count");
            entity.Property(e => e.Labels)
                .HasColumnName("labels")
                .HasColumnType("jsonb")
                .HasConversion(
                    value => JsonDocumentConverters.ToString(value),
                    value => JsonDocumentConverters.FromString(value))
                .Metadata.SetValueComparer(jsonDocumentComparer);

            // Unique constraint on project + issue id
            entity.HasIndex(e => new { e.ProjectId, e.IssueId }).HasDatabaseName("idx_raw_issue_project_issue").IsUnique();
        });

        modelBuilder.Entity<RawMergeRequestNote>(entity =>
        {
            entity.ToTable("raw_merge_request_note");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(255);
            entity.Property(e => e.MergeRequestIid).HasColumnName("merge_request_iid");
            entity.Property(e => e.NoteId).HasColumnName("note_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name").HasMaxLength(255);
            entity.Property(e => e.Body).HasColumnName("body").HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.System).HasColumnName("system");
            entity.Property(e => e.Resolvable).HasColumnName("resolvable");
            entity.Property(e => e.Resolved).HasColumnName("resolved");
            entity.Property(e => e.ResolvedById).HasColumnName("resolved_by_id");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(255);
            entity.Property(e => e.NoteableType).HasColumnName("noteable_type").HasMaxLength(50);
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");
            
            // Indexes
            entity.HasIndex(e => new { e.ProjectId, e.MergeRequestIid }).HasDatabaseName("idx_raw_mr_note_project_mr");
            entity.HasIndex(e => e.AuthorId).HasDatabaseName("idx_raw_mr_note_author");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_raw_mr_note_created_at");
            entity.HasIndex(e => new { e.ProjectId, e.NoteId }).HasDatabaseName("idx_raw_mr_note_project_note").IsUnique();
        });

        modelBuilder.Entity<RawIssueNote>(entity =>
        {
            entity.ToTable("raw_issue_note");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.ProjectName).HasColumnName("project_name").HasMaxLength(255);
            entity.Property(e => e.IssueIid).HasColumnName("issue_iid");
            entity.Property(e => e.NoteId).HasColumnName("note_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name").HasMaxLength(255);
            entity.Property(e => e.Body).HasColumnName("body").HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.System).HasColumnName("system");
            entity.Property(e => e.NoteableType).HasColumnName("noteable_type").HasMaxLength(50);
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");
            
            // Indexes
            entity.HasIndex(e => new { e.ProjectId, e.IssueIid }).HasDatabaseName("idx_raw_issue_note_project_issue");
            entity.HasIndex(e => e.AuthorId).HasDatabaseName("idx_raw_issue_note_author");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_raw_issue_note_created_at");
            entity.HasIndex(e => new { e.ProjectId, e.NoteId }).HasDatabaseName("idx_raw_issue_note_project_note").IsUnique();
        });
    }

    private static void ConfigureFactTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FactMergeRequest>(entity =>
        {
            entity.ToTable("fact_mr");
            entity.HasKey(e => e.MrId);
            entity.Property(e => e.MrId).HasColumnName("mr_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.CycleTimeHours).HasColumnName("cycle_time_hours").HasPrecision(10, 2);
            entity.Property(e => e.ReviewWaitHours).HasColumnName("review_wait_hours").HasPrecision(10, 2);
            entity.Property(e => e.ReworkCount).HasColumnName("rework_count");
            entity.Property(e => e.LinesAdded).HasColumnName("lines_added");
            entity.Property(e => e.LinesRemoved).HasColumnName("lines_removed");
        });

        modelBuilder.Entity<FactPipeline>(entity =>
        {
            entity.ToTable("fact_pipeline");
            entity.HasKey(e => e.PipelineId);
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.MtgSeconds).HasColumnName("mtg_seconds");
            entity.Property(e => e.IsProd).HasColumnName("is_prod");
            entity.Property(e => e.IsRollback).HasColumnName("is_rollback");
            entity.Property(e => e.IsFlakyCandidate).HasColumnName("is_flaky_candidate");
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
        });

        modelBuilder.Entity<FactGitHygiene>(entity =>
        {
            entity.ToTable("fact_git_hygiene");
            entity.HasKey(e => new { e.ProjectId, e.Day });
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.Day).HasColumnName("day");
            entity.Property(e => e.DirectPushesDefault).HasColumnName("direct_pushes_default");
            entity.Property(e => e.ForcePushesProtected).HasColumnName("force_pushes_protected");
            entity.Property(e => e.UnsignedCommitCount).HasColumnName("unsigned_commit_count");
        });

        modelBuilder.Entity<FactRelease>(entity =>
        {
            entity.ToTable("fact_release");
            entity.HasKey(e => new { e.TagName, e.ProjectId });
            entity.Property(e => e.TagName).HasColumnName("tag_name").HasMaxLength(255);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.IsSemver).HasColumnName("is_semver");
            entity.Property(e => e.CadenceBucket).HasColumnName("cadence_bucket").HasMaxLength(50);
        });

        modelBuilder.Entity<FactUserMetrics>(entity =>
        {
            entity.ToTable("fact_user_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.CollectedAt).HasColumnName("collected_at");
            entity.Property(e => e.FromDate).HasColumnName("from_date");
            entity.Property(e => e.ToDate).HasColumnName("to_date");
            entity.Property(e => e.PeriodDays).HasColumnName("period_days");
            
            // Code Contribution Metrics
            entity.Property(e => e.TotalCommits).HasColumnName("total_commits");
            entity.Property(e => e.TotalLinesAdded).HasColumnName("total_lines_added");
            entity.Property(e => e.TotalLinesDeleted).HasColumnName("total_lines_deleted");
            entity.Property(e => e.TotalLinesChanged).HasColumnName("total_lines_changed");
            entity.Property(e => e.AverageCommitsPerDay).HasColumnName("average_commits_per_day").HasPrecision(10, 2);
            entity.Property(e => e.AverageLinesChangedPerCommit).HasColumnName("average_lines_changed_per_commit").HasPrecision(10, 2);
            entity.Property(e => e.ActiveProjects).HasColumnName("active_projects");
            
            // Code Review Metrics
            entity.Property(e => e.TotalMergeRequestsCreated).HasColumnName("total_merge_requests_created");
            entity.Property(e => e.TotalMergeRequestsMerged).HasColumnName("total_merge_requests_merged");
            entity.Property(e => e.TotalMergeRequestsReviewed).HasColumnName("total_merge_requests_reviewed");
            entity.Property(e => e.AverageMergeRequestCycleTimeHours).HasColumnName("average_merge_request_cycle_time_hours").HasPrecision(10, 2);
            entity.Property(e => e.MergeRequestMergeRate).HasColumnName("merge_request_merge_rate").HasPrecision(5, 4);
            
            // Quality Metrics
            entity.Property(e => e.TotalPipelinesTriggered).HasColumnName("total_pipelines_triggered");
            entity.Property(e => e.SuccessfulPipelines).HasColumnName("successful_pipelines");
            entity.Property(e => e.FailedPipelines).HasColumnName("failed_pipelines");
            entity.Property(e => e.PipelineSuccessRate).HasColumnName("pipeline_success_rate").HasPrecision(5, 4);
            entity.Property(e => e.AveragePipelineDurationMinutes).HasColumnName("average_pipeline_duration_minutes").HasPrecision(10, 2);
            
            // Issue Management Metrics
            entity.Property(e => e.TotalIssuesCreated).HasColumnName("total_issues_created");
            entity.Property(e => e.TotalIssuesAssigned).HasColumnName("total_issues_assigned");
            entity.Property(e => e.TotalIssuesClosed).HasColumnName("total_issues_closed");
            entity.Property(e => e.AverageIssueResolutionTimeHours).HasColumnName("average_issue_resolution_time_hours").HasPrecision(10, 2);
            
            // Collaboration Metrics
            entity.Property(e => e.TotalCommentsOnMergeRequests).HasColumnName("total_comments_on_merge_requests");
            entity.Property(e => e.TotalCommentsOnIssues).HasColumnName("total_comments_on_issues");
            entity.Property(e => e.CollaborationScore).HasColumnName("collaboration_score").HasPrecision(5, 2);
            
            // Productivity Metrics
            entity.Property(e => e.ProductivityScore).HasColumnName("productivity_score").HasPrecision(5, 2);
            entity.Property(e => e.ProductivityLevel).HasColumnName("productivity_level").HasMaxLength(50);
            entity.Property(e => e.CodeChurnRate).HasColumnName("code_churn_rate").HasPrecision(5, 4);
            entity.Property(e => e.ReviewThroughput).HasColumnName("review_throughput").HasPrecision(10, 2);
            
            // Metadata
            entity.Property(e => e.TotalDataPoints).HasColumnName("total_data_points");
            entity.Property(e => e.DataQuality).HasColumnName("data_quality").HasMaxLength(50);
            
            // Indexes for performance
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_fact_user_metrics_user_id");
            entity.HasIndex(e => e.CollectedAt).HasDatabaseName("idx_fact_user_metrics_collected_at");
            entity.HasIndex(e => new { e.UserId, e.CollectedAt }).HasDatabaseName("idx_fact_user_metrics_user_collected");
            entity.HasIndex(e => new { e.UserId, e.FromDate, e.ToDate }).HasDatabaseName("idx_fact_user_metrics_user_period");
        });
    }

    private static void ConfigureOperationalTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngestionState>(entity =>
        {
            entity.ToTable("ingestion_state");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Entity).HasColumnName("entity").HasMaxLength(100);
            entity.Property(e => e.LastSeenUpdatedAt).HasColumnName("last_seen_updated_at");
            entity.Property(e => e.LastRunAt).HasColumnName("last_run_at");
        });
    }

    private static class JsonDocumentConverters
    {
        public static bool AreEqual(JsonDocument? left, JsonDocument? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.RootElement.GetRawText() == right.RootElement.GetRawText();
        }

        public static int GetHashCode(JsonDocument? value)
            => value is null
                ? 0
                : HashCode.Combine(value.RootElement.GetRawText());

        public static JsonDocument? Clone(JsonDocument? value)
            => value is null
                ? null
                : JsonDocument.Parse(value.RootElement.GetRawText());

        public static string? ToString(JsonDocument? value)
            => value is null ? null : value.RootElement.GetRawText();

        public static JsonDocument? FromString(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : JsonDocument.Parse(value);
    }
}
