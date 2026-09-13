using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public sealed class AttemptAttachmentConfiguration : IEntityTypeConfiguration<AttemptAttachment>
{
    public void Configure(EntityTypeBuilder<AttemptAttachment> builder)
    {
        builder.ToTable("attempt_attachments");
        builder.HasKey(attachment => attachment.AttachmentId).HasName("pk_attempt_attachments");

        builder.HasIndex(attachment => new { attachment.CenterId, attachment.AttemptId })
            .IsUnique()
            .HasDatabaseName("ux_attempt_attachments_center_id_attempt_id");
        builder.HasIndex(attachment => new { attachment.CenterId, attachment.UploadNonce })
            .IsUnique()
            .HasDatabaseName("ux_attempt_attachments_center_id_upload_nonce");
        builder.HasIndex(attachment => new { attachment.CenterId, attachment.StorageKey })
            .IsUnique()
            .HasDatabaseName("ux_attempt_attachments_center_id_storage_key");

        builder.Property(attachment => attachment.AttachmentId)
            .HasColumnName("attachment_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();
        builder.Property(attachment => attachment.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(attachment => attachment.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(attachment => attachment.FileName).HasColumnName("file_name").HasColumnType("varchar(255)").IsRequired();
        builder.Property(attachment => attachment.ContentType).HasColumnName("content_type").HasColumnType("varchar(64)").IsRequired();
        builder.Property(attachment => attachment.StorageKey).HasColumnName("storage_key").HasColumnType("varchar(512)").IsRequired();
        builder.Property(attachment => attachment.FileSizeBytes).HasColumnName("file_size_bytes").HasColumnType("bigint").IsRequired();
        builder.Property(attachment => attachment.UploadNonce).HasColumnName("upload_nonce").HasColumnType("varchar(64)").IsRequired();
        builder.Property(attachment => attachment.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(attachment => attachment.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_attempt_attachments_file_size_bytes", "`file_size_bytes` >= 1 AND `file_size_bytes` <= 5242880");
            table.HasCheckConstraint("ck_attempt_attachments_content_type", "`content_type` = 'image/png'");
        });

        builder.HasOne(attachment => attachment.Attempt)
            .WithOne(attempt => attempt.Attachment)
            .HasForeignKey<AttemptAttachment>(attachment => new { attachment.CenterId, attachment.AttemptId })
            .HasPrincipalKey<Attempt>(attempt => new { attempt.CenterId, attempt.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attempt_attachments_attempts");
    }
}
