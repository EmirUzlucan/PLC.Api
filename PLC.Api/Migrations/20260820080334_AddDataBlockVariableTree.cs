using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PLC.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDataBlockVariableTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataBlockVariables_DataBlockId",
                table: "DataBlockVariables");

            migrationBuilder.AlterColumn<int>(
                name: "BitOffset",
                table: "DataBlockVariables",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "DataBlockVariables",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "DataBlockVariables",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Size",
                table: "DataBlockVariables",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DataBlockVariables_DataBlockId_Path",
                table: "DataBlockVariables",
                columns: new[] { "DataBlockId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataBlockVariables_ParentId",
                table: "DataBlockVariables",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DataBlockVariables_DataBlockVariables_ParentId",
                table: "DataBlockVariables",
                column: "ParentId",
                principalTable: "DataBlockVariables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataBlockVariables_DataBlockVariables_ParentId",
                table: "DataBlockVariables");

            migrationBuilder.DropIndex(
                name: "IX_DataBlockVariables_DataBlockId_Path",
                table: "DataBlockVariables");

            migrationBuilder.DropIndex(
                name: "IX_DataBlockVariables_ParentId",
                table: "DataBlockVariables");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "DataBlockVariables");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "DataBlockVariables");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "DataBlockVariables");

            migrationBuilder.AlterColumn<int>(
                name: "BitOffset",
                table: "DataBlockVariables",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_DataBlockVariables_DataBlockId",
                table: "DataBlockVariables",
                column: "DataBlockId");
        }
    }
}
