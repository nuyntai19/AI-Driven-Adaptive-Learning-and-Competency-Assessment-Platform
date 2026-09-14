using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceActiveCenterPrimaryManagerDataIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Forward data integrity enforcement:
            // Ensures that on running/existing databases, every Active center has a non-null primary_manager_user_id
            // pointing to an active, non-deleted CenterManager.
            // Uses an ephemeral temporary table with CHECK (1 = 0) constraint to fail-closed without permanent artifacts.
            migrationBuilder.Sql(@"
DROP TEMPORARY TABLE IF EXISTS __active_center_integrity_check;
CREATE TEMPORARY TABLE __active_center_integrity_check (
    center_id VARCHAR(36) NOT NULL,
    CONSTRAINT chk_active_center_must_have_valid_primary_manager CHECK (1 = 0)
);
INSERT INTO __active_center_integrity_check (center_id)
SELECT c.center_id
FROM centers c
WHERE c.center_id != '00000000-0000-0000-0000-000000000001'
  AND c.status = 'Active'
  AND (
      c.primary_manager_user_id IS NULL
      OR NOT EXISTS (
          SELECT 1 FROM users u
          WHERE u.center_id = c.center_id
            AND u.user_id = c.primary_manager_user_id
            AND u.role_name = 'CenterManager'
            AND u.status = 'Active'
            AND u.is_deleted = 0
      )
  );
DROP TEMPORARY TABLE IF EXISTS __active_center_integrity_check;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data integrity check is a verification step and leaves schema intact on rollback.
        }
    }
}
