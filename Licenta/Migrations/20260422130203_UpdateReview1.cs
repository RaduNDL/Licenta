using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenta.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReview1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AuthorUserId_DoctorId_Target",
                table: "Reviews",
                columns: new[] { "AuthorUserId", "DoctorId", "Target" },
                unique: true,
                filter: "[Target] = 1 AND [DoctorId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_AuthorUserId_DoctorId_Target",
                table: "Reviews");
        }
    }
}
