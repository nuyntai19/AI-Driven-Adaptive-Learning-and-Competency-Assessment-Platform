using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EduTwin.DAL.Persistence.Migrations;

[DbContext(typeof(EduTwinDbContext))]
[Migration("20261009103000_HardenAcademicLifecycleDependencies")]
public sealed class HardenAcademicLifecycleDependencies : Migration
{
    private static string LiveUsage(string curriculumFilter) => $"""
        EXISTS (SELECT 1 FROM classes c JOIN curriculums cu ON cu.center_id=c.center_id AND cu.subject_id=c.subject_id
          WHERE c.center_id=NEW.center_id AND c.status='Active' AND c.learning_scope='Current' AND c.is_deleted=0
          AND cu.is_deleted=0 AND {curriculumFilter}
          AND (EXISTS (SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=c.center_id AND a.class_id=c.class_id
                 AND a.curriculum_id=cu.curriculum_id AND a.ended_at IS NULL)
            OR (cu.review_status='Published' AND NOT EXISTS (SELECT 1 FROM class_curriculum_applications h WHERE h.center_id=c.center_id AND h.class_id=c.class_id)
                AND EXISTS (SELECT 1 FROM curriculum_classes cc WHERE cc.center_id=c.center_id AND cc.class_id=c.class_id AND cc.curriculum_id=cu.curriculum_id))))
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No data reclassification or deletion. Guard future writes only.
        migrationBuilder.Sql($"""
            CREATE TRIGGER tr_curriculum_archive_live_usage BEFORE UPDATE ON curriculums FOR EACH ROW
            BEGIN
              IF OLD.review_status<>'Archived' AND NEW.review_status='Archived' AND {LiveUsage("cu.curriculum_id=OLD.curriculum_id")}
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: curriculum still used by active classes'; END IF;
            END;
            """);
        var nodeUse = LiveUsage("EXISTS (SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=cu.center_id AND cn.curriculum_id=cu.curriculum_id AND cn.node_id=OLD.node_id)");
        migrationBuilder.Sql($"""
            CREATE TRIGGER tr_knowledge_node_lifecycle BEFORE UPDATE ON knowledge_nodes FOR EACH ROW
            BEGIN
              IF (NOT(NEW.node_name <=> OLD.node_name) OR NOT(NEW.description <=> OLD.description) OR NOT(NEW.parent_node_id <=> OLD.parent_node_id)
                  OR NOT(NEW.subject_id <=> OLD.subject_id) OR NOT(NEW.node_code <=> OLD.node_code) OR NOT(NEW.node_type <=> OLD.node_type))
                AND EXISTS (SELECT 1 FROM curriculum_nodes cn JOIN curriculums cu ON cu.center_id=cn.center_id AND cu.curriculum_id=cn.curriculum_id
                  WHERE cn.center_id=OLD.center_id AND cn.node_id=OLD.node_id AND cu.review_status IN ('Published','Archived'))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published curriculum node content is immutable'; END IF;
              IF OLD.is_active=1 AND NEW.is_active=0 AND ({nodeUse}
                OR EXISTS (SELECT 1 FROM knowledge_nodes child WHERE child.center_id=OLD.center_id AND child.parent_node_id=OLD.node_id AND child.is_deleted=0 AND child.is_active=1)
                OR EXISTS (SELECT 1 FROM questions q WHERE q.center_id=OLD.center_id AND q.status='Active' AND q.is_deleted=0 AND
                  (q.primary_topic_node_id=OLD.node_id OR EXISTS (SELECT 1 FROM question_knowledge_nodes qn WHERE qn.center_id=q.center_id AND qn.question_id=q.question_id AND qn.node_id=OLD.node_id)))
                OR EXISTS (SELECT 1 FROM learning_path_items i JOIN learning_paths p ON p.center_id=i.center_id AND p.learning_path_id=i.learning_path_id
                  WHERE i.center_id=OLD.center_id AND i.topic_node_id=OLD.node_id AND i.is_deleted=0 AND p.is_deleted=0 AND p.status='Active'
                  AND (NOT EXISTS (SELECT 1 FROM class_students m JOIN classes c ON c.center_id=m.center_id AND c.class_id=m.class_id
                    WHERE m.center_id=p.center_id AND m.student_id=p.student_id AND c.subject_id=p.subject_id)
                    OR EXISTS (SELECT 1 FROM class_students m JOIN classes c ON c.center_id=m.center_id AND c.class_id=m.class_id
                      WHERE m.center_id=p.center_id AND m.student_id=p.student_id AND c.subject_id=p.subject_id AND m.status='Active'
                        AND c.status='Active' AND c.learning_scope='Current' AND c.is_deleted=0))))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: active node still in use'; END IF;
              IF OLD.is_deleted=0 AND NEW.is_deleted=1 AND (
                EXISTS (SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=OLD.center_id AND cn.node_id=OLD.node_id)
                OR EXISTS (SELECT 1 FROM question_knowledge_nodes qn WHERE qn.center_id=OLD.center_id AND qn.node_id=OLD.node_id)
                OR EXISTS (SELECT 1 FROM questions q WHERE q.center_id=OLD.center_id AND q.primary_topic_node_id=OLD.node_id AND q.is_deleted=0)
                OR EXISTS (SELECT 1 FROM knowledge_edges e WHERE e.center_id=OLD.center_id AND e.is_deleted=0 AND (e.source_node_id=OLD.node_id OR e.target_node_id=OLD.node_id))
                OR EXISTS (SELECT 1 FROM knowledge_nodes child WHERE child.center_id=OLD.center_id AND child.parent_node_id=OLD.node_id AND child.is_deleted=0)
                OR EXISTS (SELECT 1 FROM knowledge_twins k WHERE k.center_id=OLD.center_id AND k.topic_node_id=OLD.node_id AND k.is_deleted=0)
                OR EXISTS (SELECT 1 FROM twin_update_history h WHERE h.center_id=OLD.center_id AND h.topic_node_id=OLD.node_id)
                OR EXISTS (SELECT 1 FROM learning_path_items i WHERE i.center_id=OLD.center_id AND i.topic_node_id=OLD.node_id AND i.is_deleted=0)
                OR EXISTS (SELECT 1 FROM recommendations r WHERE r.center_id=OLD.center_id AND r.topic_node_id=OLD.node_id AND r.is_deleted=0)
                OR EXISTS (SELECT 1 FROM reasoning_analyses a WHERE a.center_id=OLD.center_id AND
                  (JSON_CONTAINS(a.root_cause_node_ids,CAST(OLD.node_id AS CHAR),'$')=1
                    OR JSON_CONTAINS(a.root_cause_node_ids,JSON_QUOTE(CAST(OLD.node_id AS CHAR)),'$')=1)))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: node still referenced'; END IF;
            END;
            """);
        var edgeUse = LiveUsage("EXISTS (SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=cu.center_id AND cn.curriculum_id=cu.curriculum_id AND cn.node_id IN (NEW.source_node_id,NEW.target_node_id))");
        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            var changed = operation == "INSERT" ? "TRUE" : "(NOT(NEW.weight <=> OLD.weight) OR NOT(NEW.is_deleted <=> OLD.is_deleted) OR NOT(NEW.source_node_id <=> OLD.source_node_id) OR NOT(NEW.target_node_id <=> OLD.target_node_id) OR NOT(NEW.relation_type <=> OLD.relation_type))";
            migrationBuilder.Sql($"""
                CREATE TRIGGER tr_knowledge_edge_lifecycle_{operation.ToLowerInvariant()} BEFORE {operation} ON knowledge_edges FOR EACH ROW
                BEGIN
                  IF NEW.is_deleted=0 AND NOT EXISTS (SELECT 1 FROM knowledge_nodes s JOIN knowledge_nodes t ON t.center_id=s.center_id AND t.subject_id=s.subject_id
                    WHERE s.center_id=NEW.center_id AND s.node_id=NEW.source_node_id AND t.node_id=NEW.target_node_id
                      AND s.subject_id=NEW.subject_id AND s.is_deleted=0 AND t.is_deleted=0 AND s.is_active=1 AND t.is_active=1)
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge endpoints must be active in the same subject'; END IF;
                  IF {changed} AND {edgeUse}
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge still used by active classes'; END IF;
                END;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var trigger in new[] { "tr_curriculum_archive_live_usage", "tr_knowledge_node_lifecycle", "tr_knowledge_edge_lifecycle_insert", "tr_knowledge_edge_lifecycle_update" })
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
    }
}
