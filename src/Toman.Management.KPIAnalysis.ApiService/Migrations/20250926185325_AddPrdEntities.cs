using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddPrdEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raw_issue");

            migrationBuilder.CreateTable(
                name: "developers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    gitlab_user_id = table.Column<long>(type: "bigint", nullable: false),
                    primary_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    primary_username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    path_with_namespace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    web_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    visibility_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "raw_merge_request_note",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    project_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    merge_request_iid = table.Column<long>(type: "bigint", nullable: false),
                    note_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    author_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    system = table.Column<bool>(type: "boolean", nullable: false),
                    resolvable = table.Column<bool>(type: "boolean", nullable: false),
                    resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_by_id = table.Column<long>(type: "bigint", nullable: true),
                    resolved_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    noteable_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_merge_request_note", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "developer_aliases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    developer_id = table.Column<long>(type: "bigint", nullable: false),
                    alias_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    alias_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developer_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_developer_aliases_developers_developer_id",
                        column: x => x.developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "developer_metrics_aggregates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    developer_id = table.Column<long>(type: "bigint", nullable: false),
                    period_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    commits_count = table.Column<int>(type: "integer", nullable: false),
                    lines_added = table.Column<int>(type: "integer", nullable: false),
                    lines_deleted = table.Column<int>(type: "integer", nullable: false),
                    files_changed = table.Column<int>(type: "integer", nullable: false),
                    mrs_created = table.Column<int>(type: "integer", nullable: false),
                    mrs_merged = table.Column<int>(type: "integer", nullable: false),
                    mrs_reviewed = table.Column<int>(type: "integer", nullable: false),
                    avg_cycle_time_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    pipelines_triggered = table.Column<int>(type: "integer", nullable: false),
                    successful_pipelines = table.Column<int>(type: "integer", nullable: false),
                    pipeline_success_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    reviews_given = table.Column<int>(type: "integer", nullable: false),
                    unique_collaborators = table.Column<int>(type: "integer", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developer_metrics_aggregates", x => x.id);
                    table.ForeignKey(
                        name: "FK_developer_metrics_aggregates_developers_developer_id",
                        column: x => x.developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commit_facts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    developer_id = table.Column<long>(type: "bigint", nullable: false),
                    sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lines_added = table.Column<int>(type: "integer", nullable: false),
                    lines_deleted = table.Column<int>(type: "integer", nullable: false),
                    files_changed = table.Column<int>(type: "integer", nullable: false),
                    is_signed = table.Column<bool>(type: "boolean", nullable: false),
                    is_merge_commit = table.Column<bool>(type: "boolean", nullable: false),
                    parent_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commit_facts", x => x.id);
                    table.ForeignKey(
                        name: "FK_commit_facts_developers_developer_id",
                        column: x => x.developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_commit_facts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merge_request_facts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    mr_iid = table.Column<int>(type: "integer", nullable: false),
                    author_developer_id = table.Column<long>(type: "bigint", nullable: false),
                    target_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    merged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_review_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lines_added = table.Column<int>(type: "integer", nullable: false),
                    lines_deleted = table.Column<int>(type: "integer", nullable: false),
                    commits_count = table.Column<int>(type: "integer", nullable: false),
                    files_changed = table.Column<int>(type: "integer", nullable: false),
                    cycle_time_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    review_time_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    has_pipeline = table.Column<bool>(type: "boolean", nullable: false),
                    is_draft = table.Column<bool>(type: "boolean", nullable: false),
                    is_wip = table.Column<bool>(type: "boolean", nullable: false),
                    has_conflicts = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_fact = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merge_request_facts", x => x.id);
                    table.ForeignKey(
                        name: "FK_merge_request_facts_developers_author_developer_id",
                        column: x => x.author_developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_merge_request_facts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_facts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_id = table.Column<long>(type: "bigint", nullable: false),
                    merge_request_fact_id = table.Column<long>(type: "bigint", nullable: true),
                    developer_id = table.Column<long>(type: "bigint", nullable: false),
                    ref_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at_fact = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_facts", x => x.id);
                    table.ForeignKey(
                        name: "FK_pipeline_facts_developers_developer_id",
                        column: x => x.developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pipeline_facts_merge_request_facts_merge_request_fact_id",
                        column: x => x.merge_request_fact_id,
                        principalTable: "merge_request_facts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_pipeline_facts_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    merge_request_fact_id = table.Column<long>(type: "bigint", nullable: false),
                    reviewer_developer_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_review_events_developers_reviewer_developer_id",
                        column: x => x.reviewer_developer_id,
                        principalTable: "developers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_events_merge_request_facts_merge_request_fact_id",
                        column: x => x.merge_request_fact_id,
                        principalTable: "merge_request_facts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_commit_facts_committed_at",
                table: "commit_facts",
                column: "committed_at");

            migrationBuilder.CreateIndex(
                name: "idx_commit_facts_developer_id",
                table: "commit_facts",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_commit_facts_project_id",
                table: "commit_facts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_commit_facts_project_sha",
                table: "commit_facts",
                columns: new[] { "project_id", "sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_developer_aliases_developer_id",
                table: "developer_aliases",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_developer_aliases_value",
                table: "developer_aliases",
                column: "alias_value");

            migrationBuilder.CreateIndex(
                name: "idx_developer_aliases_value_type",
                table: "developer_aliases",
                columns: new[] { "alias_value", "alias_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_developer",
                table: "developer_metrics_aggregates",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_developer_period",
                table: "developer_metrics_aggregates",
                columns: new[] { "developer_id", "period_type", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_period",
                table: "developer_metrics_aggregates",
                columns: new[] { "period_type", "period_start" });

            migrationBuilder.CreateIndex(
                name: "idx_developers_gitlab_user_id",
                table: "developers",
                column: "gitlab_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_developers_primary_email",
                table: "developers",
                column: "primary_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_merge_request_facts_author",
                table: "merge_request_facts",
                column: "author_developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_merge_request_facts_merged_at",
                table: "merge_request_facts",
                column: "merged_at");

            migrationBuilder.CreateIndex(
                name: "idx_merge_request_facts_project_iid",
                table: "merge_request_facts",
                columns: new[] { "project_id", "mr_iid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_merge_request_facts_state",
                table: "merge_request_facts",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_facts_created_at",
                table: "pipeline_facts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_facts_developer_id",
                table: "pipeline_facts",
                column: "developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_facts_merge_request",
                table: "pipeline_facts",
                column: "merge_request_fact_id");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_facts_project_pipeline",
                table: "pipeline_facts",
                columns: new[] { "project_id", "pipeline_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_facts_status",
                table: "pipeline_facts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_projects_archived",
                table: "projects",
                column: "archived");

            migrationBuilder.CreateIndex(
                name: "idx_projects_created_at",
                table: "projects",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_projects_name",
                table: "projects",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_projects_path",
                table: "projects",
                column: "path_with_namespace",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_note_author",
                table: "raw_merge_request_note",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_note_created_at",
                table: "raw_merge_request_note",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_note_project_mr",
                table: "raw_merge_request_note",
                columns: new[] { "project_id", "merge_request_iid" });

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_note_project_note",
                table: "raw_merge_request_note",
                columns: new[] { "project_id", "note_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_review_events_merge_request",
                table: "review_events",
                column: "merge_request_fact_id");

            migrationBuilder.CreateIndex(
                name: "idx_review_events_occurred_at",
                table: "review_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "idx_review_events_reviewer",
                table: "review_events",
                column: "reviewer_developer_id");

            migrationBuilder.CreateIndex(
                name: "idx_review_events_type",
                table: "review_events",
                column: "event_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commit_facts");

            migrationBuilder.DropTable(
                name: "developer_aliases");

            migrationBuilder.DropTable(
                name: "developer_metrics_aggregates");

            migrationBuilder.DropTable(
                name: "pipeline_facts");

            migrationBuilder.DropTable(
                name: "raw_merge_request_note");

            migrationBuilder.DropTable(
                name: "review_events");

            migrationBuilder.DropTable(
                name: "merge_request_facts");

            migrationBuilder.DropTable(
                name: "developers");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "raw_issue",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assignee_user_id = table.Column<long>(type: "bigint", nullable: true),
                    author_user_id = table.Column<long>(type: "bigint", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    labels = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    reopened_count = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_issue", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_raw_issue_project_issue",
                table: "raw_issue",
                columns: new[] { "project_id", "issue_id" },
                unique: true);
        }
    }
}
