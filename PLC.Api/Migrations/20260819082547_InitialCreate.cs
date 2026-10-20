using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLC.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    CpuType = table.Column<int>(type: "INTEGER", nullable: false),
                    Rack = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoReconnect = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxReconnectAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    ReconnectIntervalMs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlcId = table.Column<int>(type: "INTEGER", nullable: false),
                    DbNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataBlocks_Plcs_PlcId",
                        column: x => x.PlcId,
                        principalTable: "Plcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataBlockVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataBlockId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TypeDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    ByteOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    BitOffset = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataBlockVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataBlockVariables_DataBlocks_DataBlockId",
                        column: x => x.DataBlockId,
                        principalTable: "DataBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataBlocks_PlcId_DbNumber",
                table: "DataBlocks",
                columns: new[] { "PlcId", "DbNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataBlockVariables_DataBlockId",
                table: "DataBlockVariables",
                column: "DataBlockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataBlockVariables");

            migrationBuilder.DropTable(
                name: "DataBlocks");

            migrationBuilder.DropTable(
                name: "Plcs");
        }
    }
}
