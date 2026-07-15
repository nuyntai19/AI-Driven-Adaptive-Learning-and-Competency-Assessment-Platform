using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantIdentityOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "centers",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    center_code = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    center_name = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    timezone = table.Column<string>(type: "VARCHAR(64)", nullable: false, defaultValue: "Asia/Bangkok"),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centers", x => x.center_id);
                    table.CheckConstraint("ck_centers_status", "status IN ('Active', 'Suspended')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    subject_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    subject_code = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    subject_name = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    description = table.Column<string>(type: "VARCHAR(500)", nullable: true),
                    is_active = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: true),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.subject_id);
                    table.UniqueConstraint("ux_subjects_center_id_subject_id", x => new { x.center_id, x.subject_id });
                    table.ForeignKey(
                        name: "fk_subjects_centers_tenant",
                        column: x => x.center_id,
                        principalTable: "centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    username = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    password_hash = table.Column<string>(type: "VARCHAR(500)", nullable: false),
                    role_name = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    display_name = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    auth_version = table.Column<uint>(type: "INT UNSIGNED", nullable: false, defaultValue: 1u),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.user_id);
                    table.UniqueConstraint("ux_users_center_id_user_id", x => new { x.center_id, x.user_id });
                    table.CheckConstraint("ck_users_role_name", "role_name IN ('Student', 'Teacher', 'CenterManager')");
                    table.CheckConstraint("ck_users_status", "status IN ('Active', 'Locked', 'Disabled')");
                    table.ForeignKey(
                        name: "fk_users_centers_tenant",
                        column: x => x.center_id,
                        principalTable: "centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    token_hash = table.Column<string>(type: "CHAR(64)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    replaced_by_token_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: true),
                    revoke_reason = table.Column<string>(type: "VARCHAR(200)", nullable: true),
                    created_by_ip = table.Column<string>(type: "VARCHAR(64)", nullable: true),
                    revoked_by_ip = table.Column<string>(type: "VARCHAR(64)", nullable: true),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.refresh_token_id);
                    table.UniqueConstraint("ux_refresh_tokens_center_id_refresh_token_id", x => new { x.center_id, x.refresh_token_id });
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_tokens_replaced_by",
                        columns: x => new { x.center_id, x.replaced_by_token_id },
                        principalTable: "refresh_tokens",
                        principalColumns: new[] { "center_id", "refresh_token_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_tenant",
                        columns: x => new { x.center_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    full_name = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    grade_level = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "DATE", nullable: true),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.student_id);
                    table.UniqueConstraint("ux_students_center_id_student_id", x => new { x.center_id, x.student_id });
                    table.CheckConstraint("ck_students_grade_level", "grade_level BETWEEN 10 AND 12");
                    table.ForeignKey(
                        name: "fk_students_users_profile",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "teachers",
                columns: table => new
                {
                    teacher_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    department = table.Column<string>(type: "VARCHAR(150)", nullable: true),
                    bio = table.Column<string>(type: "VARCHAR(500)", nullable: true),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teachers", x => x.teacher_id);
                    table.UniqueConstraint("ux_teachers_center_id_teacher_id", x => new { x.center_id, x.teacher_id });
                    table.ForeignKey(
                        name: "fk_teachers_users_profile",
                        columns: x => new { x.center_id, x.teacher_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    class_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    teacher_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    subject_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    class_name = table.Column<string>(type: "VARCHAR(150)", nullable: false),
                    academic_year = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classes", x => x.class_id);
                    table.UniqueConstraint("ux_classes_center_id_class_id", x => new { x.center_id, x.class_id });
                    table.CheckConstraint("ck_classes_status", "status IN ('Active', 'Archived')");
                    table.ForeignKey(
                        name: "fk_classes_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_classes_teachers_teacher",
                        columns: x => new { x.center_id, x.teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "class_students",
                columns: table => new
                {
                    class_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    student_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    joined_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    removed_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_students", x => new { x.center_id, x.class_id, x.student_id });
                    table.CheckConstraint("ck_class_students_status", "status IN ('Active', 'Removed')");
                    table.ForeignKey(
                        name: "fk_class_students_classes_class",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_students_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_centers_center_code",
                table: "centers",
                column: "center_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_students_center_id_student_id_status",
                table: "class_students",
                columns: new[] { "center_id", "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_classes_center_id_subject_id_status",
                table: "classes",
                columns: new[] { "center_id", "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_classes_center_id_teacher_id_status",
                table: "classes",
                columns: new[] { "center_id", "teacher_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_classes_center_id_class_name_academic_year",
                table: "classes",
                columns: new[] { "center_id", "class_name", "academic_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_center_id_replaced_by_token_id",
                table: "refresh_tokens",
                columns: new[] { "center_id", "replaced_by_token_id" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_center_id_user_id_expires_at",
                table: "refresh_tokens",
                columns: new[] { "center_id", "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_center_id_grade_level",
                table: "students",
                columns: new[] { "center_id", "grade_level" });

            migrationBuilder.CreateIndex(
                name: "ux_subjects_center_id_subject_code",
                table: "subjects",
                columns: new[] { "center_id", "subject_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_center_id_role_name_status",
                table: "users",
                columns: new[] { "center_id", "role_name", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_users_center_id_username",
                table: "users",
                columns: new[] { "center_id", "username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_students");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "teachers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "centers");
        }
    }
}
