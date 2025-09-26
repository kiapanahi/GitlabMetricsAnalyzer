using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dim_release");

            migrationBuilder.DropTable(
                name: "fact_git_hygiene");

            migrationBuilder.DropTable(
                name: "fact_release");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "fact_release",
                columns: table => new
                {
                    tag_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    cadence_bucket = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_semver = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fact_release", x => new { x.tag_name, x.project_id });
                });
        }
    }
}
