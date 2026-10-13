using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicClassScopeAndCurriculumApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "learning_scope",
                table: "classes",
                type: "VARCHAR(16)",
                nullable: false,
                defaultValue: "Current");

            migrationBuilder.CreateTable(
                name: "class_curriculum_applications",
                columns: table => new
                {
                    application_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    class_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    curriculum_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    application_role = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    assigned_by = table.Column<string>(type: "varchar(36)", nullable: false),
                    ended_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ended_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    change_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    end_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    class_grade_at_start = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    curriculum_grade_at_start = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    is_grade_exception = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    grade_mismatch_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    exception_approved_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    exception_approved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    current_primary_key = table.Column<string>(type: "varchar(36)", nullable: true, computedColumnSql: "CASE WHEN ended_at IS NULL AND application_role='Primary' THEN class_id ELSE NULL END", stored: true),
                    current_curriculum_key = table.Column<string>(type: "varchar(36)", nullable: true, computedColumnSql: "CASE WHEN ended_at IS NULL THEN curriculum_id ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_curriculum_applications", x => x.application_id);
                    table.CheckConstraint("ck_class_curriculum_application_dates", "(ended_at IS NULL AND ended_by IS NULL) OR (ended_at IS NOT NULL AND ended_at >= started_at AND ended_by IS NOT NULL)");
                    table.CheckConstraint("ck_class_curriculum_application_exception", "(is_grade_exception=0 AND grade_mismatch_reason IS NULL AND exception_approved_by IS NULL AND exception_approved_at IS NULL) OR (is_grade_exception=1 AND grade_mismatch_reason IS NOT NULL AND CHAR_LENGTH(TRIM(grade_mismatch_reason))>0 AND exception_approved_by IS NOT NULL AND exception_approved_at IS NOT NULL)");
                    table.CheckConstraint("ck_class_curriculum_application_grades", "(class_grade_at_start IS NULL OR class_grade_at_start BETWEEN 10 AND 12) AND (curriculum_grade_at_start IS NULL OR curriculum_grade_at_start BETWEEN 10 AND 12)");
                    table.CheckConstraint("ck_class_curriculum_application_role", "application_role IN ('Primary','Supplemental')");
                    table.ForeignKey(
                        name: "FK_class_curriculum_applications_classes_center_id_class_id",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_curriculum_applications_curriculums_center_id_curricul~",
                        columns: x => new { x.center_id, x.curriculum_id },
                        principalTable: "curriculums",
                        principalColumns: new[] { "center_id", "curriculum_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_curriculum_applications_users_center_id_assigned_by",
                        columns: x => new { x.center_id, x.assigned_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_curriculum_applications_users_center_id_ended_by",
                        columns: x => new { x.center_id, x.ended_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_curriculum_applications_users_center_id_exception_appr~",
                        columns: x => new { x.center_id, x.exception_approved_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_classes_learning_scope",
                table: "classes",
                sql: "learning_scope IN ('Current','History')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_students_enrollment_grade",
                table: "class_students",
                sql: "grade_level_at_enrollment IS NULL OR grade_level_at_enrollment BETWEEN 10 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_students_exception_complete",
                table: "class_students",
                sql: "(grade_mismatch_reason IS NULL AND exception_approved_by IS NULL AND exception_approved_at IS NULL) OR (grade_mismatch_reason IS NOT NULL AND CHAR_LENGTH(TRIM(grade_mismatch_reason))>0 AND exception_approved_by IS NOT NULL AND exception_approved_at IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_assigned_by",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "assigned_by" });

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_class_id_current_cur~",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "class_id", "current_curriculum_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_class_id_started_at",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "class_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_current_primary_key",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "current_primary_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_curriculum_id",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "curriculum_id" });

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_ended_by",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "ended_by" });

            migrationBuilder.CreateIndex(
                name: "IX_class_curriculum_applications_center_id_exception_approved_by",
                table: "class_curriculum_applications",
                columns: new[] { "center_id", "exception_approved_by" });
            // Preserve legacy membership, assignment and curriculum planning rows.
            // Unclassified old classes are history scopes, not current mixed classes.
            migrationBuilder.Sql("UPDATE classes SET learning_scope='History' WHERE grade_level IS NULL;");
            migrationBuilder.Sql("""
                INSERT INTO class_curriculum_applications
                (application_id,center_id,class_id,curriculum_id,subject_id,application_role,started_at,assigned_by,
                 class_grade_at_start,curriculum_grade_at_start,is_grade_exception,change_reason)
                SELECT LOWER(UUID()), center_id,class_id,curriculum_id,subject_id,
                    CASE WHEN rn=1 THEN 'Primary' ELSE 'Supplemental' END,assigned_at,assigned_by,
                    class_grade,curriculum_grade,0,'Liên kết có trước nâng cấp; giữ nguyên dữ liệu lịch sử.'
                FROM (
                    SELECT cc.center_id,cc.class_id,cc.curriculum_id,c.subject_id,cc.assigned_at,cc.assigned_by,
                        c.grade_level AS class_grade,cu.grade_level AS curriculum_grade,
                        ROW_NUMBER() OVER(PARTITION BY cc.center_id,cc.class_id ORDER BY cc.assigned_at DESC,cc.curriculum_id) AS rn
                    FROM curriculum_classes cc
                    JOIN classes c ON c.center_id=cc.center_id AND c.class_id=cc.class_id
                    JOIN curriculums cu ON cu.center_id=cc.center_id AND cu.curriculum_id=cc.curriculum_id AND cu.subject_id=c.subject_id
                    JOIN users u ON u.center_id=cc.center_id AND u.user_id=cc.assigned_by
                    WHERE cu.review_status IN ('Published','Archived') AND cu.is_deleted=0 AND c.is_deleted=0
                ) AS legacy;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_curriculum_application_validate_insert BEFORE INSERT ON class_curriculum_applications
                FOR EACH ROW
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM classes c JOIN curriculums cu ON cu.center_id=c.center_id AND cu.subject_id=c.subject_id
                        JOIN users u ON u.center_id=c.center_id AND u.user_id=NEW.assigned_by
                        WHERE c.center_id=NEW.center_id AND c.class_id=NEW.class_id AND cu.curriculum_id=NEW.curriculum_id
                        AND c.subject_id=NEW.subject_id AND c.status='Active' AND c.learning_scope='Current' AND c.is_deleted=0 AND cu.is_deleted=0
                        AND cu.review_status='Published' AND c.teacher_id=NEW.assigned_by
                        AND u.role_name='Teacher' AND u.status='Active' AND u.is_deleted=0
                        AND (cu.teacher_id=NEW.assigned_by OR cu.visibility='Shared')
                        AND (c.grade_level <=> NEW.class_grade_at_start) AND (cu.grade_level <=> NEW.curriculum_grade_at_start)
                        AND ((c.grade_level IS NOT NULL AND cu.grade_level=c.grade_level AND NEW.is_grade_exception=0)
                            OR (NEW.is_grade_exception=1 AND NEW.exception_approved_by=NEW.assigned_by AND NEW.exception_approved_at IS NOT NULL))
                    ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid curriculum application scope, actor, status or grade'; END IF;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_curriculum_application_history_update BEFORE UPDATE ON class_curriculum_applications
                FOR EACH ROW
                BEGIN
                    IF OLD.ended_at IS NOT NULL OR NEW.ended_at IS NULL OR NEW.ended_by IS NULL OR
                        NEW.end_reason IS NULL OR CHAR_LENGTH(TRIM(NEW.end_reason))=0 OR
                        NOT(NEW.change_reason <=> OLD.change_reason) OR
                        NOT(NEW.application_id <=> OLD.application_id) OR NOT(NEW.center_id <=> OLD.center_id) OR
                        NOT(NEW.class_id <=> OLD.class_id) OR NOT(NEW.curriculum_id <=> OLD.curriculum_id) OR
                        NOT(NEW.subject_id <=> OLD.subject_id) OR NOT(NEW.application_role <=> OLD.application_role) OR
                        NOT(NEW.started_at <=> OLD.started_at) OR NOT(NEW.assigned_by <=> OLD.assigned_by) OR
                        NOT(NEW.class_grade_at_start <=> OLD.class_grade_at_start) OR NOT(NEW.curriculum_grade_at_start <=> OLD.curriculum_grade_at_start) OR
                        NOT(NEW.is_grade_exception <=> OLD.is_grade_exception) OR NOT(NEW.grade_mismatch_reason <=> OLD.grade_mismatch_reason) OR
                        NOT(NEW.exception_approved_by <=> OLD.exception_approved_by) OR NOT(NEW.exception_approved_at <=> OLD.exception_approved_at)
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Curriculum history is immutable; only end an active application with a reason'; END IF;
                    IF NOT EXISTS (SELECT 1 FROM users u JOIN classes c ON c.center_id=u.center_id
                        JOIN curriculums cu ON cu.center_id=c.center_id
                        WHERE u.center_id=NEW.center_id AND u.user_id=NEW.ended_by AND u.role_name='Teacher' AND u.status='Active' AND u.is_deleted=0
                            AND c.class_id=NEW.class_id AND cu.curriculum_id=NEW.curriculum_id
                            AND (c.teacher_id=NEW.ended_by OR cu.teacher_id=NEW.ended_by))
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Only the responsible teacher may end a curriculum application'; END IF;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_curriculum_application_no_delete BEFORE DELETE ON class_curriculum_applications
                FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Curriculum application history cannot be deleted';
                """);
            foreach (var operation in new[] { "INSERT", "UPDATE" })
                migrationBuilder.Sql($"""
                    CREATE TRIGGER tr_curriculum_nodes_subject_{operation.ToLowerInvariant()} BEFORE {operation} ON curriculum_nodes
                    FOR EACH ROW
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM curriculums cu JOIN knowledge_nodes n ON n.center_id=cu.center_id AND n.subject_id=cu.subject_id
                            WHERE cu.center_id=NEW.center_id AND cu.curriculum_id=NEW.curriculum_id AND n.node_id=NEW.node_id)
                        THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Curriculum and knowledge node must have the same subject and center'; END IF;
                    END;
                    """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_class_academic_identity_update BEFORE UPDATE ON classes FOR EACH ROW
                BEGIN
                    IF (NOT(NEW.subject_id <=> OLD.subject_id) AND EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=OLD.center_id AND a.class_id=OLD.class_id))
                        OR (NOT(NEW.grade_level <=> OLD.grade_level) AND EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=OLD.center_id AND a.class_id=OLD.class_id AND a.ended_at IS NULL))
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='End active curricula before changing class grade; historical subject identity is immutable'; END IF;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_curriculum_academic_identity_update BEFORE UPDATE ON curriculums FOR EACH ROW
                BEGIN
                    IF NOT(NEW.subject_id <=> OLD.subject_id) AND
                        (EXISTS(SELECT 1 FROM curriculum_nodes n WHERE n.center_id=OLD.center_id AND n.curriculum_id=OLD.curriculum_id)
                         OR EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=OLD.center_id AND a.curriculum_id=OLD.curriculum_id))
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Curriculum subject identity cannot change after composition or application'; END IF;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER tr_knowledge_curriculum_subject_update BEFORE UPDATE ON knowledge_nodes FOR EACH ROW
                BEGIN
                    IF NOT(NEW.subject_id <=> OLD.subject_id) AND EXISTS(SELECT 1 FROM curriculum_nodes n WHERE n.center_id=OLD.center_id AND n.node_id=OLD.node_id)
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='A curriculum knowledge node cannot move to another subject'; END IF;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var trigger in new[] { "tr_curriculum_application_validate_insert", "tr_curriculum_application_history_update", "tr_curriculum_application_no_delete", "tr_curriculum_nodes_subject_insert", "tr_curriculum_nodes_subject_update", "tr_class_academic_identity_update", "tr_curriculum_academic_identity_update", "tr_knowledge_curriculum_subject_update" })
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
            migrationBuilder.DropTable(
                name: "class_curriculum_applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_classes_learning_scope",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_students_enrollment_grade",
                table: "class_students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_students_exception_complete",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "learning_scope",
                table: "classes");
        }
    }
}
