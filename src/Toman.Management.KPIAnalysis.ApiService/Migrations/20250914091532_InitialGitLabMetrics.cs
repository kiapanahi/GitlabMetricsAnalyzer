using System.Text.Json;

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class InitialGitLabMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dim_branch",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    protected_flag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dim_branch", x => new { x.project_id, x.branch });
                });

            migrationBuilder.CreateTable(
                name: "dim_release",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    tag_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    semver_valid = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dim_release", x => new { x.project_id, x.tag_name });
                });

            migrationBuilder.CreateTable(
                name: "dim_user",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_bot = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dim_user", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "fact_git_hygiene",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    direct_pushes_default = table.Column<int>(type: "integer", nullable: false),
                    force_pushes_protected = table.Column<int>(type: "integer", nullable: false),
                    unsigned_commit_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_git_hygiene", x => new { x.project_id, x.day });
                });

            migrationBuilder.CreateTable(
                name: "fact_mr",
                columns: table => new
                {
                    mr_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    cycle_time_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    review_wait_hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    rework_count = table.Column<int>(type: "integer", nullable: false),
                    lines_added = table.Column<int>(type: "integer", nullable: false),
                    lines_removed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_mr", x => x.mr_id);
                });

            migrationBuilder.CreateTable(
                name: "fact_pipeline",
                columns: table => new
                {
                    pipeline_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    mtg_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_prod = table.Column<bool>(type: "boolean", nullable: false),
                    is_rollback = table.Column<bool>(type: "boolean", nullable: false),
                    is_flaky_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    duration_sec = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_pipeline", x => x.pipeline_id);
                });

            migrationBuilder.CreateTable(
                name: "fact_release",
                columns: table => new
                {
                    tag_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    is_semver = table.Column<bool>(type: "boolean", nullable: false),
                    cadence_bucket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_release", x => new { x.tag_name, x.project_id });
                });

            migrationBuilder.CreateTable(
                name: "ingestion_state",
                columns: table => new
                {
                    entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_seen_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_state", x => x.entity);
                });

            migrationBuilder.CreateTable(
                name: "raw_commit",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    commit_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    project_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    author_user_id = table.Column<int>(type: "integer", nullable: false),
                    author_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    author_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    additions = table.Column<int>(type: "integer", nullable: false),
                    deletions = table.Column<int>(type: "integer", nullable: false),
                    is_signed = table.Column<bool>(type: "boolean", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_commit", x => new { x.project_id, x.commit_id });
                });

            migrationBuilder.CreateTable(
                name: "raw_issue",
                columns: table => new
                {
                    issue_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    author_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reopened_count = table.Column<int>(type: "integer", nullable: false),
                    labels = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_issue", x => x.issue_id);
                });

            migrationBuilder.CreateTable(
                name: "raw_job",
                columns: table => new
                {
                    job_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    pipeline_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_sec = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retried_flag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_job", x => x.job_id);
                });

            migrationBuilder.CreateTable(
                name: "raw_mr",
                columns: table => new
                {
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    mr_id = table.Column<long>(type: "bigint", nullable: false),
                    project_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    author_user_id = table.Column<long>(type: "bigint", nullable: false),
                    author_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    merged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changes_count = table.Column<int>(type: "integer", nullable: false),
                    source_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    target_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    approvals_required = table.Column<int>(type: "integer", nullable: false),
                    approvals_given = table.Column<int>(type: "integer", nullable: false),
                    first_review_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewer_ids = table.Column<string>(type: "text", nullable: true),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_mr", x => new { x.project_id, x.mr_id });
                });

            migrationBuilder.CreateTable(
                name: "raw_pipeline",
                columns: table => new
                {
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    pipeline_id = table.Column<int>(type: "integer", nullable: false),
                    project_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    @ref = table.Column<string>(name: "ref", type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    author_user_id = table.Column<int>(type: "integer", nullable: false),
                    author_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    trigger_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_sec = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_pipeline", x => new { x.project_id, x.pipeline_id });
                });

            migrationBuilder.CreateIndex(
                name: "idx_raw_commit_author",
                table: "raw_commit",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_raw_commit_committed_at",
                table: "raw_commit",
                column: "committed_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_commit_ingested_at",
                table: "raw_commit",
                column: "ingested_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_author",
                table: "raw_mr",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_created_at",
                table: "raw_mr",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_ingested_at",
                table: "raw_mr",
                column: "ingested_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_merged_at",
                table: "raw_mr",
                column: "merged_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_state",
                table: "raw_mr",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_author",
                table: "raw_pipeline",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_created_at",
                table: "raw_pipeline",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_ingested_at",
                table: "raw_pipeline",
                column: "ingested_at");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_ref",
                table: "raw_pipeline",
                column: "ref");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_status",
                table: "raw_pipeline",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dim_branch");

            migrationBuilder.DropTable(
                name: "dim_release");

            migrationBuilder.DropTable(
                name: "dim_user");

            migrationBuilder.DropTable(
                name: "fact_git_hygiene");

            migrationBuilder.DropTable(
                name: "fact_mr");

            migrationBuilder.DropTable(
                name: "fact_pipeline");

            migrationBuilder.DropTable(
                name: "fact_release");

            migrationBuilder.DropTable(
                name: "ingestion_state");

            migrationBuilder.DropTable(
                name: "raw_commit");

            migrationBuilder.DropTable(
                name: "raw_issue");

            migrationBuilder.DropTable(
                name: "raw_job");

            migrationBuilder.DropTable(
                name: "raw_mr");

            migrationBuilder.DropTable(
                name: "raw_pipeline");
        }
    }
}
