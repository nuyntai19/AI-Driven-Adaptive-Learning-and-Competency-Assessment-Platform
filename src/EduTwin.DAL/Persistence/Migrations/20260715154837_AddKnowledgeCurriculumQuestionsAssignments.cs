using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeCurriculumQuestionsAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    assignment_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    class_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    created_by_teacher_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    title = table.Column<string>(type: "varchar(250)", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    due_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_assignments", x => x.assignment_id);
                    table.UniqueConstraint("ux_assignments_center_id_assignment_id", x => new { x.center_id, x.assignment_id });
                    table.CheckConstraint("ck_assignments_status", "status IN ('Draft', 'Published', 'Closed', 'Archived')");
                    table.ForeignKey(
                        name: "fk_assignments_classes_class",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_teachers_created_by_teacher",
                        columns: x => new { x.center_id, x.created_by_teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "curriculums",
                columns: table => new
                {
                    curriculum_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    teacher_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    title = table.Column<string>(type: "varchar(250)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    source_file = table.Column<string>(type: "varchar(500)", nullable: true),
                    review_status = table.Column<string>(type: "varchar(32)", nullable: false),
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
                    table.PrimaryKey("pk_curriculums", x => x.curriculum_id);
                    table.UniqueConstraint("ux_curriculums_center_id_curriculum_id", x => new { x.center_id, x.curriculum_id });
                    table.CheckConstraint("ck_curriculums_review_status", "review_status IN ('Draft', 'Published', 'Archived')");
                    table.ForeignKey(
                        name: "fk_curriculums_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curriculums_teachers_teacher",
                        columns: x => new { x.center_id, x.teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "knowledge_nodes",
                columns: table => new
                {
                    node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    parent_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    node_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    node_code = table.Column<string>(type: "varchar(64)", nullable: false),
                    node_name = table.Column<string>(type: "varchar(200)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    order_index = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    exam_importance = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    estimated_learning_minutes = table.Column<uint>(type: "int unsigned", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_knowledge_nodes", x => x.node_id);
                    table.UniqueConstraint("ux_knowledge_nodes_center_id_node_id", x => new { x.center_id, x.node_id });
                    table.CheckConstraint("ck_knowledge_nodes_estimated_learning_minutes", "estimated_learning_minutes > 0");
                    table.CheckConstraint("ck_knowledge_nodes_exam_importance", "exam_importance BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_knowledge_nodes_node_type", "node_type IN ('Subject', 'Chapter', 'Topic', 'Skill', 'Concept')");
                    table.ForeignKey(
                        name: "fk_knowledge_nodes_knowledge_nodes_parent",
                        columns: x => new { x.center_id, x.parent_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_nodes_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "assignment_targets",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    assignment_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    target_source = table.Column<string>(type: "varchar(32)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_targets", x => new { x.center_id, x.assignment_id, x.student_id });
                    table.CheckConstraint("ck_assignment_targets_target_source", "target_source IN ('WholeClass', 'SelectedStudents', 'GapGroup')");
                    table.ForeignKey(
                        name: "fk_assignment_targets_assignments_assignment",
                        columns: x => new { x.center_id, x.assignment_id },
                        principalTable: "assignments",
                        principalColumns: new[] { "center_id", "assignment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignment_targets_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "student_assignment_progress",
                columns: table => new
                {
                    progress_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    assignment_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    completed_question_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    total_question_count = table.Column<uint>(type: "int unsigned", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_student_assignment_progress", x => x.progress_id);
                    table.CheckConstraint("ck_student_assignment_progress_counts", "completed_question_count <= total_question_count");
                    table.CheckConstraint("ck_student_assignment_progress_status", "status IN ('NotStarted', 'InProgress', 'Completed', 'Overdue')");
                    table.ForeignKey(
                        name: "fk_student_assignment_progress_assignments_assignment",
                        columns: x => new { x.center_id, x.assignment_id },
                        principalTable: "assignments",
                        principalColumns: new[] { "center_id", "assignment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_assignment_progress_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "curriculum_classes",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    curriculum_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    class_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    assigned_by = table.Column<string>(type: "varchar(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curriculum_classes", x => new { x.center_id, x.curriculum_id, x.class_id });
                    table.ForeignKey(
                        name: "fk_curriculum_classes_classes_class",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curriculum_classes_curriculums_curriculum",
                        columns: x => new { x.center_id, x.curriculum_id },
                        principalTable: "curriculums",
                        principalColumns: new[] { "center_id", "curriculum_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "curriculum_nodes",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    curriculum_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    order_index = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curriculum_nodes", x => new { x.center_id, x.curriculum_id, x.node_id });
                    table.ForeignKey(
                        name: "fk_curriculum_nodes_curriculums_curriculum",
                        columns: x => new { x.center_id, x.curriculum_id },
                        principalTable: "curriculums",
                        principalColumns: new[] { "center_id", "curriculum_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_curriculum_nodes_knowledge_nodes_node",
                        columns: x => new { x.center_id, x.node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "knowledge_edges",
                columns: table => new
                {
                    edge_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    source_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    target_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    relation_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 1.00m),
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
                    table.PrimaryKey("pk_knowledge_edges", x => x.edge_id);
                    table.CheckConstraint("ck_knowledge_edges_relation_type", "relation_type IN ('PrerequisiteOf', 'RelatedTo', 'PartOf', 'CausesErrorIn')");
                    table.CheckConstraint("ck_knowledge_edges_self_loop", "source_node_id <> target_node_id");
                    table.CheckConstraint("ck_knowledge_edges_weight", "weight BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_knowledge_edges_knowledge_nodes_source",
                        columns: x => new { x.center_id, x.source_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_edges_knowledge_nodes_target",
                        columns: x => new { x.center_id, x.target_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_edges_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    primary_topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_by_teacher_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    difficulty = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    question_text = table.Column<string>(type: "longtext", nullable: false),
                    correct_answer = table.Column<string>(type: "text", nullable: false),
                    solution = table.Column<string>(type: "longtext", nullable: false),
                    expected_reasoning = table.Column<string>(type: "longtext", nullable: true),
                    grading_criteria = table.Column<string>(type: "json", nullable: false),
                    max_score = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 1.00m),
                    estimated_time_seconds = table.Column<uint>(type: "int unsigned", nullable: false),
                    reasoning_required = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    language_code = table.Column<string>(type: "varchar(8)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
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
                    table.PrimaryKey("pk_questions", x => x.question_id);
                    table.UniqueConstraint("ux_questions_center_id_question_id", x => new { x.center_id, x.question_id });
                    table.CheckConstraint("ck_questions_difficulty", "difficulty BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_questions_estimated_time_seconds", "estimated_time_seconds > 0");
                    table.CheckConstraint("ck_questions_language_code", "language_code IN ('vi', 'en')");
                    table.CheckConstraint("ck_questions_max_score", "max_score > 0");
                    table.CheckConstraint("ck_questions_question_type", "question_type IN ('MultipleChoice', 'ShortAnswer', 'Essay')");
                    table.CheckConstraint("ck_questions_status", "status IN ('Draft', 'Active', 'Archived')");
                    table.ForeignKey(
                        name: "fk_questions_knowledge_nodes_primary_topic",
                        columns: x => new { x.center_id, x.primary_topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_teachers_created_by_teacher",
                        columns: x => new { x.center_id, x.created_by_teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "assignment_questions",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    assignment_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    order_index = table.Column<uint>(type: "int unsigned", nullable: false),
                    points = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_questions", x => new { x.center_id, x.assignment_id, x.question_id });
                    table.CheckConstraint("ck_assignment_questions_points", "points > 0");
                    table.ForeignKey(
                        name: "fk_assignment_questions_assignments_assignment",
                        columns: x => new { x.center_id, x.assignment_id },
                        principalTable: "assignments",
                        principalColumns: new[] { "center_id", "assignment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignment_questions_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "question_knowledge_nodes",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    mapping_role = table.Column<string>(type: "varchar(32)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_knowledge_nodes", x => new { x.center_id, x.question_id, x.node_id, x.mapping_role });
                    table.CheckConstraint("ck_question_knowledge_nodes_mapping_role", "mapping_role IN ('Primary', 'Secondary', 'Prerequisite')");
                    table.ForeignKey(
                        name: "fk_question_knowledge_nodes_knowledge_nodes_node",
                        columns: x => new { x.center_id, x.node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_question_knowledge_nodes_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    option_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    option_label = table.Column<string>(type: "varchar(8)", nullable: false),
                    option_text = table.Column<string>(type: "text", nullable: false),
                    is_correct = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    order_index = table.Column<uint>(type: "int unsigned", nullable: false),
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
                    table.PrimaryKey("pk_question_options", x => x.option_id);
                    table.ForeignKey(
                        name: "fk_question_options_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_questions_center_id_question_id",
                table: "assignment_questions",
                columns: new[] { "center_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ux_assignment_questions_center_id_assignment_id_order_index",
                table: "assignment_questions",
                columns: new[] { "center_id", "assignment_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_targets_center_id_student_id",
                table: "assignment_targets",
                columns: new[] { "center_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_center_id_class_id_status_due_at",
                table: "assignments",
                columns: new[] { "center_id", "class_id", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_center_id_created_by_teacher_id",
                table: "assignments",
                columns: new[] { "center_id", "created_by_teacher_id" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_classes_center_id_class_id",
                table: "curriculum_classes",
                columns: new[] { "center_id", "class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_nodes_center_id_node_id",
                table: "curriculum_nodes",
                columns: new[] { "center_id", "node_id" });

            migrationBuilder.CreateIndex(
                name: "ux_curriculum_nodes_center_id_curriculum_id_order_index",
                table: "curriculum_nodes",
                columns: new[] { "center_id", "curriculum_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curriculums_center_id_subject_id_review_status",
                table: "curriculums",
                columns: new[] { "center_id", "subject_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculums_center_id_teacher_id_review_status",
                table: "curriculums",
                columns: new[] { "center_id", "teacher_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_edges_center_id_source_node_id",
                table: "knowledge_edges",
                columns: new[] { "center_id", "source_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_edges_center_id_subject_id",
                table: "knowledge_edges",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_edges_center_id_target_node_id_relation_type",
                table: "knowledge_edges",
                columns: new[] { "center_id", "target_node_id", "relation_type" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_edges_center_id_edge_id",
                table: "knowledge_edges",
                columns: new[] { "center_id", "edge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_edges_center_id_source_id_target_id_relation_type",
                table: "knowledge_edges",
                columns: new[] { "center_id", "source_node_id", "target_node_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_nodes_center_id_parent_node_id",
                table: "knowledge_nodes",
                columns: new[] { "center_id", "parent_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_nodes_center_id_subject_id_node_type_order_index",
                table: "knowledge_nodes",
                columns: new[] { "center_id", "subject_id", "node_type", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_nodes_center_id_subject_id_node_code",
                table: "knowledge_nodes",
                columns: new[] { "center_id", "subject_id", "node_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_knowledge_nodes_center_id_node_id",
                table: "question_knowledge_nodes",
                columns: new[] { "center_id", "node_id" });

            migrationBuilder.CreateIndex(
                name: "ux_question_options_center_id_question_id_option_label",
                table: "question_options",
                columns: new[] { "center_id", "question_id", "option_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_question_options_center_id_question_id_order_index",
                table: "question_options",
                columns: new[] { "center_id", "question_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_center_id_created_by_teacher_id_status",
                table: "questions",
                columns: new[] { "center_id", "created_by_teacher_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_center_id_primary_topic_node_id",
                table: "questions",
                columns: new[] { "center_id", "primary_topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_center_id_subject_id_topic_id_status_difficulty",
                table: "questions",
                columns: new[] { "center_id", "subject_id", "primary_topic_node_id", "status", "difficulty" });

            migrationBuilder.CreateIndex(
                name: "ix_student_assignment_progress_center_id_student_id_status",
                table: "student_assignment_progress",
                columns: new[] { "center_id", "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_student_assignment_progress_center_assignment_id_student_id",
                table: "student_assignment_progress",
                columns: new[] { "center_id", "assignment_id", "student_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_questions");

            migrationBuilder.DropTable(
                name: "assignment_targets");

            migrationBuilder.DropTable(
                name: "curriculum_classes");

            migrationBuilder.DropTable(
                name: "curriculum_nodes");

            migrationBuilder.DropTable(
                name: "knowledge_edges");

            migrationBuilder.DropTable(
                name: "question_knowledge_nodes");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "student_assignment_progress");

            migrationBuilder.DropTable(
                name: "curriculums");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropTable(
                name: "knowledge_nodes");
        }
    }
}
