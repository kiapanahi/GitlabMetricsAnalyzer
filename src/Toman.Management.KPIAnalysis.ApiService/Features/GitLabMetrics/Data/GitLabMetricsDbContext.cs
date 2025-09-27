using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

public sealed class GitLabMetricsDbContext(DbContextOptions<GitLabMetricsDbContext> options) : DbContext(options)
{
    // PRD Entities
    public DbSet<Developer> Developers => Set<Developer>();
    public DbSet<DeveloperAlias> DeveloperAliases => Set<DeveloperAlias>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CommitFact> CommitFacts => Set<CommitFact>();
    public DbSet<MergeRequestFact> MergeRequestFacts => Set<MergeRequestFact>();
    public DbSet<PipelineFact> PipelineFacts => Set<PipelineFact>();
    public DbSet<ReviewEvent> ReviewEvents => Set<ReviewEvent>();
    public DbSet<DeveloperMetricsAggregate> DeveloperMetricsAggregates => Set<DeveloperMetricsAggregate>();

    // Legacy Tables (to be phased out)
    public DbSet<DimUser> DimUsers => Set<DimUser>();
    public DbSet<DimBranch> DimBranches => Set<DimBranch>();

    // Raw Snapshots
    public DbSet<RawCommit> RawCommits => Set<RawCommit>();
    public DbSet<RawMergeRequest> RawMergeRequests => Set<RawMergeRequest>();
    public DbSet<RawMergeRequestNote> RawMergeRequestNotes => Set<RawMergeRequestNote>();
    public DbSet<RawPipeline> RawPipelines => Set<RawPipeline>();
    public DbSet<RawJob> RawJobs => Set<RawJob>();

    // Legacy Facts (to be phased out or refactored)
    public DbSet<FactMergeRequest> FactMergeRequests => Set<FactMergeRequest>();
    public DbSet<FactPipeline> FactPipelines => Set<FactPipeline>();
    public DbSet<FactUserMetrics> FactUserMetrics => Set<FactUserMetrics>();

    // Operational
    public DbSet<IngestionState> IngestionStates => Set<IngestionState>();
    public DbSet<CollectionRun> CollectionRuns => Set<CollectionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePrdEntities(modelBuilder);
        ConfigureDimensions(modelBuilder);
        ConfigureRawTables(modelBuilder);
        ConfigureFactTables(modelBuilder);
        ConfigureOperationalTables(modelBuilder);
    }

    private static void ConfigurePrdEntities(ModelBuilder modelBuilder)
    {
        // Developer entity
        modelBuilder.Entity<Developer>(entity =>
        {
            entity.ToTable("developers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.GitLabUserId).HasColumnName("gitlab_user_id");
            entity.Property(e => e.PrimaryEmail).HasColumnName("primary_email").HasMaxLength(255);
            entity.Property(e => e.PrimaryUsername).HasColumnName("primary_username").HasMaxLength(255);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(255);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.GitLabUserId).HasDatabaseName("idx_developers_gitlab_user_id").IsUnique();
            entity.HasIndex(e => e.PrimaryEmail).HasDatabaseName("idx_developers_primary_email").IsUnique();
        });

        // DeveloperAlias entity
        modelBuilder.Entity<DeveloperAlias>(entity =>
        {
            entity.ToTable("developer_aliases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.DeveloperId).HasColumnName("developer_id");
            entity.Property(e => e.AliasType).HasColumnName("alias_type").HasMaxLength(50);
            entity.Property(e => e.AliasValue).HasColumnName("alias_value").HasMaxLength(255);
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Developer).WithMany(d => d.Aliases).HasForeignKey(e => e.DeveloperId);
            entity.HasIndex(e => e.DeveloperId).HasDatabaseName("idx_developer_aliases_developer_id");
            entity.HasIndex(e => e.AliasValue).HasDatabaseName("idx_developer_aliases_value");
            entity.HasIndex(e => new { e.AliasValue, e.AliasType }).HasDatabaseName("idx_developer_aliases_value_type").IsUnique();
        });

        // Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.PathWithNamespace).HasColumnName("path_with_namespace").HasMaxLength(500);
            entity.Property(e => e.WebUrl).HasColumnName("web_url").HasMaxLength(1000);
            entity.Property(e => e.DefaultBranch).HasColumnName("default_branch").HasMaxLength(255);
            entity.Property(e => e.VisibilityLevel).HasColumnName("visibility_level").HasMaxLength(50);
            entity.Property(e => e.Archived).HasColumnName("archived");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IngestedAt).HasColumnName("ingested_at");

            entity.HasIndex(e => e.Name).HasDatabaseName("idx_projects_name");
            entity.HasIndex(e => e.Archived).HasDatabaseName("idx_projects_archived");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_projects_created_at");
            entity.HasIndex(e => e.PathWithNamespace).HasDatabaseName("idx_projects_path").IsUnique();
        });

        // CommitFact entity with partitioning
        modelBuilder.Entity<CommitFact>(entity =>
        {
            entity.ToTable("commit_facts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.DeveloperId).HasColumnName("developer_id");
            entity.Property(e => e.Sha).HasColumnName("sha").HasMaxLength(40);
            entity.Property(e => e.CommittedAt).HasColumnName("committed_at");
            entity.Property(e => e.LinesAdded).HasColumnName("lines_added");
            entity.Property(e => e.LinesDeleted).HasColumnName("lines_deleted");
            entity.Property(e => e.FilesChanged).HasColumnName("files_changed");
            entity.Property(e => e.IsSigned).HasColumnName("is_signed");
            entity.Property(e => e.IsMergeCommit).HasColumnName("is_merge_commit");
            entity.Property(e => e.ParentCount).HasColumnName("parent_count");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Project).WithMany(p => p.Commits).HasForeignKey(e => e.ProjectId);
            entity.HasOne(e => e.Developer).WithMany(d => d.Commits).HasForeignKey(e => e.DeveloperId);

            entity.HasIndex(e => e.DeveloperId).HasDatabaseName("idx_commit_facts_developer_id");
            entity.HasIndex(e => e.CommittedAt).HasDatabaseName("idx_commit_facts_committed_at");
            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_commit_facts_project_id");
            entity.HasIndex(e => new { e.ProjectId, e.Sha }).HasDatabaseName("idx_commit_facts_project_sha").IsUnique();

            // TODO: Add partitioning configuration once EF Core supports it better
        });

        // MergeRequestFact entity with partitioning
        modelBuilder.Entity<MergeRequestFact>(entity =>
        {
            entity.ToTable("merge_request_facts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.MrIid).HasColumnName("mr_iid");
            entity.Property(e => e.AuthorDeveloperId).HasColumnName("author_developer_id");
            entity.Property(e => e.TargetBranch).HasColumnName("target_branch").HasMaxLength(255);
            entity.Property(e => e.SourceBranch).HasColumnName("source_branch").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.MergedAt).HasColumnName("merged_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.FirstReviewAt).HasColumnName("first_review_at");
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.LinesAdded).HasColumnName("lines_added");
            entity.Property(e => e.LinesDeleted).HasColumnName("lines_deleted");
            entity.Property(e => e.CommitsCount).HasColumnName("commits_count");
            entity.Property(e => e.FilesChanged).HasColumnName("files_changed");
            entity.Property(e => e.CycleTimeHours).HasColumnName("cycle_time_hours").HasPrecision(10, 2);
            entity.Property(e => e.ReviewTimeHours).HasColumnName("review_time_hours").HasPrecision(10, 2);
            entity.Property(e => e.HasPipeline).HasColumnName("has_pipeline");
            entity.Property(e => e.IsDraft).HasColumnName("is_draft");
            entity.Property(e => e.IsWip).HasColumnName("is_wip");
            entity.Property(e => e.HasConflicts).HasColumnName("has_conflicts");
            entity.Property(e => e.CreatedAtFact).HasColumnName("created_at_fact");

            entity.HasOne(e => e.Project).WithMany(p => p.MergeRequests).HasForeignKey(e => e.ProjectId);
            entity.HasOne(e => e.AuthorDeveloper).WithMany(d => d.MergeRequests).HasForeignKey(e => e.AuthorDeveloperId);

            entity.HasIndex(e => e.AuthorDeveloperId).HasDatabaseName("idx_merge_request_facts_author");
            entity.HasIndex(e => e.State).HasDatabaseName("idx_merge_request_facts_state");
            entity.HasIndex(e => e.MergedAt).HasDatabaseName("idx_merge_request_facts_merged_at");
            entity.HasIndex(e => new { e.ProjectId, e.MrIid }).HasDatabaseName("idx_merge_request_facts_project_iid").IsUnique();
        });

        // PipelineFact entity with partitioning
        modelBuilder.Entity<PipelineFact>(entity =>
        {
            entity.ToTable("pipeline_facts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProjectId).HasColumnName("project_id");
            entity.Property(e => e.PipelineId).HasColumnName("pipeline_id");
            entity.Property(e => e.MergeRequestFactId).HasColumnName("merge_request_fact_id");
            entity.Property(e => e.DeveloperId).HasColumnName("developer_id");
            entity.Property(e => e.RefName).HasColumnName("ref_name").HasMaxLength(255);
            entity.Property(e => e.Sha).HasColumnName("sha").HasMaxLength(40);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
            entity.Property(e => e.CreatedAtFact).HasColumnName("created_at_fact");

            entity.HasOne(e => e.Project).WithMany(p => p.Pipelines).HasForeignKey(e => e.ProjectId);
            entity.HasOne(e => e.MergeRequestFact).WithMany(mr => mr.Pipelines).HasForeignKey(e => e.MergeRequestFactId);
            entity.HasOne(e => e.Developer).WithMany(d => d.Pipelines).HasForeignKey(e => e.DeveloperId);

            entity.HasIndex(e => e.DeveloperId).HasDatabaseName("idx_pipeline_facts_developer_id");
            entity.HasIndex(e => e.MergeRequestFactId).HasDatabaseName("idx_pipeline_facts_merge_request");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_pipeline_facts_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_pipeline_facts_created_at");
            entity.HasIndex(e => new { e.ProjectId, e.PipelineId }).HasDatabaseName("idx_pipeline_facts_project_pipeline").IsUnique();
        });

        // ReviewEvent entity with partitioning
        modelBuilder.Entity<ReviewEvent>(entity =>
        {
            entity.ToTable("review_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.MergeRequestFactId).HasColumnName("merge_request_fact_id");
            entity.Property(e => e.ReviewerDeveloperId).HasColumnName("reviewer_developer_id");
            entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(50);
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.MergeRequestFact).WithMany(mr => mr.ReviewEvents).HasForeignKey(e => e.MergeRequestFactId);
            entity.HasOne(e => e.ReviewerDeveloper).WithMany(d => d.ReviewsGiven).HasForeignKey(e => e.ReviewerDeveloperId);

            entity.HasIndex(e => e.MergeRequestFactId).HasDatabaseName("idx_review_events_merge_request");
            entity.HasIndex(e => e.ReviewerDeveloperId).HasDatabaseName("idx_review_events_reviewer");
            entity.HasIndex(e => e.EventType).HasDatabaseName("idx_review_events_type");
            entity.HasIndex(e => e.OccurredAt).HasDatabaseName("idx_review_events_occurred_at");
        });

        // DeveloperMetricsAggregate entity with partitioning
        modelBuilder.Entity<DeveloperMetricsAggregate>(entity =>
        {
            entity.ToTable("developer_metrics_aggregates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.DeveloperId).HasColumnName("developer_id");
            entity.Property(e => e.PeriodType).HasColumnName("period_type").HasMaxLength(20);
            entity.Property(e => e.PeriodStart).HasColumnName("period_start");
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end");
            entity.Property(e => e.CommitsCount).HasColumnName("commits_count");
            entity.Property(e => e.LinesAdded).HasColumnName("lines_added");
            entity.Property(e => e.LinesDeleted).HasColumnName("lines_deleted");
            entity.Property(e => e.FilesChanged).HasColumnName("files_changed");
            entity.Property(e => e.MrsCreated).HasColumnName("mrs_created");
            entity.Property(e => e.MrsMerged).HasColumnName("mrs_merged");
            entity.Property(e => e.MrsReviewed).HasColumnName("mrs_reviewed");
            entity.Property(e => e.AvgCycleTimeHours).HasColumnName("avg_cycle_time_hours").HasPrecision(10, 2);
            entity.Property(e => e.PipelinesTriggered).HasColumnName("pipelines_triggered");
            entity.Property(e => e.SuccessfulPipelines).HasColumnName("successful_pipelines");
            entity.Property(e => e.PipelineSuccessRate).HasColumnName("pipeline_success_rate").HasPrecision(5, 4);
            entity.Property(e => e.ReviewsGiven).HasColumnName("reviews_given");
            entity.Property(e => e.UniqueCollaborators).HasColumnName("unique_collaborators");
            entity.Property(e => e.CalculatedAt).HasColumnName("calculated_at");

            entity.HasOne(e => e.Developer).WithMany(d => d.MetricsAggregates).HasForeignKey(e => e.DeveloperId);

            entity.HasIndex(e => e.DeveloperId).HasDatabaseName("idx_dev_metrics_agg_developer");
            entity.HasIndex(e => new { e.PeriodType, e.PeriodStart }).HasDatabaseName("idx_dev_metrics_agg_period");
            entity.HasIndex(e => new { e.DeveloperId, e.PeriodType, e.PeriodStart }).HasDatabaseName("idx_dev_metrics_agg_developer_period").IsUnique();
        });
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

            // Collaboration Metrics
            entity.Property(e => e.TotalCommentsOnMergeRequests).HasColumnName("total_comments_on_merge_requests");
            entity.Property(e => e.TotalCommentsOnIssues).HasColumnName("total_comments_on_issues");
            entity.Property(e => e.CollaborationScore).HasColumnName("collaboration_score").HasPrecision(5, 2);

            // Issue Management Metrics (to be removed in PRD refactoring)
            entity.Property(e => e.TotalIssuesCreated).HasColumnName("total_issues_created");
            entity.Property(e => e.TotalIssuesAssigned).HasColumnName("total_issues_assigned");
            entity.Property(e => e.TotalIssuesClosed).HasColumnName("total_issues_closed");
            entity.Property(e => e.AverageIssueResolutionTimeHours).HasColumnName("average_issue_resolution_time_hours").HasPrecision(10, 2);

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
            entity.Property(e => e.WindowSizeHours).HasColumnName("window_size_hours");
            entity.Property(e => e.LastWindowEnd).HasColumnName("last_window_end");
            
            entity.HasIndex(e => e.Entity).HasDatabaseName("idx_ingestion_state_entity").IsUnique();
        });

        modelBuilder.Entity<CollectionRun>(entity =>
        {
            entity.ToTable("collection_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RunType).HasColumnName("run_type").HasMaxLength(50);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50);
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.WindowStart).HasColumnName("window_start");
            entity.Property(e => e.WindowEnd).HasColumnName("window_end");
            entity.Property(e => e.WindowSizeHours).HasColumnName("window_size_hours");
            entity.Property(e => e.ProjectsProcessed).HasColumnName("projects_processed");
            entity.Property(e => e.CommitsCollected).HasColumnName("commits_collected");
            entity.Property(e => e.MergeRequestsCollected).HasColumnName("merge_requests_collected");
            entity.Property(e => e.PipelinesCollected).HasColumnName("pipelines_collected");
            entity.Property(e => e.ReviewEventsCollected).HasColumnName("review_events_collected");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
            entity.Property(e => e.ErrorDetails).HasColumnName("error_details").HasColumnType("text");
            entity.Property(e => e.TriggerSource).HasColumnName("trigger_source").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            entity.HasIndex(e => e.RunType).HasDatabaseName("idx_collection_runs_run_type");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_collection_runs_status");
            entity.HasIndex(e => e.StartedAt).HasDatabaseName("idx_collection_runs_started_at");
            entity.HasIndex(e => new { e.RunType, e.StartedAt }).HasDatabaseName("idx_collection_runs_type_started");
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
