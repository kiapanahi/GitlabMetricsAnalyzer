using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeveloperMetricsAggregateForPRD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_dev_metrics_agg_developer_period",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropIndex(
                name: "idx_dev_metrics_agg_period",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "commits_count",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "files_changed",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "lines_added",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "period_end",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "period_start",
                table: "developer_metrics_aggregates");

            migrationBuilder.RenameColumn(
                name: "unique_collaborators",
                table: "developer_metrics_aggregates",
                newName: "wip_mr_count");

            migrationBuilder.RenameColumn(
                name: "successful_pipelines",
                table: "developer_metrics_aggregates",
                newName: "window_days");

            migrationBuilder.RenameColumn(
                name: "reviews_given",
                table: "developer_metrics_aggregates",
                newName: "rollback_incidence");

            migrationBuilder.RenameColumn(
                name: "pipelines_triggered",
                table: "developer_metrics_aggregates",
                newName: "releases_cadence_wk");

            migrationBuilder.RenameColumn(
                name: "period_type",
                table: "developer_metrics_aggregates",
                newName: "schema_version");

            migrationBuilder.RenameColumn(
                name: "mrs_reviewed",
                table: "developer_metrics_aggregates",
                newName: "mr_throughput_wk");

            migrationBuilder.RenameColumn(
                name: "mrs_merged",
                table: "developer_metrics_aggregates",
                newName: "force_pushes_protected");

            migrationBuilder.RenameColumn(
                name: "mrs_created",
                table: "developer_metrics_aggregates",
                newName: "direct_pushes_default");

            migrationBuilder.RenameColumn(
                name: "lines_deleted",
                table: "developer_metrics_aggregates",
                newName: "deployment_frequency_wk");

            migrationBuilder.RenameColumn(
                name: "avg_cycle_time_hours",
                table: "developer_metrics_aggregates",
                newName: "wip_age_p90h");

            migrationBuilder.AddColumn<decimal>(
                name: "approval_bypass_ratio",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audit_metadata",
                table: "developer_metrics_aggregates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "avg_pipeline_duration_sec",
                table: "developer_metrics_aggregates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "branch_ttl_p50h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "branch_ttl_p90h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "defect_escape_rate",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "flaky_job_rate",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "issue_sla_breach_rate",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mean_time_to_green_sec",
                table: "developer_metrics_aggregates",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mr_cycle_time_p50h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "null_reasons",
                table: "developer_metrics_aggregates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reopened_issue_rate",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rework_rate",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "signed_commit_ratio",
                table: "developer_metrics_aggregates",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "time_in_review_p50h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "time_to_first_review_p50h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_end",
                table: "developer_metrics_aggregates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_start",
                table: "developer_metrics_aggregates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<decimal>(
                name: "wip_age_p50h",
                table: "developer_metrics_aggregates",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_developer_window",
                table: "developer_metrics_aggregates",
                columns: new[] { "developer_id", "window_start", "window_days" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_schema_version",
                table: "developer_metrics_aggregates",
                column: "schema_version");

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_window",
                table: "developer_metrics_aggregates",
                columns: new[] { "window_start", "window_end" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_dev_metrics_agg_developer_window",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropIndex(
                name: "idx_dev_metrics_agg_schema_version",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropIndex(
                name: "idx_dev_metrics_agg_window",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "approval_bypass_ratio",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "audit_metadata",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "avg_pipeline_duration_sec",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "branch_ttl_p50h",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "branch_ttl_p90h",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "defect_escape_rate",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "flaky_job_rate",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "issue_sla_breach_rate",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "mean_time_to_green_sec",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "mr_cycle_time_p50h",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "null_reasons",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "reopened_issue_rate",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "rework_rate",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "signed_commit_ratio",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "time_in_review_p50h",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "time_to_first_review_p50h",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "window_end",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "window_start",
                table: "developer_metrics_aggregates");

            migrationBuilder.DropColumn(
                name: "wip_age_p50h",
                table: "developer_metrics_aggregates");

            migrationBuilder.RenameColumn(
                name: "wip_mr_count",
                table: "developer_metrics_aggregates",
                newName: "unique_collaborators");

            migrationBuilder.RenameColumn(
                name: "wip_age_p90h",
                table: "developer_metrics_aggregates",
                newName: "avg_cycle_time_hours");

            migrationBuilder.RenameColumn(
                name: "window_days",
                table: "developer_metrics_aggregates",
                newName: "successful_pipelines");

            migrationBuilder.RenameColumn(
                name: "schema_version",
                table: "developer_metrics_aggregates",
                newName: "period_type");

            migrationBuilder.RenameColumn(
                name: "rollback_incidence",
                table: "developer_metrics_aggregates",
                newName: "reviews_given");

            migrationBuilder.RenameColumn(
                name: "releases_cadence_wk",
                table: "developer_metrics_aggregates",
                newName: "pipelines_triggered");

            migrationBuilder.RenameColumn(
                name: "mr_throughput_wk",
                table: "developer_metrics_aggregates",
                newName: "mrs_reviewed");

            migrationBuilder.RenameColumn(
                name: "force_pushes_protected",
                table: "developer_metrics_aggregates",
                newName: "mrs_merged");

            migrationBuilder.RenameColumn(
                name: "direct_pushes_default",
                table: "developer_metrics_aggregates",
                newName: "mrs_created");

            migrationBuilder.RenameColumn(
                name: "deployment_frequency_wk",
                table: "developer_metrics_aggregates",
                newName: "lines_deleted");

            migrationBuilder.AddColumn<int>(
                name: "commits_count",
                table: "developer_metrics_aggregates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "files_changed",
                table: "developer_metrics_aggregates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "lines_added",
                table: "developer_metrics_aggregates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_end",
                table: "developer_metrics_aggregates",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_start",
                table: "developer_metrics_aggregates",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_developer_period",
                table: "developer_metrics_aggregates",
                columns: new[] { "developer_id", "period_type", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dev_metrics_agg_period",
                table: "developer_metrics_aggregates",
                columns: new[] { "period_type", "period_start" });
        }
    }
}
