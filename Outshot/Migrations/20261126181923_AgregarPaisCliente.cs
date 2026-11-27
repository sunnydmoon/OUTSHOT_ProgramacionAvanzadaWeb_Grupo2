using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Outshot.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPaisCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pais",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pais",
                table: "Clientes");
        }
    }
}
