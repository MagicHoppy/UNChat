using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UNChat.Migrations
{
    /// <inheritdoc />
    public partial class przyjaciele : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Friends",
                columns: table => new
                {
                    Friend1Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Friend2Id = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friends", x => new { x.Friend1Id, x.Friend2Id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Friends");
        }
    }
}
