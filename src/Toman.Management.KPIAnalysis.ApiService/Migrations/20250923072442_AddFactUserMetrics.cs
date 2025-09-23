using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddFactUserMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fact_user_metrics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_days = table.Column<int>(type: "integer", nullable: false),
                    total_commits = table.Column<int>(type: "integer", nullable: false),
                    total_lines_added = table.Column<int>(type: "integer", nullable: false),
                    total_lines_deleted = table.Column<int>(type: "integer", nullable: false),
                    total_lines_changed = table.Column<int>(type: "integer", nullable: false),
                    average_commits_per_day = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    average_lines_changed_per_commit = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    active_projects = table.Column<int>(type: "integer", nullable: false),
                    total_merge_requests_created = table.Column<int>(type: "integer", nullable: false),
                    total_merge_requests_merged = table.Column<int>(type: "integer", nullable: false),
                    total_merge_requests_reviewed = table.Column<int>(type: "integer", nullable: false),
                    average_merge_request_cycle_time_hours = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    merge_request_merge_rate = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    total_pipelines_triggered = table.Column<int>(type: "integer", nullable: false),
                    successful_pipelines = table.Column<int>(type: "integer", nullable: false),
                    failed_pipelines = table.Column<int>(type: "integer", nullable: false),
                    pipeline_success_rate = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    average_pipeline_duration_minutes = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    total_issues_created = table.Column<int>(type: "integer", nullable: false),
                    total_issues_assigned = table.Column<int>(type: "integer", nullable: false),
                    total_issues_closed = table.Column<int>(type: "integer", nullable: false),
                    average_issue_resolution_time_hours = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    total_comments_on_merge_requests = table.Column<int>(type: "integer", nullable: false),
                    total_comments_on_issues = table.Column<int>(type: "integer", nullable: false),
                    collaboration_score = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    productivity_score = table.Column<double>(type: "double precision", precision: 5, scale: 2, nullable: false),
                    productivity_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code_churn_rate = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    review_throughput = table.Column<double>(type: "double precision", precision: 10, scale: 2, nullable: false),
                    total_data_points = table.Column<int>(type: "integer", nullable: false),
                    data_quality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_user_metrics", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_fact_user_metrics_collected_at",
                table: "fact_user_metrics",
                column: "collected_at");

            migrationBuilder.CreateIndex(
                name: "idx_fact_user_metrics_user_collected",
                table: "fact_user_metrics",
                columns: new[] { "user_id", "collected_at" });

            migrationBuilder.CreateIndex(
                name: "idx_fact_user_metrics_user_id",
                table: "fact_user_metrics",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_fact_user_metrics_user_period",
                table: "fact_user_metrics",
                columns: new[] { "user_id", "from_date", "to_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fact_user_metrics");
        }
    }
}
