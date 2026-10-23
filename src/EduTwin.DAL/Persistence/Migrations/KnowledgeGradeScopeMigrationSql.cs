using Microsoft.EntityFrameworkCore.Migrations;

namespace EduTwin.DAL.Persistence.Migrations;

/// <summary>Data conversion is additive. Existing curriculum topology is a legacy snapshot, never implicit academic approval.</summary>
internal static class KnowledgeGradeScopeMigrationSql
{
    private static string LegacyId(string id) => $"LOWER(CONCAT(SUBSTR(MD5(CONCAT('knowledge-legacy:',{id})),1,8),'-',SUBSTR(MD5(CONCAT('knowledge-legacy:',{id})),9,4),'-',SUBSTR(MD5(CONCAT('knowledge-legacy:',{id})),13,4),'-',SUBSTR(MD5(CONCAT('knowledge-legacy:',{id})),17,4),'-',SUBSTR(MD5(CONCAT('knowledge-legacy:',{id})),21,12)))";

    public static void Backfill(MigrationBuilder m)
    {
        m.Sql($"""
            INSERT INTO knowledge_graph_versions(graph_version_id,center_id,subject_id,grade_level,status,is_current,is_legacy,title,created_at,created_by,updated_at,updated_by,row_version)
            SELECT {LegacyId("c.curriculum_id")},c.center_id,c.subject_id,c.grade_level,'Published',0,1,LEFT(CONCAT('Lịch sử: ',c.title),250),UTC_TIMESTAMP(6),c.teacher_id,UTC_TIMESTAMP(6),c.teacher_id,1
            FROM curriculums c WHERE c.grade_level BETWEEN 10 AND 12;
            """);
        m.Sql($"UPDATE curriculums c SET c.graph_version_id={LegacyId("c.curriculum_id")} WHERE c.grade_level BETWEEN 10 AND 12;");
        m.Sql("""
            INSERT INTO knowledge_node_grades(center_id,graph_version_id,node_id,parent_node_id,created_at,created_by)
            WITH RECURSIVE scoped_nodes(center_id,graph_version_id,node_id) AS (
              SELECT c.center_id,c.graph_version_id,cn.node_id FROM curriculums c JOIN curriculum_nodes cn ON cn.center_id=c.center_id AND cn.curriculum_id=c.curriculum_id
              WHERE c.graph_version_id IS NOT NULL
              UNION DISTINCT
              SELECT scope.center_id,scope.graph_version_id,dependency.required_node FROM scoped_nodes scope
              JOIN (
                SELECT center_id,node_id,parent_node_id AS required_node FROM knowledge_nodes WHERE parent_node_id IS NOT NULL AND is_deleted=0
                UNION SELECT center_id,target_node_id AS node_id,source_node_id AS required_node FROM knowledge_edges WHERE relation_type='PrerequisiteOf' AND is_deleted=0
              ) dependency ON dependency.center_id=scope.center_id AND dependency.node_id=scope.node_id
            )
            SELECT DISTINCT v.center_id,v.graph_version_id,n.node_id,NULL,UTC_TIMESTAMP(6),v.created_by
            FROM scoped_nodes scope JOIN knowledge_graph_versions v ON v.center_id=scope.center_id AND v.graph_version_id=scope.graph_version_id
            JOIN knowledge_nodes n ON n.center_id=v.center_id AND n.subject_id=v.subject_id AND n.node_id=scope.node_id WHERE n.is_deleted=0;
            """);
        m.Sql("""
            UPDATE knowledge_node_grades ng JOIN knowledge_nodes n ON n.center_id=ng.center_id AND n.node_id=ng.node_id
            JOIN knowledge_node_grades p ON p.center_id=ng.center_id AND p.graph_version_id=ng.graph_version_id AND p.node_id=n.parent_node_id
            SET ng.parent_node_id=p.node_id;
            """);
        m.Sql("""
            INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,created_at,created_by)
            SELECT v.center_id,v.graph_version_id,e.edge_id,e.relation_type,e.weight,UTC_TIMESTAMP(6),v.created_by
            FROM knowledge_graph_versions v JOIN knowledge_edges e ON e.center_id=v.center_id AND e.subject_id=v.subject_id
            JOIN knowledge_node_grades s ON s.center_id=v.center_id AND s.graph_version_id=v.graph_version_id AND s.node_id=e.source_node_id
            JOIN knowledge_node_grades t ON t.center_id=v.center_id AND t.graph_version_id=v.graph_version_id AND t.node_id=e.target_node_id
            WHERE v.is_legacy=1 AND e.is_deleted=0;
            """);

        // Explicit user approval, 2026-10-22: docs/verification/KNOWLEDGE-GRADE-PROPOSALS-2026-10-22.md.
        // Match tenant + subject + all node IDs/codes. Unrelated installations are never reclassified by names or partial matches.
        foreach (var centerA in new[] { true, false })
        foreach (var math in new[] { true, false })
        {
            var center = centerA ? "10000000-0000-0000-0000-000000000001" : "20000000-0000-0000-0000-000000000002";
            var subject = centerA ? (math ? "30000000-0000-0000-0000-000000000003" : "40000000-0000-0000-0000-000000000004")
                : (math ? "30000000-0000-0000-0000-000000000009" : "40000000-0000-0000-0000-000000000009");
            var prefix = centerA ? (math ? "e1000000" : "e2000000") : (math ? "e3000000" : "e4000000");
            var nodeBase = centerA ? 10000 : 20000;
            var codes = math ? new[] { "MATH-FUNCTIONS", "MATH-EXP-LOG", "MATH-ANTIDERIVATIVE" } : new[] { "ENG-TENSES", "ENG-RELATIVE-CLAUSES", "ENG-CONTEXT-VOCAB" };
            var expected = string.Join(" OR ", codes.Select((code, i) => $"(n.node_id={nodeBase + (math ? 0 : 3) + i} AND n.node_code='{code}')"));
            var matches = $"(SELECT COUNT(*) FROM knowledge_nodes n WHERE n.center_id='{center}' AND n.subject_id='{subject}' AND n.is_deleted=0 AND n.is_active=1 AND ({expected}))=3";
            foreach (var grade in new[] { 10, 11, 12 })
            {
                var version = $"{prefix}-0000-0000-0000-{grade:000000000000}";
                var nodes = math ? $"n.node_id={nodeBase + grade - 10}" : $"n.node_id IN ({nodeBase + 3},{nodeBase + 4},{nodeBase + 5})";
                m.Sql($"""
                    INSERT INTO knowledge_graph_versions(graph_version_id,center_id,subject_id,grade_level,status,is_current,is_legacy,title,created_at,updated_at,row_version)
                    SELECT '{version}','{center}','{subject}',{grade},'Published',1,0,'Phân loại đã duyệt 22/10/2026',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),1 WHERE {matches};
                    """);
                m.Sql($"""
                    INSERT INTO knowledge_node_grades(center_id,graph_version_id,node_id,parent_node_id,created_at)
                    SELECT n.center_id,v.graph_version_id,n.node_id,NULL,UTC_TIMESTAMP(6)
                    FROM knowledge_nodes n JOIN knowledge_graph_versions v ON v.center_id=n.center_id AND v.subject_id=n.subject_id
                    WHERE v.graph_version_id='{version}' AND n.center_id='{center}' AND {nodes};
                    """);
                if (!math)
                    m.Sql($"""
                        INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,created_at)
                        SELECT e.center_id,v.graph_version_id,e.edge_id,e.relation_type,e.weight,UTC_TIMESTAMP(6)
                        FROM knowledge_edges e JOIN knowledge_graph_versions v ON v.center_id=e.center_id AND v.subject_id=e.subject_id
                        WHERE v.graph_version_id='{version}' AND e.is_deleted=0 AND e.relation_type='PrerequisiteOf'
                        AND ((e.source_node_id={nodeBase + 3} AND e.target_node_id={nodeBase + 4}) OR (e.source_node_id={nodeBase + 4} AND e.target_node_id={nodeBase + 5}));
                        """);
                else if (grade > 10)
                    m.Sql($"""
                        INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,source_graph_version_id,created_at)
                        SELECT e.center_id,v.graph_version_id,e.edge_id,e.relation_type,e.weight,s.graph_version_id,UTC_TIMESTAMP(6)
                        FROM knowledge_edges e JOIN knowledge_graph_versions v ON v.center_id=e.center_id AND v.subject_id=e.subject_id
                        JOIN knowledge_graph_versions s ON s.center_id=v.center_id AND s.subject_id=v.subject_id AND s.grade_level={grade - 1} AND s.is_current=1
                        WHERE v.graph_version_id='{version}' AND e.is_deleted=0 AND e.relation_type='PrerequisiteOf'
                        AND e.source_node_id={nodeBase + grade - 11} AND e.target_node_id={nodeBase + grade - 10};
                        """);
            }
        }
    }

    private static readonly string[] GuardNames = ["tr_graph_version_insert", "tr_graph_version_update", "tr_graph_version_delete",
        "tr_node_grade_insert", "tr_node_grade_update", "tr_node_grade_delete", "tr_edge_grade_insert", "tr_edge_grade_update", "tr_edge_grade_delete",
        "tr_curriculum_graph_insert", "tr_curriculum_graph_update", "tr_curriculum_node_scope_insert", "tr_curriculum_node_scope_update", "tr_curriculum_node_scope_delete",
        "tr_canonical_node_graph_freeze", "tr_canonical_edge_graph_freeze", "tr_question_graph_insert", "tr_question_graph_update",
        "tr_question_node_graph_insert", "tr_question_node_graph_update", "tr_application_graph_scope_insert",
        "tr_planned_class_scope_insert", "tr_planned_class_scope_update", "tr_class_planned_graph_update"];

    public static void DropGuards(MigrationBuilder m)
    {
        foreach (var name in GuardNames) m.Sql($"DROP TRIGGER IF EXISTS {name};");
        m.Sql("DROP TRIGGER IF EXISTS tr_knowledge_edge_lifecycle_insert;");
        m.Sql("""
            CREATE TRIGGER tr_knowledge_edge_lifecycle_insert BEFORE INSERT ON knowledge_edges FOR EACH ROW
            BEGIN
              IF NEW.is_deleted=0 AND NOT EXISTS(SELECT 1 FROM knowledge_nodes s JOIN knowledge_nodes t ON t.center_id=s.center_id AND t.subject_id=s.subject_id
                WHERE s.center_id=NEW.center_id AND s.node_id=NEW.source_node_id AND t.node_id=NEW.target_node_id AND s.subject_id=NEW.subject_id AND
                  s.is_deleted=0 AND t.is_deleted=0 AND s.is_active=1 AND t.is_active=1)
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge endpoints must be active in the same subject'; END IF;
              IF EXISTS(SELECT 1 FROM classes c JOIN curriculums cu ON cu.center_id=c.center_id AND cu.subject_id=c.subject_id
                WHERE c.center_id=NEW.center_id AND c.status='Active' AND c.learning_scope='Current' AND c.is_deleted=0 AND cu.is_deleted=0 AND
                  EXISTS(SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=cu.center_id AND cn.curriculum_id=cu.curriculum_id AND cn.node_id IN (NEW.source_node_id,NEW.target_node_id)) AND
                  (EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=c.center_id AND a.class_id=c.class_id AND a.curriculum_id=cu.curriculum_id AND a.ended_at IS NULL) OR
                    (cu.review_status='Published' AND NOT EXISTS(SELECT 1 FROM class_curriculum_applications h WHERE h.center_id=c.center_id AND h.class_id=c.class_id) AND
                      EXISTS(SELECT 1 FROM curriculum_classes cc WHERE cc.center_id=c.center_id AND cc.class_id=c.class_id AND cc.curriculum_id=cu.curriculum_id))))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge still used by active classes'; END IF;
            END;
            """);
    }

    private static string Frozen(string prefix) => $"""
        EXISTS(SELECT 1 FROM knowledge_graph_versions v WHERE v.center_id={prefix}.center_id AND v.graph_version_id={prefix}.graph_version_id AND
          (v.status<>'Draft' OR v.is_legacy=1 OR EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=v.center_id AND c.graph_version_id=v.graph_version_id AND
            (c.review_status IN ('Published','Archived') OR EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=c.center_id AND a.curriculum_id=c.curriculum_id)))))
        """;

    public static void AddGuards(MigrationBuilder m)
    {
        // Canonical edges are an endpoint catalog. Only scoped mappings affect teaching; their triggers below enforce lifecycle and DAG.
        m.Sql("DROP TRIGGER IF EXISTS tr_knowledge_edge_lifecycle_insert;");
        m.Sql("""
            CREATE TRIGGER tr_knowledge_edge_lifecycle_insert BEFORE INSERT ON knowledge_edges FOR EACH ROW
            BEGIN
              IF NEW.is_deleted=0 AND NOT EXISTS(SELECT 1 FROM knowledge_nodes s JOIN knowledge_nodes t ON t.center_id=s.center_id AND t.subject_id=s.subject_id
                WHERE s.center_id=NEW.center_id AND s.node_id=NEW.source_node_id AND t.node_id=NEW.target_node_id AND s.subject_id=NEW.subject_id AND
                  s.is_deleted=0 AND t.is_deleted=0 AND s.is_active=1 AND t.is_active=1)
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge endpoints must be active in the same subject'; END IF;
            END;
            """);
        m.Sql("""
            CREATE TRIGGER tr_graph_version_insert BEFORE INSERT ON knowledge_graph_versions FOR EACH ROW
            BEGIN
              IF NEW.status<>'Draft' OR NEW.is_current=1 OR NEW.is_legacy=1
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: create a draft before publishing graph'; END IF;
            END;
            """);
        m.Sql("""
            CREATE TRIGGER tr_graph_version_update BEFORE UPDATE ON knowledge_graph_versions FOR EACH ROW
            BEGIN
              IF NOT(NEW.center_id <=> OLD.center_id) OR NOT(NEW.subject_id <=> OLD.subject_id) OR NOT(NEW.grade_level <=> OLD.grade_level) OR
                NOT(NEW.graph_version_id <=> OLD.graph_version_id) OR NOT(NEW.is_legacy <=> OLD.is_legacy) OR
                (OLD.status<>'Draft' AND (NOT(NEW.status <=> OLD.status) OR NOT(NEW.title <=> OLD.title) OR NOT(NEW.source_graph_version_id <=> OLD.source_graph_version_id)))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: graph identity or published topology is immutable'; END IF;
              IF NOT(NEW.created_at <=> OLD.created_at) OR NOT(NEW.created_by <=> OLD.created_by) OR
                (NEW.status<>OLD.status AND NOT(OLD.status='Draft' AND NEW.status='Published')) OR
                (NEW.is_current=1 AND (NEW.status<>'Published' OR NEW.is_legacy=1))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: invalid graph lifecycle transition'; END IF;
              IF OLD.status='Draft' AND NEW.status='Published' AND NOT EXISTS(SELECT 1 FROM knowledge_node_grades n WHERE n.center_id=NEW.center_id AND n.graph_version_id=NEW.graph_version_id)
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: empty graph cannot be published'; END IF;
            END;
            """);
        m.Sql("CREATE TRIGGER tr_graph_version_delete BEFORE DELETE ON knowledge_graph_versions FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: graph version history cannot be deleted';");
        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            var oldGuard = operation == "UPDATE" ? $" OR {Frozen("OLD")}" : "";
            var nodeIdentity = operation == "UPDATE" ? "IF NOT(NEW.center_id <=> OLD.center_id) OR NOT(NEW.graph_version_id <=> OLD.graph_version_id) OR NOT(NEW.node_id <=> OLD.node_id) OR NOT(NEW.created_at <=> OLD.created_at) OR NOT(NEW.created_by <=> OLD.created_by) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: node mapping identity is immutable'; END IF;" : "";
            var edgeIdentity = operation == "UPDATE" ? "IF NOT(NEW.center_id <=> OLD.center_id) OR NOT(NEW.graph_version_id <=> OLD.graph_version_id) OR NOT(NEW.edge_id <=> OLD.edge_id) OR NOT(NEW.created_at <=> OLD.created_at) OR NOT(NEW.created_by <=> OLD.created_by) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge mapping identity is immutable'; END IF;" : "";
            m.Sql($"""
                CREATE TRIGGER tr_node_grade_{operation.ToLowerInvariant()} BEFORE {operation} ON knowledge_node_grades FOR EACH ROW
                BEGIN
                  DECLARE cursor_node BIGINT UNSIGNED; DECLARE next_node BIGINT UNSIGNED; DECLARE steps INT DEFAULT 0; DECLARE locked_status VARCHAR(16);
                  {nodeIdentity}
                  SELECT status INTO locked_status FROM knowledge_graph_versions WHERE center_id=NEW.center_id AND graph_version_id=NEW.graph_version_id FOR UPDATE;
                  IF {Frozen("NEW")}{oldGuard}
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published node scope is immutable'; END IF;
                  IF NOT EXISTS(SELECT 1 FROM knowledge_graph_versions v JOIN knowledge_nodes n ON n.center_id=v.center_id AND n.subject_id=v.subject_id
                    WHERE v.center_id=NEW.center_id AND v.graph_version_id=NEW.graph_version_id AND n.node_id=NEW.node_id AND n.is_active=1 AND n.is_deleted=0)
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: node grade subject mismatch'; END IF;
                  SET cursor_node=NEW.parent_node_id;
                  WHILE cursor_node IS NOT NULL DO
                    IF cursor_node=NEW.node_id OR steps>10000
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: node grade hierarchy cycle'; END IF;
                    SET next_node=NULL;
                    SELECT parent_node_id INTO next_node FROM knowledge_node_grades WHERE center_id=NEW.center_id AND graph_version_id=NEW.graph_version_id AND node_id=cursor_node;
                    SET cursor_node=next_node; SET steps=steps+1;
                  END WHILE;
                END;
                """);
            m.Sql($"""
                CREATE TRIGGER tr_edge_grade_{operation.ToLowerInvariant()} BEFORE {operation} ON knowledge_edge_grades FOR EACH ROW
                BEGIN
                  DECLARE locked_status VARCHAR(16); DECLARE source_id BIGINT UNSIGNED; DECLARE target_id BIGINT UNSIGNED; DECLARE cycle_count INT DEFAULT 0;
                  {edgeIdentity}
                  SELECT status INTO locked_status FROM knowledge_graph_versions WHERE center_id=NEW.center_id AND graph_version_id=NEW.graph_version_id FOR UPDATE;
                  IF {Frozen("NEW")}{oldGuard}
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published edge scope is immutable'; END IF;
                  IF NOT EXISTS(SELECT 1 FROM knowledge_graph_versions v JOIN knowledge_edges e ON e.center_id=v.center_id AND e.subject_id=v.subject_id
                    JOIN knowledge_node_grades target ON target.center_id=v.center_id AND target.graph_version_id=v.graph_version_id AND target.node_id=e.target_node_id
                    WHERE v.center_id=NEW.center_id AND v.graph_version_id=NEW.graph_version_id AND e.edge_id=NEW.edge_id AND e.is_deleted=0 AND
                    ((NEW.source_graph_version_id IS NULL AND EXISTS(SELECT 1 FROM knowledge_node_grades source WHERE source.center_id=v.center_id AND source.graph_version_id=v.graph_version_id AND source.node_id=e.source_node_id)) OR
                     (NEW.relation_type='PrerequisiteOf' AND EXISTS(SELECT 1 FROM knowledge_graph_versions sv JOIN knowledge_node_grades source ON source.center_id=sv.center_id AND source.graph_version_id=sv.graph_version_id
                       WHERE sv.center_id=v.center_id AND sv.subject_id=v.subject_id AND sv.graph_version_id=NEW.source_graph_version_id AND sv.status='Published' AND sv.is_legacy=0 AND sv.grade_level<v.grade_level AND source.node_id=e.source_node_id))))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: edge grade endpoints or external prerequisite scope mismatch'; END IF;
                  IF NEW.relation_type IN ('PrerequisiteOf','PartOf') AND NEW.source_graph_version_id IS NULL THEN
                    SELECT source_node_id,target_node_id INTO source_id,target_id FROM knowledge_edges WHERE center_id=NEW.center_id AND edge_id=NEW.edge_id;
                    WITH RECURSIVE reachable(node_id) AS (
                      SELECT target_id
                      UNION DISTINCT
                      SELECT e.target_node_id FROM reachable r JOIN knowledge_edges e ON e.center_id=NEW.center_id AND e.source_node_id=r.node_id
                      JOIN knowledge_edge_grades eg ON eg.center_id=e.center_id AND eg.edge_id=e.edge_id AND eg.graph_version_id=NEW.graph_version_id
                      WHERE eg.relation_type IN ('PrerequisiteOf','PartOf') AND eg.source_graph_version_id IS NULL AND eg.edge_id<>NEW.edge_id
                    ) SELECT COUNT(*) INTO cycle_count FROM reachable WHERE node_id=source_id;
                    IF cycle_count>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: scoped prerequisite cycle'; END IF;
                  END IF;
                END;
                """);
        }
        m.Sql($"CREATE TRIGGER tr_node_grade_delete BEFORE DELETE ON knowledge_node_grades FOR EACH ROW BEGIN DECLARE locked_status VARCHAR(16); SELECT status INTO locked_status FROM knowledge_graph_versions WHERE center_id=OLD.center_id AND graph_version_id=OLD.graph_version_id FOR UPDATE; IF {Frozen("OLD")} THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published node scope is immutable'; END IF; END;");
        m.Sql($"CREATE TRIGGER tr_edge_grade_delete BEFORE DELETE ON knowledge_edge_grades FOR EACH ROW BEGIN DECLARE locked_status VARCHAR(16); SELECT status INTO locked_status FROM knowledge_graph_versions WHERE center_id=OLD.center_id AND graph_version_id=OLD.graph_version_id FOR UPDATE; IF {Frozen("OLD")} THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published edge scope is immutable'; END IF; END;");

        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            var freeze = operation == "UPDATE" ? """
                IF (NOT(NEW.graph_version_id <=> OLD.graph_version_id) OR NOT(NEW.grade_level <=> OLD.grade_level)) AND
                  (OLD.review_status IN ('Published','Archived') OR EXISTS(SELECT 1 FROM class_curriculum_applications a WHERE a.center_id=OLD.center_id AND a.curriculum_id=OLD.curriculum_id))
                THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: curriculum graph history is immutable'; END IF;
                """ : "";
            var publishing = operation == "INSERT" ? "NEW.review_status IN ('Published','Archived')" : "OLD.review_status='Draft' AND NEW.review_status='Published'";
            var changingGrade = operation == "INSERT" ? "TRUE" : "NOT(NEW.grade_level <=> OLD.grade_level)";
            m.Sql($"""
                CREATE TRIGGER tr_curriculum_graph_{operation.ToLowerInvariant()} BEFORE {operation} ON curriculums FOR EACH ROW
                BEGIN
                  {freeze}
                  IF ({changingGrade}) AND (NEW.grade_level IS NULL OR NEW.grade_level NOT BETWEEN 10 AND 12)
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: new curriculum grade must be explicit'; END IF;
                  IF {publishing} AND (NEW.graph_version_id IS NULL OR NOT EXISTS(SELECT 1 FROM knowledge_graph_versions v WHERE v.center_id=NEW.center_id AND
                    v.graph_version_id=NEW.graph_version_id AND v.status='Published' AND v.is_legacy=0))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: publishing requires approved graph scope'; END IF;
                  IF NEW.graph_version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM knowledge_graph_versions v WHERE v.center_id=NEW.center_id AND
                    v.graph_version_id=NEW.graph_version_id AND v.subject_id=NEW.subject_id AND v.grade_level=NEW.grade_level AND v.status='Published')
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: curriculum graph grade mismatch'; END IF;
                  IF NEW.graph_version_id IS NOT NULL AND EXISTS(SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=NEW.center_id AND cn.curriculum_id=NEW.curriculum_id AND
                    NOT EXISTS(SELECT 1 FROM knowledge_node_grades ng WHERE ng.center_id=cn.center_id AND ng.graph_version_id=NEW.graph_version_id AND ng.node_id=cn.node_id))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: curriculum nodes outside graph grade'; END IF;
                  IF NEW.graph_version_id IS NULL AND EXISTS(SELECT 1 FROM curriculum_nodes cn WHERE cn.center_id=NEW.center_id AND cn.curriculum_id=NEW.curriculum_id)
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: retained curriculum nodes require graph scope'; END IF;
                  IF NEW.review_status='Draft' AND EXISTS(SELECT 1 FROM curriculum_classes cc JOIN classes cl ON cl.center_id=cc.center_id AND cl.class_id=cc.class_id
                    WHERE cc.center_id=NEW.center_id AND cc.curriculum_id=NEW.curriculum_id AND
                      (cl.is_deleted=1 OR cl.status<>'Active' OR cl.learning_scope<>'Current' OR NOT(cl.teacher_id <=> NEW.teacher_id) OR
                       NOT(cl.subject_id <=> NEW.subject_id) OR NOT(cl.grade_level <=> NEW.grade_level)))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: draft planned classes outside curriculum scope'; END IF;
                END;
                """);
            m.Sql($"""
                CREATE TRIGGER tr_planned_class_scope_{operation.ToLowerInvariant()} BEFORE {operation} ON curriculum_classes FOR EACH ROW
                BEGIN
                  DECLARE locked_curriculum VARCHAR(36); DECLARE locked_class VARCHAR(36);
                  SELECT curriculum_id INTO locked_curriculum FROM curriculums WHERE center_id=NEW.center_id AND curriculum_id=NEW.curriculum_id FOR UPDATE;
                  SELECT class_id INTO locked_class FROM classes WHERE center_id=NEW.center_id AND class_id=NEW.class_id FOR UPDATE;
                  IF EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=NEW.center_id AND c.curriculum_id=NEW.curriculum_id AND c.review_status='Draft' AND
                    NOT EXISTS(SELECT 1 FROM classes cl WHERE cl.center_id=c.center_id AND cl.class_id=NEW.class_id AND cl.subject_id=c.subject_id AND
                      cl.teacher_id=c.teacher_id AND cl.grade_level=c.grade_level AND cl.status='Active' AND cl.learning_scope='Current' AND cl.is_deleted=0))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: invalid draft planned class scope'; END IF;
                END;
                """);
            m.Sql($"""
                CREATE TRIGGER tr_curriculum_node_scope_{operation.ToLowerInvariant()} BEFORE {operation} ON curriculum_nodes FOR EACH ROW
                BEGIN
                  IF EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=NEW.center_id AND c.curriculum_id=NEW.curriculum_id AND c.review_status IN ('Published','Archived'))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published curriculum composition is immutable'; END IF;
                  IF EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=NEW.center_id AND c.curriculum_id=NEW.curriculum_id AND c.graph_version_id IS NULL)
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: choose graph scope before assigning nodes'; END IF;
                  IF EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=NEW.center_id AND c.curriculum_id=NEW.curriculum_id AND c.graph_version_id IS NOT NULL AND
                    NOT EXISTS(SELECT 1 FROM knowledge_node_grades n WHERE n.center_id=c.center_id AND n.graph_version_id=c.graph_version_id AND n.node_id=NEW.node_id))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: node outside curriculum grade'; END IF;
                END;
                """);
        }
        m.Sql("""
            CREATE TRIGGER tr_class_planned_graph_update BEFORE UPDATE ON classes FOR EACH ROW
            BEGIN
              IF EXISTS(SELECT 1 FROM curriculum_classes cc JOIN curriculums c ON c.center_id=cc.center_id AND c.curriculum_id=cc.curriculum_id
                WHERE cc.center_id=OLD.center_id AND cc.class_id=OLD.class_id AND c.review_status='Draft' AND c.is_deleted=0 AND
                  (NOT(NEW.center_id <=> OLD.center_id) OR NOT(NEW.class_id <=> OLD.class_id) OR NOT(NEW.subject_id <=> c.subject_id) OR
                   NOT(NEW.teacher_id <=> c.teacher_id) OR NOT(NEW.grade_level <=> c.grade_level)))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: class identity conflicts with draft curriculum plans'; END IF;
            END;
            """);
        m.Sql("""
            CREATE TRIGGER tr_curriculum_node_scope_delete BEFORE DELETE ON curriculum_nodes FOR EACH ROW
            BEGIN
              IF EXISTS(SELECT 1 FROM curriculums c WHERE c.center_id=OLD.center_id AND c.curriculum_id=OLD.curriculum_id AND c.review_status IN ('Published','Archived'))
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: published curriculum composition is immutable'; END IF;
            END;
            """);
        m.Sql("""
            CREATE TRIGGER tr_canonical_node_graph_freeze BEFORE UPDATE ON knowledge_nodes FOR EACH ROW
            BEGIN
              IF (NOT(NEW.node_name <=> OLD.node_name) OR NOT(NEW.description <=> OLD.description) OR NOT(NEW.node_type <=> OLD.node_type) OR
                NOT(NEW.node_code <=> OLD.node_code) OR NOT(NEW.subject_id <=> OLD.subject_id) OR NOT(NEW.order_index <=> OLD.order_index) OR
                NOT(NEW.exam_importance <=> OLD.exam_importance) OR NOT(NEW.estimated_learning_minutes <=> OLD.estimated_learning_minutes) OR NEW.is_deleted=1 OR NEW.is_active=0) AND EXISTS(
                  SELECT 1 FROM knowledge_node_grades n JOIN knowledge_graph_versions v ON v.center_id=n.center_id AND v.graph_version_id=n.graph_version_id
                  WHERE n.center_id=OLD.center_id AND n.node_id=OLD.node_id AND v.status<>'Draft')
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: canonical node used by published graph is immutable'; END IF;
            END;
            """);
        m.Sql("""
            CREATE TRIGGER tr_canonical_edge_graph_freeze BEFORE UPDATE ON knowledge_edges FOR EACH ROW
            BEGIN
              IF (NOT(NEW.source_node_id <=> OLD.source_node_id) OR NOT(NEW.target_node_id <=> OLD.target_node_id) OR NOT(NEW.subject_id <=> OLD.subject_id) OR NEW.is_deleted=1) AND
                EXISTS(SELECT 1 FROM knowledge_edge_grades e WHERE e.center_id=OLD.center_id AND e.edge_id=OLD.edge_id)
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: canonical edge endpoints are referenced by a graph'; END IF;
            END;
            """);
        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            var changed = operation == "INSERT" ? "TRUE" : "NOT(NEW.grade_level <=> OLD.grade_level) OR NOT(NEW.primary_topic_node_id <=> OLD.primary_topic_node_id) OR NOT(NEW.subject_id <=> OLD.subject_id) OR NOT(NEW.center_id <=> OLD.center_id)";
            m.Sql($"""
                CREATE TRIGGER tr_question_graph_{operation.ToLowerInvariant()} BEFORE {operation} ON questions FOR EACH ROW
                BEGIN
                  IF ({changed}) AND (
                    NEW.grade_level IS NULL OR NOT EXISTS(SELECT 1 FROM knowledge_node_grades ng JOIN knowledge_graph_versions v ON v.center_id=ng.center_id AND v.graph_version_id=ng.graph_version_id
                      JOIN knowledge_nodes n ON n.center_id=ng.center_id AND n.node_id=ng.node_id
                      WHERE ng.center_id=NEW.center_id AND ng.node_id=NEW.primary_topic_node_id AND v.subject_id=NEW.subject_id AND v.grade_level=NEW.grade_level AND
                        v.status='Published' AND v.is_current=1 AND v.is_legacy=0 AND n.is_deleted=0 AND n.is_active=1) OR
                    EXISTS(SELECT 1 FROM question_knowledge_nodes qn WHERE qn.center_id=NEW.center_id AND qn.question_id=NEW.question_id AND NOT EXISTS(
                      SELECT 1 FROM knowledge_node_grades ng JOIN knowledge_graph_versions v ON v.center_id=ng.center_id AND v.graph_version_id=ng.graph_version_id
                      JOIN knowledge_nodes n ON n.center_id=ng.center_id AND n.node_id=ng.node_id
                      WHERE ng.center_id=NEW.center_id AND ng.node_id=qn.node_id AND v.subject_id=NEW.subject_id AND v.status='Published' AND v.is_current=1 AND v.is_legacy=0 AND
                        n.is_deleted=0 AND n.is_active=1 AND (qn.mapping_role='Prerequisite' OR v.grade_level=NEW.grade_level))))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: question grade outside approved knowledge scope'; END IF;
                END;
                """);
            m.Sql($"""
                CREATE TRIGGER tr_question_node_graph_{operation.ToLowerInvariant()} BEFORE {operation} ON question_knowledge_nodes FOR EACH ROW
                BEGIN
                  IF NOT EXISTS(SELECT 1 FROM questions q JOIN knowledge_graph_versions v ON v.center_id=q.center_id AND v.subject_id=q.subject_id
                    JOIN knowledge_node_grades ng ON ng.center_id=v.center_id AND ng.graph_version_id=v.graph_version_id
                    JOIN knowledge_nodes n ON n.center_id=ng.center_id AND n.node_id=ng.node_id
                    WHERE q.center_id=NEW.center_id AND q.question_id=NEW.question_id AND ng.node_id=NEW.node_id AND n.is_deleted=0 AND n.is_active=1 AND
                      v.status='Published' AND v.is_current=1 AND v.is_legacy=0 AND (NEW.mapping_role='Prerequisite' OR v.grade_level=q.grade_level))
                  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: question mapping outside approved knowledge scope'; END IF;
                END;
                """);
        }
        m.Sql("""
            CREATE TRIGGER tr_application_graph_scope_insert BEFORE INSERT ON class_curriculum_applications FOR EACH ROW
            BEGIN
              IF NOT EXISTS(SELECT 1 FROM curriculums c JOIN knowledge_graph_versions v ON v.center_id=c.center_id AND v.graph_version_id=c.graph_version_id
                WHERE c.center_id=NEW.center_id AND c.curriculum_id=NEW.curriculum_id AND v.status='Published' AND v.is_legacy=0 AND
                  v.subject_id=c.subject_id AND v.grade_level=c.grade_level)
              THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='EDUTWIN_ACADEMIC_DEPENDENCY: new application requires approved knowledge scope'; END IF;
            END;
            """);
    }
}
