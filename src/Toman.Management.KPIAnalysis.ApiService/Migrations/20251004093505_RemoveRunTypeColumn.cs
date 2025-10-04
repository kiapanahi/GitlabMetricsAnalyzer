using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRunTypeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_collection_runs_run_type",
                table: "collection_runs");

            migrationBuilder.DropIndex(
                name: "idx_collection_runs_type_started",
                table: "collection_runs");

            migrationBuilder.DropColumn(
                name: "run_type",
                table: "collection_runs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "run_type",
                table: "collection_runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_run_type",
                table: "collection_runs",
                column: "run_type");

            migrationBuilder.CreateIndex(
                name: "idx_collection_runs_type_started",
                table: "collection_runs",
                columns: new[] { "run_type", "started_at" });
        }
    }
}
