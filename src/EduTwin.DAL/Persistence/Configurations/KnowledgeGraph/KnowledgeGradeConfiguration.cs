using EduTwin.DAL.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.KnowledgeGraph;

public class KnowledgeGraphVersionConfiguration : IEntityTypeConfiguration<KnowledgeGraphVersion>
{
    public void Configure(EntityTypeBuilder<KnowledgeGraphVersion> b)
    {
        b.ToTable("knowledge_graph_versions", t => {
            t.HasCheckConstraint("ck_graph_versions_grade", "grade_level BETWEEN 10 AND 12");
            t.HasCheckConstraint("ck_graph_versions_status", "status IN ('Draft','Published','Archived')");
            t.HasCheckConstraint("ck_graph_versions_current", "is_current=0 OR (status='Published' AND is_legacy=0)");
        });
        b.HasKey(v => v.GraphVersionId);
        b.HasAlternateKey(v => new { v.CenterId, v.GraphVersionId });
        b.Property(v => v.GraphVersionId).HasColumnName("graph_version_id").HasColumnType("varchar(36)");
        b.Property(v => v.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(v => v.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)");
        b.Property(v => v.GradeLevel).HasColumnName("grade_level").HasColumnType("tinyint unsigned");
        b.Property(v => v.Status).HasColumnName("status").HasColumnType("varchar(16)");
        b.Property(v => v.IsCurrent).HasColumnName("is_current").HasColumnType("tinyint(1)");
        b.Property(v => v.IsLegacy).HasColumnName("is_legacy").HasColumnType("tinyint(1)");
        b.Property(v => v.Title).HasColumnName("title").HasColumnType("varchar(250)");
        b.Property(v => v.SourceGraphVersionId).HasColumnName("source_graph_version_id").HasColumnType("varchar(36)");
        b.Property(v => v.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(v => v.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.Property(v => v.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        b.Property(v => v.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        b.Property(v => v.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").IsConcurrencyToken().HasDefaultValue(1ul);
        // MySQL permits many NULLs but only one active library version per tenant/subject/grade.
        b.Property<byte?>("CurrentGrade").HasColumnName("current_grade").HasColumnType("tinyint unsigned")
            .HasComputedColumnSql("CASE WHEN is_current=1 THEN grade_level ELSE NULL END", stored: true);
        b.HasIndex("CenterId", "SubjectId", "CurrentGrade").IsUnique().HasDatabaseName("ux_graph_versions_current_grade");
        b.HasIndex(v => new { v.CenterId, v.SubjectId, v.GradeLevel, v.Status });
        b.HasOne<EduTwin.DAL.Organization.Subject>().WithMany().HasForeignKey(v => new { v.CenterId, v.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeGraphVersion>().WithMany().HasForeignKey(v => new { v.CenterId, v.SourceGraphVersionId })
            .HasPrincipalKey(v => new { v.CenterId, v.GraphVersionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public class KnowledgeNodeGradeConfiguration : IEntityTypeConfiguration<KnowledgeNodeGrade>
{
    public void Configure(EntityTypeBuilder<KnowledgeNodeGrade> b)
    {
        b.ToTable("knowledge_node_grades", t => t.HasCheckConstraint("ck_node_grades_parent", "parent_node_id IS NULL OR parent_node_id<>node_id"));
        b.HasKey(n => new { n.CenterId, n.GraphVersionId, n.NodeId });
        b.Property(n => n.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(n => n.GraphVersionId).HasColumnName("graph_version_id").HasColumnType("varchar(36)");
        b.Property(n => n.NodeId).HasColumnName("node_id").HasColumnType("bigint unsigned");
        b.Property(n => n.ParentNodeId).HasColumnName("parent_node_id").HasColumnType("bigint unsigned");
        b.Property(n => n.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(n => n.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.HasOne<KnowledgeGraphVersion>().WithMany().HasForeignKey(n => new { n.CenterId, n.GraphVersionId })
            .HasPrincipalKey(v => new { v.CenterId, v.GraphVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeNode>().WithMany().HasForeignKey(n => new { n.CenterId, n.NodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeNodeGrade>().WithMany().HasForeignKey(n => new { n.CenterId, n.GraphVersionId, n.ParentNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.GraphVersionId, n.NodeId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(n => new { n.CenterId, n.NodeId, n.GraphVersionId });
    }
}

public class KnowledgeEdgeGradeConfiguration : IEntityTypeConfiguration<KnowledgeEdgeGrade>
{
    public void Configure(EntityTypeBuilder<KnowledgeEdgeGrade> b)
    {
        b.ToTable("knowledge_edge_grades", t => {
            t.HasCheckConstraint("ck_edge_grades_weight", "weight BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_edge_grades_relation", "relation_type IN ('PrerequisiteOf','RelatedTo','PartOf','CausesErrorIn')");
        });
        b.HasKey(e => new { e.CenterId, e.GraphVersionId, e.EdgeId });
        b.Property(e => e.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(e => e.GraphVersionId).HasColumnName("graph_version_id").HasColumnType("varchar(36)");
        b.Property(e => e.EdgeId).HasColumnName("edge_id").HasColumnType("bigint unsigned");
        b.Property(e => e.RelationType).HasColumnName("relation_type").HasColumnType("varchar(32)").HasConversion<string>();
        b.Property(e => e.Weight).HasColumnName("weight").HasColumnType("decimal(5,2)");
        b.Property(e => e.SourceGraphVersionId).HasColumnName("source_graph_version_id").HasColumnType("varchar(36)");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.HasOne<KnowledgeGraphVersion>().WithMany().HasForeignKey(e => new { e.CenterId, e.GraphVersionId })
            .HasPrincipalKey(v => new { v.CenterId, v.GraphVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeGraphVersion>().WithMany().HasForeignKey(e => new { e.CenterId, e.SourceGraphVersionId })
            .HasPrincipalKey(v => new { v.CenterId, v.GraphVersionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<KnowledgeEdge>().WithMany().HasForeignKey(e => new { e.CenterId, e.EdgeId })
            .HasPrincipalKey(e => new { e.CenterId, e.EdgeId }).OnDelete(DeleteBehavior.Restrict);
    }
}
