using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class Consolidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_pipeline",
                table: "raw_pipeline");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_mr",
                table: "raw_mr");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_job",
                table: "raw_job");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_issue",
                table: "raw_issue");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_commit",
                table: "raw_commit");

            migrationBuilder.AlterColumn<long>(
                name: "author_user_id",
                table: "raw_pipeline",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "pipeline_id",
                table: "raw_pipeline",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "project_id",
                table: "raw_pipeline",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "raw_pipeline",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "raw_mr",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "project_id",
                table: "raw_job",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "pipeline_id",
                table: "raw_job",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "job_id",
                table: "raw_job",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "raw_job",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "project_id",
                table: "raw_issue",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "author_user_id",
                table: "raw_issue",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "issue_id",
                table: "raw_issue",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "raw_issue",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "author_user_id",
                table: "raw_commit",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "project_id",
                table: "raw_commit",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "raw_commit",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_pipeline",
                table: "raw_pipeline",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_mr",
                table: "raw_mr",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_job",
                table: "raw_job",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_issue",
                table: "raw_issue",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_commit",
                table: "raw_commit",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "idx_raw_pipeline_project_pipeline",
                table: "raw_pipeline",
                columns: new[] { "project_id", "pipeline_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_raw_mr_project_mr",
                table: "raw_mr",
                columns: new[] { "project_id", "mr_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_raw_job_project_job",
                table: "raw_job",
                columns: new[] { "project_id", "job_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_raw_issue_project_issue",
                table: "raw_issue",
                columns: new[] { "project_id", "issue_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_raw_commit_project_commit",
                table: "raw_commit",
                columns: new[] { "project_id", "commit_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_pipeline",
                table: "raw_pipeline");

            migrationBuilder.DropIndex(
                name: "idx_raw_pipeline_project_pipeline",
                table: "raw_pipeline");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_mr",
                table: "raw_mr");

            migrationBuilder.DropIndex(
                name: "idx_raw_mr_project_mr",
                table: "raw_mr");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_job",
                table: "raw_job");

            migrationBuilder.DropIndex(
                name: "idx_raw_job_project_job",
                table: "raw_job");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_issue",
                table: "raw_issue");

            migrationBuilder.DropIndex(
                name: "idx_raw_issue_project_issue",
                table: "raw_issue");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raw_commit",
                table: "raw_commit");

            migrationBuilder.DropIndex(
                name: "idx_raw_commit_project_commit",
                table: "raw_commit");

            migrationBuilder.DropColumn(
                name: "id",
                table: "raw_pipeline");

            migrationBuilder.DropColumn(
                name: "id",
                table: "raw_mr");

            migrationBuilder.DropColumn(
                name: "id",
                table: "raw_job");

            migrationBuilder.DropColumn(
                name: "id",
                table: "raw_issue");

            migrationBuilder.DropColumn(
                name: "id",
                table: "raw_commit");

            migrationBuilder.AlterColumn<int>(
                name: "project_id",
                table: "raw_pipeline",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "pipeline_id",
                table: "raw_pipeline",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "author_user_id",
                table: "raw_pipeline",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "project_id",
                table: "raw_job",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "pipeline_id",
                table: "raw_job",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "job_id",
                table: "raw_job",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "project_id",
                table: "raw_issue",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "issue_id",
                table: "raw_issue",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "author_user_id",
                table: "raw_issue",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "project_id",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "author_user_id",
                table: "raw_commit",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_pipeline",
                table: "raw_pipeline",
                columns: new[] { "project_id", "pipeline_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_mr",
                table: "raw_mr",
                columns: new[] { "project_id", "mr_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_job",
                table: "raw_job",
                column: "job_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_issue",
                table: "raw_issue",
                column: "issue_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raw_commit",
                table: "raw_commit",
                columns: new[] { "project_id", "commit_id" });
        }
    }
}
