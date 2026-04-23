using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenta.Migrations
{
    /// <inheritdoc />
    public partial class UniqueApplicationReviewPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UniqueApplicationReviewPerUser",
                table: "Reviews",
                columns: new[] { "AuthorUserId", "Target" },
                unique: true,
                filter: "[Target] = 0 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_UniqueApplicationReviewPerUser",
                table: "Reviews");
        }
    }
}
