using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

public sealed class GitLabMetricsDbContext : DbContext
{
    public GitLabMetricsDbContext(DbContextOptions<GitLabMetricsDbContext> options) : base(options)
    {
    }

    // Dimensions
    public DbSet<DimProject> DimProjects => Set<DimProject>();
    public DbSet<DimUser> DimUsers => Set<DimUser>();
    public DbSet<DimBranch> DimBranches => Set<DimBranch>();
    public DbSet<DimRelease> DimReleases => Set<DimRelease>();

    // Raw Snapshots
    public DbSet<RawCommit> RawCommits => Set<RawCommit>();
    public DbSet<RawMergeRequest> RawMergeRequests => Set<RawMergeRequest>();
    public DbSet<RawPipeline> RawPipelines => Set<RawPipeline>();
    public DbSet<RawJob> RawJobs => Set<RawJob>();
    public DbSet<RawIssue> RawIssues => Set<RawIssue>();

    // Derived Facts
    public DbSet<FactMergeRequest> FactMergeRequests => Set<FactMergeRequest>();
    public DbSet<FactPipeline> FactPipelines => Set<FactPipeline>();
    public DbSet<FactGitHygiene> FactGitHygiene => Set<FactGitHygiene>();
    public DbSet<FactRelease> FactReleases => Set<FactRelease>();

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
        modelBuilder.Entity<DimProject>(entity =>
        {
            entity.ToTable("dim_project");
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.PathWithNamespace).HasColumnName("path_with_namespace").HasMaxLength(500);
            entity.Property(e => e.DefaultBranch).HasColumnName("default_branch").HasMaxLength(255);
            entity.Property(e => e.Visibility).HasColumnName("visibility").HasMaxLength(50);
            entity.Property(e => e.ActiveFlag).HasColumnName("active_flag");
        });

        modelBuilder.Entity<DimUser>(entity =>
        {
            entity.ToTable("dim_user");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.IsBot).HasColumnName("is_bot");
            entity.Property(e => e.EmailHash).HasColumnName("email_hash").HasMaxLength(64);
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
        modelBuilder.Entity<RawCommit>(entity =>
        {
            entity.ToTable("raw_commit");
            entity.HasKey(e => e.CommitId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.CommitId).HasColumnName("commit_id").HasMaxLength(40);
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.CommittedAt).HasColumnName("committed_at");
            entity.Property(e => e.Additions).HasColumnName("additions");
            entity.Property(e => e.Deletions).HasColumnName("deletions");
            entity.Property(e => e.IsSigned).HasColumnName("is_signed");
        });

        modelBuilder.Entity<RawMergeRequest>(entity =>
        {
            entity.ToTable("raw_mr");
            entity.HasKey(e => e.MrId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.MrId).HasColumnName("mr_id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
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
        });

        modelBuilder.Entity<RawPipeline>(entity =>
        {
            entity.ToTable("raw_pipeline");
            entity.HasKey(e => e.PipelineId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.Sha).HasColumnName("sha").HasMaxLength(40);
            entity.Property(e => e.Ref).HasColumnName("ref").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
            entity.Property(e => e.Environment).HasColumnName("environment").HasMaxLength(100);
        });

        modelBuilder.Entity<RawJob>(entity =>
        {
            entity.ToTable("raw_job");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.RetriedFlag).HasColumnName("retried_flag");
        });

        modelBuilder.Entity<RawIssue>(entity =>
        {
            entity.ToTable("raw_issue");
            entity.HasKey(e => e.IssueId);
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.IssueId).HasColumnName("issue_id");
            entity.Property(e => e.AuthorUserId).HasColumnName("author_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.ReopenedCount).HasColumnName("reopened_count");
            entity.Property(e => e.Labels)
                .HasColumnName("labels")
                .HasColumnType("jsonb");
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
    }

    private static void ConfigureOperationalTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngestionState>(entity =>
        {
            entity.ToTable("ingestion_state");
            entity.HasKey(e => e.Entity);
            entity.Property(e => e.Entity).HasColumnName("entity").HasMaxLength(100);
            entity.Property(e => e.LastSeenUpdatedAt).HasColumnName("last_seen_updated_at");
            entity.Property(e => e.LastRunAt).HasColumnName("last_run_at");
        });
    }
}
