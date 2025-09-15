using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Toman.Management.KPIAnalysis.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddIdToIngestionStateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ingestion_state",
                table: "ingestion_state");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ingestion_state",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ingestion_state",
                table: "ingestion_state",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ingestion_state",
                table: "ingestion_state");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ingestion_state");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ingestion_state",
                table: "ingestion_state",
                column: "entity");
        }
    }
}
