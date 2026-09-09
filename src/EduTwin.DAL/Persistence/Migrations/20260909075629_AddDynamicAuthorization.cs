using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ux_users_center_id_user_id_role_name",
                table: "users",
                columns: new[] { "center_id", "user_id", "role_name" });

            migrationBuilder.CreateTable(
                name: "authorization_audit_logs",
                columns: table => new
                {
                    authorization_audit_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    actor_user_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    action_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    target_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    target_id = table.Column<string>(type: "varchar(128)", nullable: false),
                    target_user_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    permission_code = table.Column<string>(type: "varchar(100)", nullable: true),
                    before_data = table.Column<string>(type: "json", nullable: true),
                    after_data = table.Column<string>(type: "json", nullable: true),
                    reason = table.Column<string>(type: "varchar(1000)", nullable: false),
                    trace_id = table.Column<string>(type: "varchar(64)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorization_audit_logs", x => x.authorization_audit_id);
                    table.UniqueConstraint("ux_authorization_audit_logs_center_id_audit_id", x => new { x.center_id, x.authorization_audit_id });
                    table.ForeignKey(
                        name: "fk_authorization_audit_logs_users_actor",
                        columns: x => new { x.center_id, x.actor_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_authorization_audit_logs_users_target",
                        columns: x => new { x.center_id, x.target_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    permission_code = table.Column<string>(type: "varchar(100)", nullable: false),
                    module_name = table.Column<string>(type: "varchar(64)", nullable: false),
                    resource_name = table.Column<string>(type: "varchar(64)", nullable: false),
                    action_name = table.Column<string>(type: "varchar(32)", nullable: false),
                    description = table.Column<string>(type: "varchar(500)", nullable: false),
                    is_sensitive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_delegable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.permission_id);
                    table.CheckConstraint("ck_permissions_status", "`status` IN ('Active', 'Deprecated')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    role_code = table.Column<string>(type: "varchar(64)", nullable: false),
                    role_name = table.Column<string>(type: "varchar(150)", nullable: false),
                    account_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    description = table.Column<string>(type: "varchar(500)", nullable: true),
                    is_system_role = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.role_id);
                    table.UniqueConstraint("ux_roles_center_id_role_id", x => new { x.center_id, x.role_id });
                    table.UniqueConstraint("ux_roles_center_id_role_id_account_type", x => new { x.center_id, x.role_id, x.account_type });
                    table.CheckConstraint("ck_roles_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')");
                    table.CheckConstraint("ck_roles_status", "`status` IN ('Active', 'Archived')");
                    table.ForeignKey(
                        name: "fk_roles_centers_tenant",
                        column: x => x.center_id,
                        principalTable: "centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "permission_account_types",
                columns: table => new
                {
                    permission_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    account_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_account_types", x => new { x.permission_id, x.account_type });
                    table.CheckConstraint("ck_permission_account_types_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')");
                    table.ForeignKey(
                        name: "fk_permission_account_types_permissions",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    user_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    role_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    account_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    assigned_by_user_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_by_user_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    revoke_reason = table.Column<string>(type: "varchar(500)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.center_id, x.user_id, x.role_id });
                    table.CheckConstraint("ck_user_roles_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')");
                    table.CheckConstraint("ck_user_roles_status", "`status` IN ('Active', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_user_roles_roles_account_type",
                        columns: x => new { x.center_id, x.role_id, x.account_type },
                        principalTable: "roles",
                        principalColumns: new[] { "center_id", "role_id", "account_type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_account_type",
                        columns: x => new { x.center_id, x.user_id, x.account_type },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id", "role_name" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_assigned_by",
                        columns: x => new { x.center_id, x.assigned_by_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_revoked_by",
                        columns: x => new { x.center_id, x.revoked_by_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    role_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    permission_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    account_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    granted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    granted_by_user_id = table.Column<string>(type: "varchar(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.center_id, x.role_id, x.permission_id });
                    table.CheckConstraint("ck_role_permissions_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')");
                    table.ForeignKey(
                        name: "fk_role_permissions_permission_account_types",
                        columns: x => new { x.permission_id, x.account_type },
                        principalTable: "permission_account_types",
                        principalColumns: new[] { "permission_id", "account_type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_account_type",
                        columns: x => new { x.center_id, x.role_id, x.account_type },
                        principalTable: "roles",
                        principalColumns: new[] { "center_id", "role_id", "account_type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_permissions_users_granted_by",
                        columns: x => new { x.center_id, x.granted_by_user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "0107fed3-60db-5dec-91e4-19264b9f6cc4", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read permissions trong phạm vi được cấp.", true, false, "Authorization", "authorization.permissions.read", "Permissions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "0bf8a5ff-bf1e-5d4d-a0b0-eea80249efbe", "assign", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép assign user_roles trong phạm vi được cấp.", true, true, "Authorization", "authorization.user_roles.assign", "User_roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "1bea1cdd-98a7-5f16-9d82-26031c6bf82a", "manage_members", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép manage_members classes trong phạm vi được cấp.", true, true, "Organization", "organization.classes.manage_members", "Classes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "1d553e2a-603c-52fb-90b8-23abaf838e30", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read roles trong phạm vi được cấp.", true, false, "Authorization", "authorization.roles.read", "Roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "1df2878a-2f15-5d98-aff5-985f9bc8d20c", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read curriculums trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.curriculums.read", "Curriculums", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "20bfe8fa-9a9b-57a2-a61e-434839ccacc9", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read questions trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.questions.read", "Questions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "265a0526-9217-5fac-a735-b1ff1b522621", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read audit trong phạm vi được cấp.", true, true, "Authorization", "authorization.audit.read", "Audit", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "273babe7-569d-56c9-812c-417128fcbf49", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create curriculums trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.curriculums.create", "Curriculums", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "2a640d34-c437-564f-9abe-0b4d3f04b7bb", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own student trong phạm vi được cấp.", true, false, "Recommendations", "recommendations.student.read_own", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "2aff255a-56fe-5c97-a598-693dc5fa4cf1", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create questions trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.questions.create", "Questions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "2d7350d0-6faa-54ff-9ac1-f770e17b8113", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update center trong phạm vi được cấp.", true, true, "Organization", "organization.center.update", "Center", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "31a15ec9-7e7b-5c1c-b11e-95d9933f46dc", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create nodes trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.nodes.create", "Nodes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "365216e5-c1b9-5ebf-9f60-e0dbe1562072", "review", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép review reasoning trong phạm vi được cấp.", true, false, "Twin", "twin.reasoning.review", "Reasoning", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "418b6ea7-7dfd-5a9e-a8ad-7e95633e70b9", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read center trong phạm vi được cấp.", true, false, "Organization", "organization.center.read", "Center", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "423a2eda-b505-5984-85d4-02d2e2fff834", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update assignments trong phạm vi được cấp.", true, false, "Assignments", "assignments.assignments.update", "Assignments", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "457dcd27-b308-57e7-9734-b4d8c716ee03", "submit", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép submit attempts trong phạm vi được cấp.", true, false, "Learning", "learning.attempts.submit", "Attempts", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "49b02421-4c49-5210-b51f-100a3adbd835", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read center trong phạm vi được cấp.", true, false, "Dashboards", "dashboards.center.read", "Center", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "4d043d04-6dfd-5035-a9df-2d1f928828e5", "publish", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép publish curriculums trong phạm vi được cấp.", true, true, "Curriculum", "curriculum.curriculums.publish", "Curriculums", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "4f574f5c-92de-57ed-95c0-b0111b7c98c6", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create classes trong phạm vi được cấp.", true, true, "Organization", "organization.classes.create", "Classes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "50333eb2-ce8d-5af9-ade6-47d56019d4c7", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update questions trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.questions.update", "Questions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5aa2cf5c-0a7d-51e2-a8b2-d2eabddb9ed5", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own student trong phạm vi được cấp.", true, false, "Twin", "twin.student.read_own", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5b4f74bf-79a3-51cd-bac7-aa343aca36b7", "manage_permissions", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép manage_permissions roles trong phạm vi được cấp.", true, true, "Authorization", "authorization.roles.manage_permissions", "Roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5c1c2f87-57d8-5f75-85b6-340355e2f1a1", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update students trong phạm vi được cấp.", true, true, "Organization", "organization.students.update", "Students", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5d0570f6-a3b5-5e08-b7df-4c83589479e7", "close", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép close assignments trong phạm vi được cấp.", true, true, "Assignments", "assignments.assignments.close", "Assignments", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "6091c47a-f377-5b09-8011-eab471953cd7", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete nodes trong phạm vi được cấp.", true, true, "Knowledge", "knowledge.nodes.delete", "Nodes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "60fe0d1f-3210-580d-a6cc-4f0e985c6830", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete teachers trong phạm vi được cấp.", true, true, "Organization", "organization.teachers.delete", "Teachers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "613fe1ce-ba83-51aa-a41b-9a2f50f40a4f", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update subjects trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.subjects.update", "Subjects", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "62b1d660-2d38-5ff5-bd55-11dd9034426a", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read classes trong phạm vi được cấp.", true, false, "Organization", "organization.classes.read", "Classes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "6494771f-ee89-51f4-9ece-6392f9323ffd", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read assignments trong phạm vi được cấp.", true, false, "Assignments", "assignments.assignments.read", "Assignments", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "6f5ccf7e-38c6-56b6-b504-d8d7c90e2d18", "publish", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép publish questions trong phạm vi được cấp.", true, true, "Curriculum", "curriculum.questions.publish", "Questions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "7100bf0a-ec79-5019-86a0-7fd01f08f843", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update edges trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.edges.update", "Edges", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "73bfa2d1-fb32-5139-b9d0-700a4dc38464", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read nodes trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.nodes.read", "Nodes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "771d3667-a37e-5667-9235-cb4de6543c8b", "read_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_scoped teacher trong phạm vi được cấp.", true, false, "Dashboards", "dashboards.teacher.read_scoped", "Teacher", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "7d7bc1cb-e190-52f0-925e-382b9d35e621", "read_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_scoped attempts trong phạm vi được cấp.", true, false, "Learning", "learning.attempts.read_scoped", "Attempts", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "7f2302ce-ca65-5ffc-a10e-b6b49633886d", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update curriculums trong phạm vi được cấp.", true, false, "Curriculum", "curriculum.curriculums.update", "Curriculums", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "7f4ca559-5f64-5c9b-841d-d9bf07d79eb3", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create assignments trong phạm vi được cấp.", true, false, "Assignments", "assignments.assignments.create", "Assignments", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "874e961b-2516-559c-89ce-79fbcc5d2ee2", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update roles trong phạm vi được cấp.", true, true, "Authorization", "authorization.roles.update", "Roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "88e69333-95b9-57c6-8246-f93fdc7b5279", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own attempts trong phạm vi được cấp.", true, false, "Learning", "learning.attempts.read_own", "Attempts", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "895da0ab-70a0-5386-aa49-115418748c00", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update nodes trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.nodes.update", "Nodes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8a6afa54-6728-51be-ad8c-3bcbf500c797", "read_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_scoped student trong phạm vi được cấp.", true, false, "Twin", "twin.student.read_scoped", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8b7e4b80-5038-5653-ab99-b80eef434ed0", "override", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép override reasoning trong phạm vi được cấp.", true, true, "Twin", "twin.reasoning.override", "Reasoning", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "93104ecc-6cc8-53c7-ba1c-a89c2a17c7b0", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create edges trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.edges.create", "Edges", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "9bd8cecb-4c5d-51e9-958f-8ce985a00563", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete subjects trong phạm vi được cấp.", true, true, "Knowledge", "knowledge.subjects.delete", "Subjects", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "a5101135-e503-5bd5-9168-eac4ac1de14a", "publish", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép publish assignments trong phạm vi được cấp.", true, true, "Assignments", "assignments.assignments.publish", "Assignments", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "b2dff53d-6f6c-5ad8-a19b-f53faee7fbc6", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update classes trong phạm vi được cấp.", true, true, "Organization", "organization.classes.update", "Classes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "ba1486f8-9c47-57f4-ab91-b3d2f8389143", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete edges trong phạm vi được cấp.", true, true, "Knowledge", "knowledge.edges.delete", "Edges", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "bc176f15-b117-5061-865d-bd6484edcd10", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read user_roles trong phạm vi được cấp.", true, false, "Authorization", "authorization.user_roles.read", "User_roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "c18625a1-d9e0-5bd0-b39f-66d581ab8736", "update", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update teachers trong phạm vi được cấp.", true, true, "Organization", "organization.teachers.update", "Teachers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "c3206a6b-bdde-561b-bb39-2f99171e7e13", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read edges trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.edges.read", "Edges", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "c5d2ce2a-dfa4-5fb9-8d63-824d9e3cec24", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read students trong phạm vi được cấp.", true, false, "Organization", "organization.students.read", "Students", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "d094a0c5-c764-5a72-9712-d86e51e3cc0a", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create roles trong phạm vi được cấp.", true, true, "Authorization", "authorization.roles.create", "Roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "d4e56eeb-715d-510f-8c53-4f55eabc34f1", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create teachers trong phạm vi được cấp.", true, true, "Organization", "organization.teachers.create", "Teachers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "d723d209-9817-5469-8ad0-7ccd0dd12b52", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create subjects trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.subjects.create", "Subjects", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "dcefec39-829e-58bc-9c40-09e616ab4dc8", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read teachers trong phạm vi được cấp.", true, false, "Organization", "organization.teachers.read", "Teachers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "e3af8fce-c73e-5ede-803b-cada5b876e7d", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete students trong phạm vi được cấp.", true, true, "Organization", "organization.students.delete", "Students", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "e80be4b0-191b-5fd6-9b74-0fcf5eccb3ff", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read subjects trong phạm vi được cấp.", true, false, "Knowledge", "knowledge.subjects.read", "Subjects", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "f0325a2f-51f1-56e3-9825-176ae1f1d62b", "create", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép create students trong phạm vi được cấp.", true, true, "Organization", "organization.students.create", "Students", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "f2f2211c-3699-57c9-9bcd-9fdf81f4b52c", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own student trong phạm vi được cấp.", true, false, "Dashboards", "dashboards.student.read_own", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "faed18c5-e620-5f59-baa5-013d69727221", "archive", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép archive roles trong phạm vi được cấp.", true, true, "Authorization", "authorization.roles.archive", "Roles", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "0107fed3-60db-5dec-91e4-19264b9f6cc4", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "0bf8a5ff-bf1e-5d4d-a0b0-eea80249efbe", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "1bea1cdd-98a7-5f16-9d82-26031c6bf82a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "1bea1cdd-98a7-5f16-9d82-26031c6bf82a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "1d553e2a-603c-52fb-90b8-23abaf838e30", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "1df2878a-2f15-5d98-aff5-985f9bc8d20c", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "1df2878a-2f15-5d98-aff5-985f9bc8d20c", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "20bfe8fa-9a9b-57a2-a61e-434839ccacc9", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "20bfe8fa-9a9b-57a2-a61e-434839ccacc9", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "265a0526-9217-5fac-a735-b1ff1b522621", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "273babe7-569d-56c9-812c-417128fcbf49", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "273babe7-569d-56c9-812c-417128fcbf49", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "2a640d34-c437-564f-9abe-0b4d3f04b7bb", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "2aff255a-56fe-5c97-a598-693dc5fa4cf1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "2aff255a-56fe-5c97-a598-693dc5fa4cf1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "2d7350d0-6faa-54ff-9ac1-f770e17b8113", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "31a15ec9-7e7b-5c1c-b11e-95d9933f46dc", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "31a15ec9-7e7b-5c1c-b11e-95d9933f46dc", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "365216e5-c1b9-5ebf-9f60-e0dbe1562072", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "365216e5-c1b9-5ebf-9f60-e0dbe1562072", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "418b6ea7-7dfd-5a9e-a8ad-7e95633e70b9", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "423a2eda-b505-5984-85d4-02d2e2fff834", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "423a2eda-b505-5984-85d4-02d2e2fff834", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "457dcd27-b308-57e7-9734-b4d8c716ee03", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "49b02421-4c49-5210-b51f-100a3adbd835", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "4d043d04-6dfd-5035-a9df-2d1f928828e5", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "4d043d04-6dfd-5035-a9df-2d1f928828e5", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "4f574f5c-92de-57ed-95c0-b0111b7c98c6", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "50333eb2-ce8d-5af9-ade6-47d56019d4c7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "50333eb2-ce8d-5af9-ade6-47d56019d4c7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "5aa2cf5c-0a7d-51e2-a8b2-d2eabddb9ed5", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "5b4f74bf-79a3-51cd-bac7-aa343aca36b7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "5c1c2f87-57d8-5f75-85b6-340355e2f1a1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "5c1c2f87-57d8-5f75-85b6-340355e2f1a1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "5d0570f6-a3b5-5e08-b7df-4c83589479e7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "5d0570f6-a3b5-5e08-b7df-4c83589479e7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "6091c47a-f377-5b09-8011-eab471953cd7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "60fe0d1f-3210-580d-a6cc-4f0e985c6830", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "613fe1ce-ba83-51aa-a41b-9a2f50f40a4f", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "613fe1ce-ba83-51aa-a41b-9a2f50f40a4f", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "62b1d660-2d38-5ff5-bd55-11dd9034426a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "62b1d660-2d38-5ff5-bd55-11dd9034426a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "6494771f-ee89-51f4-9ece-6392f9323ffd", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "6494771f-ee89-51f4-9ece-6392f9323ffd", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "6494771f-ee89-51f4-9ece-6392f9323ffd", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "6f5ccf7e-38c6-56b6-b504-d8d7c90e2d18", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "6f5ccf7e-38c6-56b6-b504-d8d7c90e2d18", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "7100bf0a-ec79-5019-86a0-7fd01f08f843", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "7100bf0a-ec79-5019-86a0-7fd01f08f843", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "73bfa2d1-fb32-5139-b9d0-700a4dc38464", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "73bfa2d1-fb32-5139-b9d0-700a4dc38464", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "73bfa2d1-fb32-5139-b9d0-700a4dc38464", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "771d3667-a37e-5667-9235-cb4de6543c8b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "7d7bc1cb-e190-52f0-925e-382b9d35e621", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "7d7bc1cb-e190-52f0-925e-382b9d35e621", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "7f2302ce-ca65-5ffc-a10e-b6b49633886d", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "7f2302ce-ca65-5ffc-a10e-b6b49633886d", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "7f4ca559-5f64-5c9b-841d-d9bf07d79eb3", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "7f4ca559-5f64-5c9b-841d-d9bf07d79eb3", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "874e961b-2516-559c-89ce-79fbcc5d2ee2", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "88e69333-95b9-57c6-8246-f93fdc7b5279", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "895da0ab-70a0-5386-aa49-115418748c00", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "895da0ab-70a0-5386-aa49-115418748c00", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "8a6afa54-6728-51be-ad8c-3bcbf500c797", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "8a6afa54-6728-51be-ad8c-3bcbf500c797", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "8b7e4b80-5038-5653-ab99-b80eef434ed0", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "8b7e4b80-5038-5653-ab99-b80eef434ed0", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "93104ecc-6cc8-53c7-ba1c-a89c2a17c7b0", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "93104ecc-6cc8-53c7-ba1c-a89c2a17c7b0", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "9bd8cecb-4c5d-51e9-958f-8ce985a00563", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "a5101135-e503-5bd5-9168-eac4ac1de14a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "a5101135-e503-5bd5-9168-eac4ac1de14a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "b2dff53d-6f6c-5ad8-a19b-f53faee7fbc6", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "ba1486f8-9c47-57f4-ab91-b3d2f8389143", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "ba1486f8-9c47-57f4-ab91-b3d2f8389143", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "bc176f15-b117-5061-865d-bd6484edcd10", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "c18625a1-d9e0-5bd0-b39f-66d581ab8736", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "c3206a6b-bdde-561b-bb39-2f99171e7e13", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "c3206a6b-bdde-561b-bb39-2f99171e7e13", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "c3206a6b-bdde-561b-bb39-2f99171e7e13", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "c5d2ce2a-dfa4-5fb9-8d63-824d9e3cec24", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "c5d2ce2a-dfa4-5fb9-8d63-824d9e3cec24", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "c5d2ce2a-dfa4-5fb9-8d63-824d9e3cec24", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "d094a0c5-c764-5a72-9712-d86e51e3cc0a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "d4e56eeb-715d-510f-8c53-4f55eabc34f1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "d723d209-9817-5469-8ad0-7ccd0dd12b52", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "d723d209-9817-5469-8ad0-7ccd0dd12b52", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "dcefec39-829e-58bc-9c40-09e616ab4dc8", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "dcefec39-829e-58bc-9c40-09e616ab4dc8", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "e3af8fce-c73e-5ede-803b-cada5b876e7d", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "e80be4b0-191b-5fd6-9b74-0fcf5eccb3ff", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "e80be4b0-191b-5fd6-9b74-0fcf5eccb3ff", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "e80be4b0-191b-5fd6-9b74-0fcf5eccb3ff", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "f0325a2f-51f1-56e3-9825-176ae1f1d62b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "f0325a2f-51f1-56e3-9825-176ae1f1d62b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "f2f2211c-3699-57c9-9bcd-9fdf81f4b52c", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "faed18c5-e620-5f59-baa5-013d69727221", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_audit_logs_center_actor_created",
                table: "authorization_audit_logs",
                columns: new[] { "center_id", "actor_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_audit_logs_center_created_action",
                table: "authorization_audit_logs",
                columns: new[] { "center_id", "created_at", "action_type" });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_audit_logs_center_target_created",
                table: "authorization_audit_logs",
                columns: new[] { "center_id", "target_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_permission_account_types_account_type_permission_id",
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id" });

            migrationBuilder.CreateIndex(
                name: "ix_permissions_module_resource_action_status",
                table: "permissions",
                columns: new[] { "module_name", "resource_name", "action_name", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_permissions_permission_code",
                table: "permissions",
                column: "permission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_center_granted_by_user",
                table: "role_permissions",
                columns: new[] { "center_id", "granted_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_center_permission_role",
                table: "role_permissions",
                columns: new[] { "center_id", "permission_id", "role_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_center_role_account_type",
                table: "role_permissions",
                columns: new[] { "center_id", "role_id", "account_type" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_account_type",
                table: "role_permissions",
                columns: new[] { "permission_id", "account_type" });

            migrationBuilder.CreateIndex(
                name: "ix_roles_center_account_type_status_role_name",
                table: "roles",
                columns: new[] { "center_id", "account_type", "status", "role_name" });

            migrationBuilder.CreateIndex(
                name: "ux_roles_center_id_role_code",
                table: "roles",
                columns: new[] { "center_id", "role_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_assigned_by_user",
                table: "user_roles",
                columns: new[] { "center_id", "assigned_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_revoked_by_user",
                table: "user_roles",
                columns: new[] { "center_id", "revoked_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_role_account_type",
                table: "user_roles",
                columns: new[] { "center_id", "role_id", "account_type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_role_status_user",
                table: "user_roles",
                columns: new[] { "center_id", "role_id", "status", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_user_account_type",
                table: "user_roles",
                columns: new[] { "center_id", "user_id", "account_type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_center_user_status",
                table: "user_roles",
                columns: new[] { "center_id", "user_id", "status" });

            migrationBuilder.Sql(
                """
                INSERT INTO roles (
                    role_id, role_code, role_name, account_type, description,
                    is_system_role, status, center_id, created_at, created_by,
                    updated_at, updated_by, is_deleted, deleted_at, deleted_by, row_version)
                SELECT
                    LOWER(CONCAT(
                        SUBSTRING(seed.hash_value, 1, 8), '-',
                        SUBSTRING(seed.hash_value, 9, 4), '-',
                        SUBSTRING(seed.hash_value, 13, 4), '-',
                        SUBSTRING(seed.hash_value, 17, 4), '-',
                        SUBSTRING(seed.hash_value, 21, 12))),
                    CONCAT('SYSTEM_', UPPER(seed.account_type)),
                    CASE seed.account_type
                        WHEN 'Student' THEN 'Học viên hệ thống'
                        WHEN 'Teacher' THEN 'Giáo viên hệ thống'
                        ELSE 'Quản trị trung tâm'
                    END,
                    seed.account_type,
                    'Vai trò hệ thống được tạo khi chuyển đổi sang phân quyền động.',
                    1, 'Active', seed.center_id,
                    '2026-09-09 00:00:00.000000', NULL,
                    '2026-09-09 00:00:00.000000', NULL,
                    0, NULL, NULL, 1
                FROM (
                    SELECT c.center_id, account_types.account_type,
                           MD5(CONCAT('edutwin:system-role:v1:', c.center_id, ':', account_types.account_type)) AS hash_value
                    FROM centers c
                    CROSS JOIN (
                        SELECT 'Student' AS account_type
                        UNION ALL SELECT 'Teacher'
                        UNION ALL SELECT 'CenterManager'
                    ) account_types
                ) seed;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO role_permissions (
                    center_id, role_id, permission_id, account_type, granted_at, granted_by_user_id)
                SELECT
                    r.center_id, r.role_id, pat.permission_id, r.account_type,
                    '2026-09-09 00:00:00.000000', actor.actor_user_id
                FROM roles r
                INNER JOIN permission_account_types pat ON pat.account_type = r.account_type
                INNER JOIN (
                    SELECT center_id, MIN(user_id) AS actor_user_id
                    FROM users
                    WHERE role_name = 'CenterManager' AND status = 'Active' AND is_deleted = 0
                    GROUP BY center_id
                ) actor ON actor.center_id = r.center_id
                WHERE r.is_system_role = 1 AND r.status = 'Active' AND r.is_deleted = 0;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO user_roles (
                    center_id, user_id, role_id, account_type, status,
                    assigned_at, assigned_by_user_id, revoked_at, revoked_by_user_id,
                    revoke_reason, row_version)
                SELECT
                    u.center_id, u.user_id, r.role_id, u.role_name, 'Active',
                    '2026-09-09 00:00:00.000000', actor.actor_user_id,
                    NULL, NULL, NULL, 1
                FROM users u
                INNER JOIN roles r
                    ON r.center_id = u.center_id
                    AND r.account_type = u.role_name
                    AND r.is_system_role = 1
                    AND r.status = 'Active'
                    AND r.is_deleted = 0
                INNER JOIN (
                    SELECT center_id, MIN(user_id) AS actor_user_id
                    FROM users
                    WHERE role_name = 'CenterManager' AND status = 'Active' AND is_deleted = 0
                    GROUP BY center_id
                ) actor ON actor.center_id = u.center_id
                WHERE u.is_deleted = 0;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO authorization_audit_logs (
                    center_id, actor_user_id, action_type, target_type, target_id,
                    target_user_id, permission_code, before_data, after_data,
                    reason, trace_id, created_at, created_by)
                SELECT
                    c.center_id, NULL, 'AuthorizationBootstrap', 'Center', c.center_id,
                    NULL, NULL, NULL,
                    JSON_OBJECT('catalogVersion', 'v1', 'systemRoles', 3),
                    'Khởi tạo phân quyền động từ account type hiện hữu.',
                    'migration:AddDynamicAuthorization',
                    '2026-09-09 00:00:00.000000', NULL
                FROM centers c;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_audit_logs");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "permission_account_types");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_users_center_id_user_id_role_name",
                table: "users");
        }
    }
}
