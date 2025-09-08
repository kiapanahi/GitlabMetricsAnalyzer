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
            entity.HasKey(e => e.id);

            // Basic project information
            entity.Property(e => e.id).HasColumnName("id");
            entity.Property(e => e.description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(e => e.name).HasColumnName("name").HasMaxLength(255);
            entity.Property(e => e.name_with_namespace).HasColumnName("name_with_namespace").HasMaxLength(500);
            entity.Property(e => e.path).HasColumnName("path").HasMaxLength(255);
            entity.Property(e => e.path_with_namespace).HasColumnName("path_with_namespace").HasMaxLength(500);
            entity.Property(e => e.created_at).HasColumnName("created_at");
            entity.Property(e => e.default_branch).HasColumnName("default_branch").HasMaxLength(255);
            entity.Property(e => e.tag_list).HasColumnName("tag_list");
            entity.Property(e => e.topics).HasColumnName("topics");
            entity.Property(e => e.ssh_url_to_repo).HasColumnName("ssh_url_to_repo").HasMaxLength(500);
            entity.Property(e => e.http_url_to_repo).HasColumnName("http_url_to_repo").HasMaxLength(500);
            entity.Property(e => e.web_url).HasColumnName("web_url").HasMaxLength(500);
            entity.Property(e => e.readme_url).HasColumnName("readme_url").HasMaxLength(500);
            entity.Property(e => e.forks_count).HasColumnName("forks_count");
            entity.Property(e => e.avatar_url).HasColumnName("avatar_url").HasMaxLength(500);
            entity.Property(e => e.star_count).HasColumnName("star_count");
            entity.Property(e => e.last_activity_at).HasColumnName("last_activity_at");
            entity.Property(e => e.visibility).HasColumnName("visibility").HasMaxLength(50);
            entity.Property(e => e.container_registry_image_prefix).HasColumnName("container_registry_image_prefix").HasMaxLength(500);

            // Deletion tracking
            //entity.Property(e => e.marked_for_deletion_at).HasColumnName("marked_for_deletion_at");
            //entity.Property(e => e.marked_for_deletion_on).HasColumnName("marked_for_deletion_on");

            // Feature flags
            entity.Property(e => e.packages_enabled).HasColumnName("packages_enabled");
            entity.Property(e => e.empty_repo).HasColumnName("empty_repo");
            entity.Property(e => e.archived).HasColumnName("archived");
            entity.Property(e => e.resolve_outdated_diff_discussions).HasColumnName("resolve_outdated_diff_discussions");

            // Repository settings
            entity.Property(e => e.repository_object_format).HasColumnName("repository_object_format").HasMaxLength(50);

            // Feature access levels
            entity.Property(e => e.issues_enabled).HasColumnName("issues_enabled");
            entity.Property(e => e.merge_requests_enabled).HasColumnName("merge_requests_enabled");
            entity.Property(e => e.wiki_enabled).HasColumnName("wiki_enabled");
            entity.Property(e => e.jobs_enabled).HasColumnName("jobs_enabled");
            entity.Property(e => e.snippets_enabled).HasColumnName("snippets_enabled");
            entity.Property(e => e.container_registry_enabled).HasColumnName("container_registry_enabled");
            entity.Property(e => e.service_desk_enabled).HasColumnName("service_desk_enabled");
            entity.Property(e => e.service_desk_address).HasColumnName("service_desk_address").HasMaxLength(255);
            entity.Property(e => e.can_create_merge_request_in).HasColumnName("can_create_merge_request_in");

            // Access levels
            entity.Property(e => e.issues_access_level).HasColumnName("issues_access_level").HasMaxLength(50);
            entity.Property(e => e.repository_access_level).HasColumnName("repository_access_level").HasMaxLength(50);
            entity.Property(e => e.merge_requests_access_level).HasColumnName("merge_requests_access_level").HasMaxLength(50);
            entity.Property(e => e.forking_access_level).HasColumnName("forking_access_level").HasMaxLength(50);
            entity.Property(e => e.wiki_access_level).HasColumnName("wiki_access_level").HasMaxLength(50);
            entity.Property(e => e.builds_access_level).HasColumnName("builds_access_level").HasMaxLength(50);
            entity.Property(e => e.snippets_access_level).HasColumnName("snippets_access_level").HasMaxLength(50);
            entity.Property(e => e.pages_access_level).HasColumnName("pages_access_level").HasMaxLength(50);
            entity.Property(e => e.analytics_access_level).HasColumnName("analytics_access_level").HasMaxLength(50);
            entity.Property(e => e.container_registry_access_level).HasColumnName("container_registry_access_level").HasMaxLength(50);
            entity.Property(e => e.security_and_compliance_access_level).HasColumnName("security_and_compliance_access_level").HasMaxLength(50);
            entity.Property(e => e.releases_access_level).HasColumnName("releases_access_level").HasMaxLength(50);
            entity.Property(e => e.environments_access_level).HasColumnName("environments_access_level").HasMaxLength(50);
            entity.Property(e => e.feature_flags_access_level).HasColumnName("feature_flags_access_level").HasMaxLength(50);
            entity.Property(e => e.infrastructure_access_level).HasColumnName("infrastructure_access_level").HasMaxLength(50);
            entity.Property(e => e.monitor_access_level).HasColumnName("monitor_access_level").HasMaxLength(50);
            entity.Property(e => e.model_experiments_access_level).HasColumnName("model_experiments_access_level").HasMaxLength(50);
            entity.Property(e => e.model_registry_access_level).HasColumnName("model_registry_access_level").HasMaxLength(50);

            // Email settings
            entity.Property(e => e.emails_disabled).HasColumnName("emails_disabled");
            entity.Property(e => e.emails_enabled).HasColumnName("emails_enabled");

            // Runner settings
            entity.Property(e => e.shared_runners_enabled).HasColumnName("shared_runners_enabled");
            entity.Property(e => e.lfs_enabled).HasColumnName("lfs_enabled");

            // Import settings
            entity.Property(e => e.creator_id).HasColumnName("creator_id");
            entity.Property(e => e.import_url).HasColumnName("import_url").HasMaxLength(500);
            //entity.Property(e => e.import_type).HasColumnName("import_type");
            entity.Property(e => e.import_status).HasColumnName("import_status").HasMaxLength(100);

            // Issue tracking
            entity.Property(e => e.open_issues_count).HasColumnName("open_issues_count");
            entity.Property(e => e.description_html).HasColumnName("description_html");
            entity.Property(e => e.updated_at).HasColumnName("updated_at");

            // CI/CD settings
            entity.Property(e => e.ci_default_git_depth).HasColumnName("ci_default_git_depth");
            entity.Property(e => e.ci_delete_pipelines_in_seconds).HasColumnName("ci_delete_pipelines_in_seconds");
            entity.Property(e => e.ci_forward_deployment_enabled).HasColumnName("ci_forward_deployment_enabled");
            entity.Property(e => e.ci_forward_deployment_rollback_allowed).HasColumnName("ci_forward_deployment_rollback_allowed");
            entity.Property(e => e.ci_job_token_scope_enabled).HasColumnName("ci_job_token_scope_enabled");
            entity.Property(e => e.ci_separated_caches).HasColumnName("ci_separated_caches");
            entity.Property(e => e.ci_allow_fork_pipelines_to_run_in_parent_project).HasColumnName("ci_allow_fork_pipelines_to_run_in_parent_project");
            entity.Property(e => e.ci_id_token_sub_claim_components).HasColumnName("ci_id_token_sub_claim_components");
            entity.Property(e => e.build_git_strategy).HasColumnName("build_git_strategy").HasMaxLength(50);
            entity.Property(e => e.keep_latest_artifact).HasColumnName("keep_latest_artifact");
            entity.Property(e => e.restrict_user_defined_variables).HasColumnName("restrict_user_defined_variables");
            entity.Property(e => e.ci_pipeline_variables_minimum_override_role).HasColumnName("ci_pipeline_variables_minimum_override_role").HasMaxLength(50);
            entity.Property(e => e.runner_token_expiration_interval).HasColumnName("runner_token_expiration_interval");
            entity.Property(e => e.group_runners_enabled).HasColumnName("group_runners_enabled");
            entity.Property(e => e.auto_cancel_pending_pipelines).HasColumnName("auto_cancel_pending_pipelines").HasMaxLength(50);
            entity.Property(e => e.build_timeout).HasColumnName("build_timeout");
            entity.Property(e => e.auto_devops_enabled).HasColumnName("auto_devops_enabled");
            entity.Property(e => e.auto_devops_deploy_strategy).HasColumnName("auto_devops_deploy_strategy").HasMaxLength(50);
            entity.Property(e => e.ci_push_repository_for_job_token_allowed).HasColumnName("ci_push_repository_for_job_token_allowed");
            entity.Property(e => e.runners_token).HasColumnName("runners_token").HasMaxLength(255);
            entity.Property(e => e.ci_config_path).HasColumnName("ci_config_path");
            entity.Property(e => e.public_jobs).HasColumnName("public_jobs");

            // Merge request settings
            entity.Property(e => e.only_allow_merge_if_pipeline_succeeds).HasColumnName("only_allow_merge_if_pipeline_succeeds");
            entity.Property(e => e.allow_merge_on_skipped_pipeline).HasColumnName("allow_merge_on_skipped_pipeline");
            entity.Property(e => e.request_access_enabled).HasColumnName("request_access_enabled");
            entity.Property(e => e.only_allow_merge_if_all_discussions_are_resolved).HasColumnName("only_allow_merge_if_all_discussions_are_resolved");
            entity.Property(e => e.remove_source_branch_after_merge).HasColumnName("remove_source_branch_after_merge");
            entity.Property(e => e.printing_merge_request_link_enabled).HasColumnName("printing_merge_request_link_enabled");
            entity.Property(e => e.merge_method).HasColumnName("merge_method").HasMaxLength(50);
            entity.Property(e => e.merge_request_title_regex).HasColumnName("merge_request_title_regex");
            entity.Property(e => e.merge_request_title_regex_description).HasColumnName("merge_request_title_regex_description");
            entity.Property(e => e.squash_option).HasColumnName("squash_option").HasMaxLength(50);
            entity.Property(e => e.enforce_auth_checks_on_uploads).HasColumnName("enforce_auth_checks_on_uploads");
            entity.Property(e => e.suggestion_commit_message).HasColumnName("suggestion_commit_message");
            entity.Property(e => e.merge_commit_template).HasColumnName("merge_commit_template");
            entity.Property(e => e.squash_commit_template).HasColumnName("squash_commit_template");
            entity.Property(e => e.issue_branch_template).HasColumnName("issue_branch_template");
            entity.Property(e => e.warn_about_potentially_unwanted_characters).HasColumnName("warn_about_potentially_unwanted_characters");
            entity.Property(e => e.autoclose_referenced_issues).HasColumnName("autoclose_referenced_issues");
            entity.Property(e => e.max_artifacts_size).HasColumnName("max_artifacts_size");
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
