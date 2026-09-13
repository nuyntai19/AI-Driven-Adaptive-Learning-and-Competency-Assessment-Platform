using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptAttachmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attempt_attachments",
                columns: table => new
                {
                    attachment_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    file_name = table.Column<string>(type: "varchar(255)", nullable: false),
                    content_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    storage_key = table.Column<string>(type: "varchar(512)", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    upload_nonce = table.Column<string>(type: "varchar(64)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempt_attachments", x => x.attachment_id);
                    table.CheckConstraint("ck_attempt_attachments_content_type", "`content_type` = 'image/png'");
                    table.CheckConstraint("ck_attempt_attachments_file_size_bytes", "`file_size_bytes` >= 1 AND `file_size_bytes` <= 5242880");
                    table.ForeignKey(
                        name: "fk_attempt_attachments_attempts",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_attempt_attachments_center_id_attempt_id",
                table: "attempt_attachments",
                columns: new[] { "center_id", "attempt_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attempt_attachments_center_id_storage_key",
                table: "attempt_attachments",
                columns: new[] { "center_id", "storage_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attempt_attachments_center_id_upload_nonce",
                table: "attempt_attachments",
                columns: new[] { "center_id", "upload_nonce" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempt_attachments");
        }
    }
}
