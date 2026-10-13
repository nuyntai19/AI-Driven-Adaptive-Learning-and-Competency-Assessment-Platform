using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignClassLifecycleScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Status is authoritative. Missing grade is NOT evidence that a class was archived.
            // Null actor explicitly records a system correction, never a fabricated manager action.
            migrationBuilder.Sql("""
                START TRANSACTION;
                INSERT INTO authorization_audit_logs
                  (center_id, actor_user_id, action_type, target_type, target_id, before_data, after_data, reason, trace_id, created_at, created_by)
                SELECT center_id, NULL, 'ClassScopeCorrected', 'Class', class_id,
                  JSON_OBJECT('Status', status, 'LearningScope', learning_scope, 'RowVersion', row_version),
                  JSON_OBJECT('Status', status, 'LearningScope', IF(status='Active','Current','History'), 'RowVersion', row_version+1, 'Source','System/Migration'),
                  'Migration sửa phân loại phạm vi lớp theo trạng thái đã lưu; không suy ra lưu trữ từ khối chưa xác định.',
                  'migration:align-class-lifecycle', UTC_TIMESTAMP(6), NULL
                FROM classes
                WHERE learning_scope <> IF(status='Active','Current','History');
                UPDATE classes SET learning_scope=IF(status='Active','Current','History'),
                    row_version=row_version+1, updated_at=UTC_TIMESTAMP(6), updated_by=NULL
                WHERE learning_scope <> IF(status='Active','Current','History');
                COMMIT;
                """, suppressTransaction: true);
            migrationBuilder.AddCheckConstraint(
                name: "ck_classes_lifecycle_scope",
                table: "classes",
                sql: "(status='Active' AND learning_scope='Current') OR (status='Archived' AND learning_scope='History')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_classes_lifecycle_scope",
                table: "classes");
        }
    }
}
