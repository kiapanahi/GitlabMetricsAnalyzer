using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddWindowedCollectionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_window_end",
                table: "ingestion_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "window_size_hours",
                table: "ingestion_state",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collection_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    window_size_hours = table.Column<int>(type: "integer", nullable: true),
                    projects_processed = table.Column<int>(type: "integer", nullable: false),
                    commits_collected = table.Column<int>(type: "integer", nullable: false),
                    merge_requests_collected = table.Column<int>(type: "integer", nullable: false),
                    pipelines_collected = table.Column<int>(type: "integer", nullable: false),
                    review_events_collected = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_details = table.Column<string>(type: "text", nullable: true),
                    trigger_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ingestion_state_entity",
                table: "ingestion_state",
                column: "entity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_run_type",
                table: "collection_runs",
                column: "run_type");

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_started_at",
                table: "collection_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_status",
                table: "collection_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_type_started",
                table: "collection_runs",
                columns: new[] { "run_type", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_runs");

            migrationBuilder.DropIndex(
                name: "idx_ingestion_state_entity",
                table: "ingestion_state");

            migrationBuilder.DropColumn(
                name: "last_window_end",
                table: "ingestion_state");

            migrationBuilder.DropColumn(
                name: "window_size_hours",
                table: "ingestion_state");
        }
    }
}
