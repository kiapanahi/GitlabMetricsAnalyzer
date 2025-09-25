using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddDataQualityIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "data_quality_warnings",
                table: "fact_user_metrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metric_quality_json",
                table: "fact_user_metrics",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "overall_confidence_score",
                table: "fact_user_metrics",
                type: "double precision",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_quality_warnings",
                table: "fact_user_metrics");

            migrationBuilder.DropColumn(
                name: "metric_quality_json",
                table: "fact_user_metrics");

            migrationBuilder.DropColumn(
                name: "overall_confidence_score",
                table: "fact_user_metrics");
        }
    }
}
