using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryManagerToCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "primary_manager_user_id",
                table: "centers",
                type: "VARCHAR(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_centers_center_id_primary_manager_user_id",
                table: "centers",
                columns: new[] { "center_id", "primary_manager_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_centers_primary_manager_user_id",
                table: "centers",
                column: "primary_manager_user_id",
                unique: true);

            // Preflight backfill: Assign earliest created active CenterManager for any active centers
            migrationBuilder.Sql(@"
UPDATE centers c
JOIN (
    SELECT u.center_id, u.user_id,
           ROW_NUMBER() OVER (PARTITION BY u.center_id ORDER BY u.created_at ASC, u.user_id ASC) as rn
    FROM users u
    WHERE u.role_name = 'CenterManager' AND u.status = 'Active' AND u.is_deleted = 0
) m ON c.center_id = m.center_id AND m.rn = 1
SET c.primary_manager_user_id = m.user_id
WHERE c.center_id != '00000000-0000-0000-0000-000000000001' AND c.status = 'Active';
");

            migrationBuilder.AddForeignKey(
                name: "fk_centers_primary_manager_user",
                table: "centers",
                columns: new[] { "center_id", "primary_manager_user_id" },
                principalTable: "users",
                principalColumns: new[] { "center_id", "user_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_centers_primary_manager_user",
                table: "centers");

            migrationBuilder.DropIndex(
                name: "IX_centers_center_id_primary_manager_user_id",
                table: "centers");

            migrationBuilder.DropIndex(
                name: "ix_centers_primary_manager_user_id",
                table: "centers");

            migrationBuilder.DropColumn(
                name: "primary_manager_user_id",
                table: "centers");
        }
    }
}
