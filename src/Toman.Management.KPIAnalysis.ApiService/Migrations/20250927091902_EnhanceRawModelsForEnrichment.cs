using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceRawModelsForEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "commits_count",
                table: "raw_mr",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_commit_at",
                table: "raw_mr",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_commit_message",
                table: "raw_mr",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_commit_sha",
                table: "raw_mr",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_conflicts",
                table: "raw_mr",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_draft",
                table: "raw_mr",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_hotfix",
                table: "raw_mr",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_revert",
                table: "raw_mr",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "labels",
                table: "raw_mr",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lines_added",
                table: "raw_mr",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "lines_deleted",
                table: "raw_mr",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "web_url",
                table: "raw_mr",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "additions_excluded",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "deletions_excluded",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "files_changed",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "files_changed_excluded",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_merge_commit",
                table: "raw_commit",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "parent_count",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "parent_shas",
                table: "raw_commit",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_sha",
                table: "raw_commit",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "web_url",
                table: "raw_commit",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commits_count",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "first_commit_at",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "first_commit_message",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "first_commit_sha",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "has_conflicts",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "is_draft",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "is_hotfix",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "is_revert",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "labels",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "lines_added",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "lines_deleted",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "web_url",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "additions_excluded",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "deletions_excluded",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "files_changed",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "files_changed_excluded",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "is_merge_commit",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "parent_count",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "parent_shas",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "short_sha",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "web_url",
                table: "raw_commit");
        }
    }
}
