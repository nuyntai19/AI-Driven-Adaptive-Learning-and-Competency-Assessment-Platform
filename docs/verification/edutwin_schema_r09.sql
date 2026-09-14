-- MySQL dump 10.13  Distrib 8.0.35, for Linux (x86_64)
--
-- Host: localhost    Database: edutwin
-- ------------------------------------------------------
-- Server version	8.0.35

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `__EFMigrationsHistory`
--

DROP TABLE IF EXISTS `__EFMigrationsHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ai_analysis_jobs`
--

DROP TABLE IF EXISTS `ai_analysis_jobs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ai_analysis_jobs` (
  `analysis_job_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `attempt_id` bigint unsigned NOT NULL,
  `status` varchar(32) NOT NULL,
  `retry_count` tinyint unsigned NOT NULL DEFAULT '0',
  `available_at` datetime(6) NOT NULL,
  `started_at` datetime(6) DEFAULT NULL,
  `completed_at` datetime(6) DEFAULT NULL,
  `lease_owner` varchar(100) DEFAULT NULL,
  `lease_until` datetime(6) DEFAULT NULL,
  `last_error_code` varchar(100) DEFAULT NULL,
  `last_error_message` varchar(1000) DEFAULT NULL,
  `correlation_id` varchar(64) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`analysis_job_id`),
  UNIQUE KEY `ux_ai_analysis_jobs_center_id_attempt_id` (`center_id`,`attempt_id`),
  KEY `ix_ai_analysis_jobs_center_id_status_created_at` (`center_id`,`status`,`created_at`),
  KEY `ix_ai_analysis_jobs_status_available_at_lease_until` (`status`,`available_at`,`lease_until`),
  CONSTRAINT `fk_ai_analysis_jobs_attempts_attempt` FOREIGN KEY (`center_id`, `attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_ai_analysis_jobs_retry_count` CHECK ((`retry_count` between 0 and 3)),
  CONSTRAINT `ck_ai_analysis_jobs_status` CHECK ((`status` in (_utf8mb4'Pending',_utf8mb4'Processing',_utf8mb4'Completed',_utf8mb4'FallbackCompleted',_utf8mb4'FailedTerminal')))
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `assignment_questions`
--

DROP TABLE IF EXISTS `assignment_questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `assignment_questions` (
  `center_id` varchar(36) NOT NULL,
  `assignment_id` varchar(36) NOT NULL,
  `question_id` bigint unsigned NOT NULL,
  `order_index` int unsigned NOT NULL,
  `points` decimal(5,2) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`center_id`,`assignment_id`,`question_id`),
  UNIQUE KEY `ux_assignment_questions_center_id_assignment_id_order_index` (`center_id`,`assignment_id`,`order_index`),
  KEY `ix_assignment_questions_center_id_question_id` (`center_id`,`question_id`),
  CONSTRAINT `fk_assignment_questions_assignments_assignment` FOREIGN KEY (`center_id`, `assignment_id`) REFERENCES `assignments` (`center_id`, `assignment_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_assignment_questions_questions_question` FOREIGN KEY (`center_id`, `question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_assignment_questions_points` CHECK ((`points` > 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `assignment_targets`
--

DROP TABLE IF EXISTS `assignment_targets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `assignment_targets` (
  `center_id` varchar(36) NOT NULL,
  `assignment_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `target_source` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) NOT NULL,
  PRIMARY KEY (`center_id`,`assignment_id`,`student_id`),
  KEY `ix_assignment_targets_center_id_student_id` (`center_id`,`student_id`),
  CONSTRAINT `fk_assignment_targets_assignments_assignment` FOREIGN KEY (`center_id`, `assignment_id`) REFERENCES `assignments` (`center_id`, `assignment_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_assignment_targets_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_assignment_targets_target_source` CHECK ((`target_source` in (_utf8mb4'WholeClass',_utf8mb4'SelectedStudents',_utf8mb4'GapGroup')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `assignments`
--

DROP TABLE IF EXISTS `assignments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `assignments` (
  `assignment_id` varchar(36) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `class_id` varchar(36) NOT NULL,
  `created_by_teacher_id` varchar(36) NOT NULL,
  `title` varchar(250) NOT NULL,
  `instructions` text,
  `due_at` datetime(6) DEFAULT NULL,
  `status` varchar(32) NOT NULL,
  `published_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`assignment_id`),
  UNIQUE KEY `ux_assignments_center_id_assignment_id` (`center_id`,`assignment_id`),
  KEY `ix_assignments_center_id_class_id_status_due_at` (`center_id`,`class_id`,`status`,`due_at`),
  KEY `ix_assignments_center_id_created_by_teacher_id` (`center_id`,`created_by_teacher_id`),
  CONSTRAINT `fk_assignments_classes_class` FOREIGN KEY (`center_id`, `class_id`) REFERENCES `classes` (`center_id`, `class_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_assignments_teachers_created_by_teacher` FOREIGN KEY (`center_id`, `created_by_teacher_id`) REFERENCES `teachers` (`center_id`, `teacher_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_assignments_status` CHECK ((`status` in (_utf8mb4'Draft',_utf8mb4'Published',_utf8mb4'Closed',_utf8mb4'Archived')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `attempt_attachments`
--

DROP TABLE IF EXISTS `attempt_attachments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `attempt_attachments` (
  `attachment_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `attempt_id` bigint unsigned NOT NULL,
  `file_name` varchar(255) NOT NULL,
  `content_type` varchar(64) NOT NULL,
  `storage_key` varchar(512) NOT NULL,
  `file_size_bytes` bigint NOT NULL,
  `upload_nonce` varchar(64) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`attachment_id`),
  UNIQUE KEY `ux_attempt_attachments_center_id_attempt_id` (`center_id`,`attempt_id`),
  UNIQUE KEY `ux_attempt_attachments_center_id_storage_key` (`center_id`,`storage_key`),
  UNIQUE KEY `ux_attempt_attachments_center_id_upload_nonce` (`center_id`,`upload_nonce`),
  CONSTRAINT `fk_attempt_attachments_attempts` FOREIGN KEY (`center_id`, `attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_attempt_attachments_content_type` CHECK ((`content_type` = _utf8mb4'image/png')),
  CONSTRAINT `ck_attempt_attachments_file_size_bytes` CHECK (((`file_size_bytes` >= 1) and (`file_size_bytes` <= 5242880)))
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `attempts`
--

DROP TABLE IF EXISTS `attempts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `attempts` (
  `attempt_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `question_id` bigint unsigned NOT NULL,
  `assignment_id` varchar(36) DEFAULT NULL,
  `final_answer` longtext NOT NULL,
  `reasoning_text` longtext,
  `is_correct` tinyint(1) DEFAULT NULL,
  `awarded_score` decimal(5,2) DEFAULT NULL,
  `time_spent_seconds` int unsigned NOT NULL,
  `confidence` decimal(5,2) NOT NULL,
  `answer_changes` int unsigned NOT NULL DEFAULT '0',
  `skipped` tinyint(1) NOT NULL DEFAULT '0',
  `reasoning_language` varchar(8) NOT NULL,
  `status` varchar(32) NOT NULL,
  `client_submission_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `answer_display_latex` varchar(2048) DEFAULT NULL,
  PRIMARY KEY (`attempt_id`),
  UNIQUE KEY `ux_attempts_center_id_attempt_id` (`center_id`,`attempt_id`),
  UNIQUE KEY `ux_attempts_center_id_student_id_client_submission_id` (`center_id`,`student_id`,`client_submission_id`),
  KEY `ix_attempts_center_id_assignment_id_student_id` (`center_id`,`assignment_id`,`student_id`),
  KEY `ix_attempts_center_id_question_id` (`center_id`,`question_id`),
  KEY `ix_attempts_center_id_status_created_at` (`center_id`,`status`,`created_at`),
  KEY `ix_attempts_center_id_student_id_question_id_created_at` (`center_id`,`student_id`,`question_id`,`created_at`),
  CONSTRAINT `fk_attempts_assignments_assignment` FOREIGN KEY (`center_id`, `assignment_id`) REFERENCES `assignments` (`center_id`, `assignment_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_attempts_questions_question` FOREIGN KEY (`center_id`, `question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_attempts_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_attempts_confidence` CHECK ((`confidence` between 0 and 100)),
  CONSTRAINT `ck_attempts_reasoning_language` CHECK ((`reasoning_language` in (_utf8mb4'vi',_utf8mb4'en'))),
  CONSTRAINT `ck_attempts_status` CHECK ((`status` in (_utf8mb4'PendingAnalysis',_utf8mb4'Processing',_utf8mb4'Completed',_utf8mb4'NeedsTeacherReview',_utf8mb4'AnalysisFailed'))),
  CONSTRAINT `ck_attempts_time_spent_seconds` CHECK ((`time_spent_seconds` >= 0))
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `authorization_audit_logs`
--

DROP TABLE IF EXISTS `authorization_audit_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `authorization_audit_logs` (
  `authorization_audit_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `actor_user_id` varchar(36) DEFAULT NULL,
  `action_type` varchar(64) NOT NULL,
  `target_type` varchar(64) NOT NULL,
  `target_id` varchar(128) NOT NULL,
  `target_user_id` varchar(36) DEFAULT NULL,
  `permission_code` varchar(100) DEFAULT NULL,
  `before_data` json DEFAULT NULL,
  `after_data` json DEFAULT NULL,
  `reason` varchar(1000) NOT NULL,
  `trace_id` varchar(64) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `target_center_id` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`authorization_audit_id`),
  UNIQUE KEY `ux_authorization_audit_logs_center_id_audit_id` (`center_id`,`authorization_audit_id`),
  KEY `ix_authorization_audit_logs_center_actor_created` (`center_id`,`actor_user_id`,`created_at`),
  KEY `ix_authorization_audit_logs_center_created_action` (`center_id`,`created_at`,`action_type`),
  KEY `ix_authorization_audit_logs_center_target_created` (`center_id`,`target_user_id`,`created_at`),
  KEY `ix_auth_audit_center_target_center_created` (`center_id`,`target_center_id`,`created_at`),
  KEY `IX_authorization_audit_logs_target_center_id` (`target_center_id`),
  CONSTRAINT `fk_authorization_audit_logs_centers_target` FOREIGN KEY (`target_center_id`) REFERENCES `centers` (`center_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_authorization_audit_logs_users_actor` FOREIGN KEY (`center_id`, `actor_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_authorization_audit_logs_users_target` FOREIGN KEY (`center_id`, `target_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=73 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `behavior_twins`
--

DROP TABLE IF EXISTS `behavior_twins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `behavior_twins` (
  `behavior_twin_id` bigint unsigned NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `avg_time_spent_seconds` decimal(10,2) NOT NULL DEFAULT '0.00',
  `skip_rate` decimal(5,2) NOT NULL DEFAULT '0.00',
  `change_answer_rate` decimal(5,2) NOT NULL DEFAULT '0.00',
  `avg_confidence` decimal(5,2) NOT NULL DEFAULT '0.00',
  `confidence_calibration` decimal(5,2) NOT NULL DEFAULT '0.00',
  `attempt_count` int unsigned NOT NULL DEFAULT '0',
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`behavior_twin_id`),
  UNIQUE KEY `ux_behavior_twins_center_id_student_id_subject_id` (`center_id`,`student_id`,`subject_id`),
  KEY `ix_behavior_twins_center_id_subject_id` (`center_id`,`subject_id`),
  CONSTRAINT `fk_behavior_twins_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_behavior_twins_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_behavior_twins_avg_confidence` CHECK ((`avg_confidence` between 0 and 100)),
  CONSTRAINT `ck_behavior_twins_change_answer_rate` CHECK ((`change_answer_rate` between 0 and 100)),
  CONSTRAINT `ck_behavior_twins_confidence_calibration` CHECK ((`confidence_calibration` between 0 and 100)),
  CONSTRAINT `ck_behavior_twins_skip_rate` CHECK ((`skip_rate` between 0 and 100))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `centers`
--

DROP TABLE IF EXISTS `centers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `centers` (
  `center_id` varchar(36) NOT NULL,
  `center_code` varchar(32) NOT NULL,
  `center_name` varchar(200) NOT NULL,
  `status` varchar(32) NOT NULL,
  `timezone` varchar(64) NOT NULL DEFAULT 'Asia/Bangkok',
  `created_at` datetime(6) NOT NULL,
  `updated_at` datetime(6) NOT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `primary_manager_user_id` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`center_id`),
  UNIQUE KEY `ux_centers_center_code` (`center_code`),
  UNIQUE KEY `ix_centers_primary_manager_user_id` (`primary_manager_user_id`),
  KEY `IX_centers_center_id_primary_manager_user_id` (`center_id`,`primary_manager_user_id`),
  CONSTRAINT `fk_centers_primary_manager_user` FOREIGN KEY (`center_id`, `primary_manager_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_centers_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Suspended')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `class_students`
--

DROP TABLE IF EXISTS `class_students`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `class_students` (
  `class_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `joined_at` datetime(6) NOT NULL,
  `status` varchar(32) NOT NULL,
  `removed_at` datetime(6) DEFAULT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`center_id`,`class_id`,`student_id`),
  KEY `ix_class_students_center_id_student_id_status` (`center_id`,`student_id`,`status`),
  CONSTRAINT `fk_class_students_classes_class` FOREIGN KEY (`center_id`, `class_id`) REFERENCES `classes` (`center_id`, `class_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_class_students_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_class_students_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Removed')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `classes`
--

DROP TABLE IF EXISTS `classes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `classes` (
  `class_id` varchar(36) NOT NULL,
  `teacher_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `class_name` varchar(150) NOT NULL,
  `academic_year` varchar(20) NOT NULL,
  `status` varchar(32) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`class_id`),
  UNIQUE KEY `ux_classes_center_id_class_id` (`center_id`,`class_id`),
  UNIQUE KEY `ux_classes_center_id_class_name_academic_year` (`center_id`,`class_name`,`academic_year`),
  KEY `ix_classes_center_id_subject_id_status` (`center_id`,`subject_id`,`status`),
  KEY `ix_classes_center_id_teacher_id_status` (`center_id`,`teacher_id`,`status`),
  CONSTRAINT `fk_classes_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_classes_teachers_teacher` FOREIGN KEY (`center_id`, `teacher_id`) REFERENCES `teachers` (`center_id`, `teacher_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_classes_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Archived')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `curriculum_classes`
--

DROP TABLE IF EXISTS `curriculum_classes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `curriculum_classes` (
  `center_id` varchar(36) NOT NULL,
  `curriculum_id` varchar(36) NOT NULL,
  `class_id` varchar(36) NOT NULL,
  `assigned_at` datetime(6) NOT NULL,
  `assigned_by` varchar(36) NOT NULL,
  PRIMARY KEY (`center_id`,`curriculum_id`,`class_id`),
  KEY `ix_curriculum_classes_center_id_class_id` (`center_id`,`class_id`),
  CONSTRAINT `fk_curriculum_classes_classes_class` FOREIGN KEY (`center_id`, `class_id`) REFERENCES `classes` (`center_id`, `class_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_curriculum_classes_curriculums_curriculum` FOREIGN KEY (`center_id`, `curriculum_id`) REFERENCES `curriculums` (`center_id`, `curriculum_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `curriculum_nodes`
--

DROP TABLE IF EXISTS `curriculum_nodes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `curriculum_nodes` (
  `center_id` varchar(36) NOT NULL,
  `curriculum_id` varchar(36) NOT NULL,
  `node_id` bigint unsigned NOT NULL,
  `order_index` int unsigned NOT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`center_id`,`curriculum_id`,`node_id`),
  UNIQUE KEY `ux_curriculum_nodes_center_id_curriculum_id_order_index` (`center_id`,`curriculum_id`,`order_index`),
  KEY `ix_curriculum_nodes_center_id_node_id` (`center_id`,`node_id`),
  CONSTRAINT `fk_curriculum_nodes_curriculums_curriculum` FOREIGN KEY (`center_id`, `curriculum_id`) REFERENCES `curriculums` (`center_id`, `curriculum_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_curriculum_nodes_knowledge_nodes_node` FOREIGN KEY (`center_id`, `node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `curriculums`
--

DROP TABLE IF EXISTS `curriculums`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `curriculums` (
  `curriculum_id` varchar(36) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `teacher_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `title` varchar(250) NOT NULL,
  `description` text,
  `source_file` varchar(500) DEFAULT NULL,
  `review_status` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`curriculum_id`),
  UNIQUE KEY `ux_curriculums_center_id_curriculum_id` (`center_id`,`curriculum_id`),
  KEY `ix_curriculums_center_id_subject_id_review_status` (`center_id`,`subject_id`,`review_status`),
  KEY `ix_curriculums_center_id_teacher_id_review_status` (`center_id`,`teacher_id`,`review_status`),
  CONSTRAINT `fk_curriculums_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_curriculums_teachers_teacher` FOREIGN KEY (`center_id`, `teacher_id`) REFERENCES `teachers` (`center_id`, `teacher_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_curriculums_review_status` CHECK ((`review_status` in (_utf8mb4'Draft',_utf8mb4'Published',_utf8mb4'Archived')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `evidence_assessments`
--

DROP TABLE IF EXISTS `evidence_assessments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `evidence_assessments` (
  `evidence_assessment_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `attempt_id` bigint unsigned NOT NULL,
  `analysis_id` bigint unsigned DEFAULT NULL,
  `supersedes_assessment_id` bigint unsigned DEFAULT NULL,
  `source_type` varchar(32) NOT NULL,
  `trust_level` varchar(32) NOT NULL,
  `decision_mode` varchar(32) NOT NULL,
  `reasoning_weight` decimal(4,3) NOT NULL,
  `reason_codes` json NOT NULL,
  `requires_teacher_review` tinyint(1) NOT NULL,
  `policy_version` varchar(32) NOT NULL,
  `analysis_override_version` int unsigned NOT NULL,
  `evaluated_at` datetime(6) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`evidence_assessment_id`),
  UNIQUE KEY `ux_evidence_center_assessment` (`center_id`,`evidence_assessment_id`),
  UNIQUE KEY `ux_evidence_center_assessment_attempt` (`center_id`,`evidence_assessment_id`,`attempt_id`),
  KEY `ix_evidence_center_analysis_attempt` (`center_id`,`analysis_id`,`attempt_id`),
  KEY `ix_evidence_center_attempt_evaluated` (`center_id`,`attempt_id`,`evaluated_at`),
  KEY `ix_evidence_center_review_evaluated` (`center_id`,`requires_teacher_review`,`evaluated_at`),
  KEY `ix_evidence_center_supersedes_attempt` (`center_id`,`supersedes_assessment_id`,`attempt_id`),
  CONSTRAINT `fk_evidence_assessments_attempts_attempt` FOREIGN KEY (`center_id`, `attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_evidence_assessments_evidence_assessments_supersedes` FOREIGN KEY (`center_id`, `supersedes_assessment_id`, `attempt_id`) REFERENCES `evidence_assessments` (`center_id`, `evidence_assessment_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_evidence_assessments_reasoning_analyses_analysis` FOREIGN KEY (`center_id`, `analysis_id`, `attempt_id`) REFERENCES `reasoning_analyses` (`center_id`, `analysis_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_evidence_assessments_decision_mode` CHECK ((`decision_mode` in (_utf8mb4'AIWeighted',_utf8mb4'DeterministicOnly',_utf8mb4'HumanConfirmed'))),
  CONSTRAINT `ck_evidence_assessments_reasoning_weight` CHECK ((`reasoning_weight` between 0 and 1)),
  CONSTRAINT `ck_evidence_assessments_source_type` CHECK ((`source_type` in (_utf8mb4'AI',_utf8mb4'RuleFallback',_utf8mb4'TeacherOverride'))),
  CONSTRAINT `ck_evidence_assessments_trust_level` CHECK ((`trust_level` in (_utf8mb4'Trusted',_utf8mb4'Reduced',_utf8mb4'ReviewOnly')))
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_0900_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`edutwin_user`@`%`*/ /*!50003 TRIGGER `tr_evidence_no_self_supersede` AFTER INSERT ON `evidence_assessments` FOR EACH ROW BEGIN
    IF NEW.supersedes_assessment_id IS NOT NULL
       AND NEW.supersedes_assessment_id = NEW.evidence_assessment_id THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Evidence assessment cannot supersede itself.';
    END IF;
END */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_0900_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`edutwin_user`@`%`*/ /*!50003 TRIGGER `tr_evidence_append_only_update` BEFORE UPDATE ON `evidence_assessments` FOR EACH ROW SIGNAL SQLSTATE '45000'
    SET MESSAGE_TEXT = 'Evidence assessments are append-only and cannot be updated.' */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_0900_ai_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
/*!50003 CREATE*/ /*!50017 DEFINER=`edutwin_user`@`%`*/ /*!50003 TRIGGER `tr_evidence_append_only_delete` BEFORE DELETE ON `evidence_assessments` FOR EACH ROW SIGNAL SQLSTATE '45000'
    SET MESSAGE_TEXT = 'Evidence assessments are append-only and cannot be deleted.' */;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Table structure for table `knowledge_edges`
--

DROP TABLE IF EXISTS `knowledge_edges`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `knowledge_edges` (
  `edge_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `source_node_id` bigint unsigned NOT NULL,
  `target_node_id` bigint unsigned NOT NULL,
  `relation_type` varchar(32) NOT NULL,
  `weight` decimal(5,2) NOT NULL DEFAULT '1.00',
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`edge_id`),
  UNIQUE KEY `ux_knowledge_edges_center_id_edge_id` (`center_id`,`edge_id`),
  UNIQUE KEY `ux_knowledge_edges_center_id_source_id_target_id_relation_type` (`center_id`,`source_node_id`,`target_node_id`,`relation_type`),
  KEY `ix_knowledge_edges_center_id_source_node_id` (`center_id`,`source_node_id`),
  KEY `ix_knowledge_edges_center_id_subject_id` (`center_id`,`subject_id`),
  KEY `ix_knowledge_edges_center_id_target_node_id_relation_type` (`center_id`,`target_node_id`,`relation_type`),
  CONSTRAINT `fk_knowledge_edges_knowledge_nodes_source` FOREIGN KEY (`center_id`, `source_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_edges_knowledge_nodes_target` FOREIGN KEY (`center_id`, `target_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_edges_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_knowledge_edges_relation_type` CHECK ((`relation_type` in (_utf8mb4'PrerequisiteOf',_utf8mb4'RelatedTo',_utf8mb4'PartOf',_utf8mb4'CausesErrorIn'))),
  CONSTRAINT `ck_knowledge_edges_self_loop` CHECK ((`source_node_id` <> `target_node_id`)),
  CONSTRAINT `ck_knowledge_edges_weight` CHECK ((`weight` between 0 and 1))
) ENGINE=InnoDB AUTO_INCREMENT=20004 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `knowledge_nodes`
--

DROP TABLE IF EXISTS `knowledge_nodes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `knowledge_nodes` (
  `node_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `parent_node_id` bigint unsigned DEFAULT NULL,
  `node_type` varchar(32) NOT NULL,
  `node_code` varchar(64) NOT NULL,
  `node_name` varchar(200) NOT NULL,
  `description` text,
  `order_index` int unsigned NOT NULL DEFAULT '0',
  `exam_importance` decimal(5,2) NOT NULL,
  `estimated_learning_minutes` int unsigned NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`node_id`),
  UNIQUE KEY `ux_knowledge_nodes_center_id_node_id` (`center_id`,`node_id`),
  UNIQUE KEY `ux_knowledge_nodes_center_id_subject_id_node_code` (`center_id`,`subject_id`,`node_code`),
  KEY `ix_knowledge_nodes_center_id_parent_node_id` (`center_id`,`parent_node_id`),
  KEY `ix_knowledge_nodes_center_id_subject_id_node_type_order_index` (`center_id`,`subject_id`,`node_type`,`order_index`),
  CONSTRAINT `fk_knowledge_nodes_knowledge_nodes_parent` FOREIGN KEY (`center_id`, `parent_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_nodes_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_knowledge_nodes_estimated_learning_minutes` CHECK ((`estimated_learning_minutes` > 0)),
  CONSTRAINT `ck_knowledge_nodes_exam_importance` CHECK ((`exam_importance` between 0 and 100)),
  CONSTRAINT `ck_knowledge_nodes_node_type` CHECK ((`node_type` in (_utf8mb4'Subject',_utf8mb4'Chapter',_utf8mb4'Topic',_utf8mb4'Skill',_utf8mb4'Concept')))
) ENGINE=InnoDB AUTO_INCREMENT=20006 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `knowledge_twins`
--

DROP TABLE IF EXISTS `knowledge_twins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `knowledge_twins` (
  `knowledge_twin_id` bigint unsigned NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `topic_node_id` bigint unsigned NOT NULL,
  `mastery_percentage` decimal(5,2) NOT NULL DEFAULT '0.00',
  `evidence_count` int unsigned NOT NULL DEFAULT '0',
  `last_reasoning_quality` decimal(5,2) DEFAULT NULL,
  `last_attempt_id` bigint unsigned DEFAULT NULL,
  `last_evidence_at` datetime(6) DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`knowledge_twin_id`),
  UNIQUE KEY `ux_knowledge_twins_center_id_student_id_topic_node_id` (`center_id`,`student_id`,`topic_node_id`),
  KEY `ix_knowledge_twins_center_id_subject_id_mastery_percentage` (`center_id`,`subject_id`,`mastery_percentage`),
  KEY `ix_knowledge_twins_center_id_topic_node_id` (`center_id`,`topic_node_id`),
  KEY `ix_knowledge_twins_center_id_last_attempt_id` (`center_id`,`last_attempt_id`),
  CONSTRAINT `fk_knowledge_twins_attempts_last_attempt` FOREIGN KEY (`center_id`, `last_attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_twins_knowledge_nodes_topic_node` FOREIGN KEY (`center_id`, `topic_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_twins_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_knowledge_twins_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_knowledge_twins_last_reasoning_quality` CHECK (((`last_reasoning_quality` is null) or (`last_reasoning_quality` between 0 and 100))),
  CONSTRAINT `ck_knowledge_twins_mastery_percentage` CHECK ((`mastery_percentage` between 0 and 100))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `learning_path_items`
--

DROP TABLE IF EXISTS `learning_path_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `learning_path_items` (
  `learning_path_item_id` bigint unsigned NOT NULL,
  `learning_path_id` varchar(36) NOT NULL,
  `topic_node_id` bigint unsigned NOT NULL,
  `recommended_question_id` bigint unsigned DEFAULT NULL,
  `rank_order` int unsigned NOT NULL,
  `opportunity_score` decimal(5,2) DEFAULT NULL,
  `reason` varchar(1000) NOT NULL,
  `status` varchar(32) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `calculation_breakdown` json DEFAULT NULL,
  PRIMARY KEY (`learning_path_item_id`),
  UNIQUE KEY `ux_learning_path_items_center_id_learning_path_id_rank_order` (`center_id`,`learning_path_id`,`rank_order`),
  UNIQUE KEY `ux_learning_path_items_center_id_learning_path_id_topic_node_id` (`center_id`,`learning_path_id`,`topic_node_id`),
  KEY `ix_learning_path_items_center_id_recommended_question_id` (`center_id`,`recommended_question_id`),
  KEY `ix_learning_path_items_center_id_topic_node_id` (`center_id`,`topic_node_id`),
  CONSTRAINT `fk_learning_path_items_knowledge_nodes_topic_node` FOREIGN KEY (`center_id`, `topic_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_learning_path_items_learning_paths_learning_path` FOREIGN KEY (`center_id`, `learning_path_id`) REFERENCES `learning_paths` (`center_id`, `learning_path_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_learning_path_items_questions_recommended_question` FOREIGN KEY (`center_id`, `recommended_question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_learning_path_items_opportunity_score` CHECK (((`opportunity_score` is null) or (`opportunity_score` between 0 and 100))),
  CONSTRAINT `ck_learning_path_items_status` CHECK ((`status` in (_utf8mb4'Pending',_utf8mb4'Current',_utf8mb4'Completed',_utf8mb4'Skipped')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `learning_paths`
--

DROP TABLE IF EXISTS `learning_paths`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `learning_paths` (
  `learning_path_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `strategy` varchar(32) NOT NULL,
  `version` int unsigned NOT NULL,
  `status` varchar(32) NOT NULL,
  `generated_from_attempt_id` bigint unsigned DEFAULT NULL,
  `generated_at` datetime(6) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`learning_path_id`),
  UNIQUE KEY `ux_learning_paths_center_id_learning_path_id` (`center_id`,`learning_path_id`),
  KEY `ix_learning_paths_center_id_student_id_subject_id_status` (`center_id`,`student_id`,`subject_id`,`status`),
  KEY `ix_learning_paths_center_id_subject_id` (`center_id`,`subject_id`),
  KEY `ix_learning_paths_center_id_generated_from_attempt_id` (`center_id`,`generated_from_attempt_id`),
  CONSTRAINT `fk_learning_paths_attempts_generated_from_attempt` FOREIGN KEY (`center_id`, `generated_from_attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_learning_paths_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_learning_paths_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_learning_paths_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Superseded',_utf8mb4'Completed'))),
  CONSTRAINT `ck_learning_paths_strategy` CHECK ((`strategy` in (_utf8mb4'LinearFallback',_utf8mb4'OpportunityGap',_utf8mb4'MaintenanceReview')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `permission_account_types`
--

DROP TABLE IF EXISTS `permission_account_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `permission_account_types` (
  `permission_id` varchar(36) NOT NULL,
  `account_type` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`permission_id`,`account_type`),
  KEY `ix_permission_account_types_account_type_permission_id` (`account_type`,`permission_id`),
  CONSTRAINT `fk_permission_account_types_permissions` FOREIGN KEY (`permission_id`) REFERENCES `permissions` (`permission_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_permission_account_types_account_type` CHECK ((`account_type` in (_utf8mb4'Student',_utf8mb4'Teacher',_utf8mb4'CenterManager',_utf8mb4'PlatformAdmin')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `permissions`
--

DROP TABLE IF EXISTS `permissions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `permissions` (
  `permission_id` varchar(36) NOT NULL,
  `permission_code` varchar(100) NOT NULL,
  `module_name` varchar(64) NOT NULL,
  `resource_name` varchar(64) NOT NULL,
  `action_name` varchar(32) NOT NULL,
  `description` varchar(500) NOT NULL,
  `is_sensitive` tinyint(1) NOT NULL,
  `is_delegable` tinyint(1) NOT NULL,
  `status` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `updated_at` datetime(6) NOT NULL,
  PRIMARY KEY (`permission_id`),
  UNIQUE KEY `ux_permissions_permission_code` (`permission_code`),
  KEY `ix_permissions_module_resource_action_status` (`module_name`,`resource_name`,`action_name`,`status`),
  CONSTRAINT `ck_permissions_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Deprecated')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `question_knowledge_nodes`
--

DROP TABLE IF EXISTS `question_knowledge_nodes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `question_knowledge_nodes` (
  `center_id` varchar(36) NOT NULL,
  `question_id` bigint unsigned NOT NULL,
  `node_id` bigint unsigned NOT NULL,
  `mapping_role` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`center_id`,`question_id`,`node_id`,`mapping_role`),
  KEY `ix_question_knowledge_nodes_center_id_node_id` (`center_id`,`node_id`),
  CONSTRAINT `fk_question_knowledge_nodes_knowledge_nodes_node` FOREIGN KEY (`center_id`, `node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_question_knowledge_nodes_questions_question` FOREIGN KEY (`center_id`, `question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_question_knowledge_nodes_mapping_role` CHECK ((`mapping_role` in (_utf8mb4'Primary',_utf8mb4'Secondary',_utf8mb4'Prerequisite')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `question_options`
--

DROP TABLE IF EXISTS `question_options`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `question_options` (
  `option_id` bigint unsigned NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `question_id` bigint unsigned NOT NULL,
  `option_label` varchar(8) NOT NULL,
  `option_text` text NOT NULL,
  `is_correct` tinyint(1) NOT NULL,
  `order_index` int unsigned NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`option_id`),
  UNIQUE KEY `ux_question_options_center_id_question_id_option_label` (`center_id`,`question_id`,`option_label`),
  UNIQUE KEY `ux_question_options_center_id_question_id_order_index` (`center_id`,`question_id`,`order_index`),
  CONSTRAINT `fk_question_options_questions_question` FOREIGN KEY (`center_id`, `question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `questions`
--

DROP TABLE IF EXISTS `questions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `questions` (
  `question_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `primary_topic_node_id` bigint unsigned NOT NULL,
  `created_by_teacher_id` varchar(36) NOT NULL,
  `question_type` varchar(32) NOT NULL,
  `difficulty` tinyint unsigned NOT NULL,
  `question_text` longtext NOT NULL,
  `correct_answer` text NOT NULL,
  `solution` longtext NOT NULL,
  `expected_reasoning` longtext,
  `grading_criteria` json NOT NULL,
  `max_score` decimal(5,2) NOT NULL DEFAULT '1.00',
  `estimated_time_seconds` int unsigned NOT NULL,
  `reasoning_required` tinyint(1) NOT NULL DEFAULT '1',
  `language_code` varchar(8) NOT NULL,
  `status` varchar(32) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `answer_evaluation_mode` varchar(32) NOT NULL DEFAULT 'TextExact',
  PRIMARY KEY (`question_id`),
  UNIQUE KEY `ux_questions_center_id_question_id` (`center_id`,`question_id`),
  KEY `ix_questions_center_id_created_by_teacher_id_status` (`center_id`,`created_by_teacher_id`,`status`),
  KEY `ix_questions_center_id_primary_topic_node_id` (`center_id`,`primary_topic_node_id`),
  KEY `ix_questions_center_id_subject_id_topic_id_status_difficulty` (`center_id`,`subject_id`,`primary_topic_node_id`,`status`,`difficulty`),
  CONSTRAINT `fk_questions_knowledge_nodes_primary_topic` FOREIGN KEY (`center_id`, `primary_topic_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_questions_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_questions_teachers_created_by_teacher` FOREIGN KEY (`center_id`, `created_by_teacher_id`) REFERENCES `teachers` (`center_id`, `teacher_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_questions_answer_evaluation_mode` CHECK ((`answer_evaluation_mode` in (_utf8mb4'TextExact',_utf8mb4'NumericRational',_utf8mb4'Manual'))),
  CONSTRAINT `ck_questions_difficulty` CHECK ((`difficulty` between 1 and 5)),
  CONSTRAINT `ck_questions_estimated_time_seconds` CHECK ((`estimated_time_seconds` > 0)),
  CONSTRAINT `ck_questions_language_code` CHECK ((`language_code` in (_utf8mb4'vi',_utf8mb4'en'))),
  CONSTRAINT `ck_questions_max_score` CHECK ((`max_score` > 0)),
  CONSTRAINT `ck_questions_question_type` CHECK ((`question_type` in (_utf8mb4'MultipleChoice',_utf8mb4'ShortAnswer',_utf8mb4'Essay'))),
  CONSTRAINT `ck_questions_status` CHECK ((`status` in (_utf8mb4'Draft',_utf8mb4'Active',_utf8mb4'Archived')))
) ENGINE=InnoDB AUTO_INCREMENT=20030 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `reasoning_analyses`
--

DROP TABLE IF EXISTS `reasoning_analyses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reasoning_analyses` (
  `analysis_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `center_id` varchar(36) NOT NULL,
  `attempt_id` bigint unsigned NOT NULL,
  `schema_version` varchar(20) NOT NULL,
  `method_detected` varchar(500) DEFAULT NULL,
  `reasoning_quality` decimal(5,2) DEFAULT NULL,
  `error_type` varchar(32) NOT NULL,
  `misconception` varchar(1000) DEFAULT NULL,
  `missing_steps` json NOT NULL,
  `root_cause_node_ids` json NOT NULL,
  `analysis_confidence` decimal(5,2) DEFAULT NULL,
  `feedback` longtext NOT NULL,
  `is_fallback` tinyint(1) NOT NULL,
  `needs_teacher_review` tinyint(1) NOT NULL,
  `provider` varchar(32) NOT NULL,
  `model_name` varchar(100) DEFAULT NULL,
  `override_reasoning_quality` decimal(5,2) DEFAULT NULL,
  `override_error_type` varchar(32) DEFAULT NULL,
  `override_feedback` longtext,
  `override_is_correct` tinyint(1) DEFAULT NULL,
  `override_reason` varchar(1000) DEFAULT NULL,
  `overridden_by_user_id` varchar(36) DEFAULT NULL,
  `overridden_at` datetime(6) DEFAULT NULL,
  `override_version` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `override_awarded_score` decimal(5,2) DEFAULT NULL,
  PRIMARY KEY (`analysis_id`),
  UNIQUE KEY `ux_reasoning_analyses_center_id_analysis_id` (`center_id`,`analysis_id`),
  UNIQUE KEY `ux_reasoning_analyses_center_id_attempt_id` (`center_id`,`attempt_id`),
  UNIQUE KEY `ux_reasoning_analyses_center_id_analysis_id_attempt_id` (`center_id`,`analysis_id`,`attempt_id`),
  KEY `ix_reasoning_analyses_center_id_needs_teacher_review_created_at` (`center_id`,`needs_teacher_review`,`created_at`),
  KEY `ix_reasoning_analyses_center_id_overridden_by_user_id` (`center_id`,`overridden_by_user_id`),
  CONSTRAINT `fk_reasoning_analyses_attempts_attempt` FOREIGN KEY (`center_id`, `attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_reasoning_analyses_users_overridden_by_user` FOREIGN KEY (`center_id`, `overridden_by_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_reasoning_analyses_analysis_confidence` CHECK (((`analysis_confidence` is null) or (`analysis_confidence` between 0 and 100))),
  CONSTRAINT `ck_reasoning_analyses_error_type` CHECK ((`error_type` in (_utf8mb4'None',_utf8mb4'Knowledge',_utf8mb4'Skill',_utf8mb4'Reasoning',_utf8mb4'Behavior',_utf8mb4'Presentation',_utf8mb4'Unknown'))),
  CONSTRAINT `ck_reasoning_analyses_override_awarded_score` CHECK (((`override_awarded_score` is null) or (`override_awarded_score` >= 0))),
  CONSTRAINT `ck_reasoning_analyses_override_error_type` CHECK (((`override_error_type` is null) or (`override_error_type` in (_utf8mb4'None',_utf8mb4'Knowledge',_utf8mb4'Skill',_utf8mb4'Reasoning',_utf8mb4'Behavior',_utf8mb4'Presentation',_utf8mb4'Unknown')))),
  CONSTRAINT `ck_reasoning_analyses_override_reasoning_quality` CHECK (((`override_reasoning_quality` is null) or (`override_reasoning_quality` between 0 and 100))),
  CONSTRAINT `ck_reasoning_analyses_provider` CHECK ((`provider` in (_utf8mb4'Gemini',_utf8mb4'RuleBased'))),
  CONSTRAINT `ck_reasoning_analyses_reasoning_quality` CHECK (((`reasoning_quality` is null) or (`reasoning_quality` between 0 and 100)))
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `recommendation_generation_states`
--

DROP TABLE IF EXISTS `recommendation_generation_states`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `recommendation_generation_states` (
  `center_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `last_trigger_at` datetime(6) NOT NULL,
  `last_source_attempt_id` bigint unsigned DEFAULT NULL,
  `last_outcome` varchar(32) NOT NULL,
  `diagnostic_reason` varchar(500) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL,
  `updated_at` datetime(6) NOT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`center_id`,`student_id`,`subject_id`),
  KEY `ix_recommendation_generation_states_center_id_subject_id` (`center_id`,`subject_id`),
  CONSTRAINT `fk_recommendation_generation_states_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_recommendation_generation_states_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_recommendation_generation_states_outcome` CHECK ((`last_outcome` in (_utf8mb4'Generated',_utf8mb4'NoCandidate',_utf8mb4'Blocked')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `recommendations`
--

DROP TABLE IF EXISTS `recommendations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `recommendations` (
  `recommendation_id` bigint unsigned NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `topic_node_id` bigint unsigned NOT NULL,
  `question_id` bigint unsigned DEFAULT NULL,
  `recommendation_type` varchar(32) NOT NULL,
  `opportunity_score` decimal(5,2) DEFAULT NULL,
  `calculation_version` varchar(20) NOT NULL,
  `calculation_breakdown` json NOT NULL,
  `explanation` varchar(1000) NOT NULL,
  `source_attempt_id` bigint unsigned DEFAULT NULL,
  `status` varchar(32) NOT NULL,
  `generated_at` datetime(6) NOT NULL,
  `expires_at` datetime(6) DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  `dismiss_reason` varchar(1000) DEFAULT NULL,
  PRIMARY KEY (`recommendation_id`),
  KEY `ix_recommendations_center_id_question_id` (`center_id`,`question_id`),
  KEY `ix_recommendations_center_id_subject_id` (`center_id`,`subject_id`),
  KEY `ix_recommendations_center_id_topic_node_id` (`center_id`,`topic_node_id`),
  KEY `ix_recommendations_center_student_subject_status_generated_at` (`center_id`,`student_id`,`subject_id`,`status`,`generated_at`),
  KEY `ix_recommendations_center_id_source_attempt_id` (`center_id`,`source_attempt_id`),
  CONSTRAINT `fk_recommendations_attempts_source_attempt` FOREIGN KEY (`center_id`, `source_attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_recommendations_knowledge_nodes_topic_node` FOREIGN KEY (`center_id`, `topic_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_recommendations_questions_question` FOREIGN KEY (`center_id`, `question_id`) REFERENCES `questions` (`center_id`, `question_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_recommendations_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_recommendations_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_recommendations_opportunity_score` CHECK (((`opportunity_score` is null) or (`opportunity_score` between 0 and 100))),
  CONSTRAINT `ck_recommendations_recommendation_type` CHECK ((`recommendation_type` in (_utf8mb4'TopicAndQuestion',_utf8mb4'LinearFallback'))),
  CONSTRAINT `ck_recommendations_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Accepted',_utf8mb4'Dismissed',_utf8mb4'Superseded')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `refresh_tokens`
--

DROP TABLE IF EXISTS `refresh_tokens`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `refresh_tokens` (
  `refresh_token_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` varchar(36) NOT NULL,
  `token_hash` char(64) NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `revoked_at` datetime(6) DEFAULT NULL,
  `replaced_by_token_id` bigint unsigned DEFAULT NULL,
  `revoke_reason` varchar(200) DEFAULT NULL,
  `created_by_ip` varchar(64) DEFAULT NULL,
  `revoked_by_ip` varchar(64) DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`refresh_token_id`),
  UNIQUE KEY `ux_refresh_tokens_center_id_refresh_token_id` (`center_id`,`refresh_token_id`),
  UNIQUE KEY `ux_refresh_tokens_token_hash` (`token_hash`),
  KEY `ix_refresh_tokens_center_id_replaced_by_token_id` (`center_id`,`replaced_by_token_id`),
  KEY `ix_refresh_tokens_center_id_user_id_expires_at` (`center_id`,`user_id`,`expires_at`),
  CONSTRAINT `fk_refresh_tokens_refresh_tokens_replaced_by` FOREIGN KEY (`center_id`, `replaced_by_token_id`) REFERENCES `refresh_tokens` (`center_id`, `refresh_token_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_refresh_tokens_users_tenant` FOREIGN KEY (`center_id`, `user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=107 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `role_permissions`
--

DROP TABLE IF EXISTS `role_permissions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `role_permissions` (
  `center_id` varchar(36) NOT NULL,
  `role_id` varchar(36) NOT NULL,
  `permission_id` varchar(36) NOT NULL,
  `account_type` varchar(32) NOT NULL,
  `granted_at` datetime(6) NOT NULL,
  `granted_by_user_id` varchar(36) NOT NULL,
  PRIMARY KEY (`center_id`,`role_id`,`permission_id`),
  KEY `ix_role_permissions_center_granted_by_user` (`center_id`,`granted_by_user_id`),
  KEY `ix_role_permissions_center_permission_role` (`center_id`,`permission_id`,`role_id`),
  KEY `ix_role_permissions_center_role_account_type` (`center_id`,`role_id`,`account_type`),
  KEY `ix_role_permissions_permission_account_type` (`permission_id`,`account_type`),
  CONSTRAINT `fk_role_permissions_permission_account_types` FOREIGN KEY (`permission_id`, `account_type`) REFERENCES `permission_account_types` (`permission_id`, `account_type`) ON DELETE RESTRICT,
  CONSTRAINT `fk_role_permissions_roles_account_type` FOREIGN KEY (`center_id`, `role_id`, `account_type`) REFERENCES `roles` (`center_id`, `role_id`, `account_type`) ON DELETE RESTRICT,
  CONSTRAINT `fk_role_permissions_users_granted_by` FOREIGN KEY (`center_id`, `granted_by_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_role_permissions_account_type` CHECK ((`account_type` in (_utf8mb4'Student',_utf8mb4'Teacher',_utf8mb4'CenterManager',_utf8mb4'PlatformAdmin')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `roles`
--

DROP TABLE IF EXISTS `roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `roles` (
  `role_id` varchar(36) NOT NULL,
  `role_code` varchar(64) NOT NULL,
  `role_name` varchar(150) NOT NULL,
  `account_type` varchar(32) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `is_system_role` tinyint(1) NOT NULL,
  `status` varchar(32) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `ux_roles_center_id_role_id` (`center_id`,`role_id`),
  UNIQUE KEY `ux_roles_center_id_role_id_account_type` (`center_id`,`role_id`,`account_type`),
  UNIQUE KEY `ux_roles_center_id_role_code` (`center_id`,`role_code`),
  KEY `ix_roles_center_account_type_status_role_name` (`center_id`,`account_type`,`status`,`role_name`),
  CONSTRAINT `fk_roles_centers_tenant` FOREIGN KEY (`center_id`) REFERENCES `centers` (`center_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_roles_account_type` CHECK ((`account_type` in (_utf8mb4'Student',_utf8mb4'Teacher',_utf8mb4'CenterManager',_utf8mb4'PlatformAdmin'))),
  CONSTRAINT `ck_roles_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Archived')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `student_assignment_progress`
--

DROP TABLE IF EXISTS `student_assignment_progress`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `student_assignment_progress` (
  `progress_id` bigint unsigned NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `assignment_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `status` varchar(32) NOT NULL,
  `completed_question_count` int unsigned NOT NULL DEFAULT '0',
  `total_question_count` int unsigned NOT NULL,
  `started_at` datetime(6) DEFAULT NULL,
  `completed_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`progress_id`),
  UNIQUE KEY `ux_student_assignment_progress_center_assignment_id_student_id` (`center_id`,`assignment_id`,`student_id`),
  KEY `ix_student_assignment_progress_center_id_student_id_status` (`center_id`,`student_id`,`status`),
  CONSTRAINT `fk_student_assignment_progress_assignments_assignment` FOREIGN KEY (`center_id`, `assignment_id`) REFERENCES `assignments` (`center_id`, `assignment_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_student_assignment_progress_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_student_assignment_progress_counts` CHECK ((`completed_question_count` <= `total_question_count`)),
  CONSTRAINT `ck_student_assignment_progress_status` CHECK ((`status` in (_utf8mb4'NotStarted',_utf8mb4'InProgress',_utf8mb4'Completed',_utf8mb4'Overdue')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `student_subject_goals`
--

DROP TABLE IF EXISTS `student_subject_goals`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `student_subject_goals` (
  `goal_id` bigint unsigned NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `target_score` decimal(4,2) NOT NULL,
  `remaining_days` int unsigned NOT NULL,
  `current_predicted_score` decimal(4,2) NOT NULL DEFAULT '0.00',
  `risk_score` decimal(5,2) NOT NULL DEFAULT '0.00',
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`goal_id`),
  UNIQUE KEY `ux_student_subject_goals_center_id_student_id_subject_id` (`center_id`,`student_id`,`subject_id`),
  KEY `ix_student_subject_goals_center_id_subject_id_risk_score` (`center_id`,`subject_id`,`risk_score`),
  CONSTRAINT `fk_student_subject_goals_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_student_subject_goals_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_student_subject_goals_current_predicted_score` CHECK ((`current_predicted_score` between 0 and 10)),
  CONSTRAINT `ck_student_subject_goals_remaining_days` CHECK ((`remaining_days` <= 3650)),
  CONSTRAINT `ck_student_subject_goals_risk_score` CHECK ((`risk_score` between 0 and 100)),
  CONSTRAINT `ck_student_subject_goals_target_score` CHECK ((`target_score` between 0 and 10))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `student_twins`
--

DROP TABLE IF EXISTS `student_twins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `student_twins` (
  `twin_id` varchar(36) NOT NULL,
  `student_id` varchar(36) NOT NULL,
  `overall_mastery` decimal(5,2) NOT NULL,
  `last_evidence_at` datetime(6) DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`twin_id`),
  UNIQUE KEY `ux_student_twins_center_id_student_id` (`center_id`,`student_id`),
  CONSTRAINT `fk_student_twins_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_student_twins_overall_mastery` CHECK ((`overall_mastery` between 0 and 100))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `students`
--

DROP TABLE IF EXISTS `students`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `students` (
  `student_id` varchar(36) NOT NULL,
  `full_name` varchar(200) NOT NULL,
  `grade_level` tinyint unsigned NOT NULL,
  `date_of_birth` date DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`student_id`),
  UNIQUE KEY `ux_students_center_id_student_id` (`center_id`,`student_id`),
  KEY `ix_students_center_id_grade_level` (`center_id`,`grade_level`),
  CONSTRAINT `fk_students_users_profile` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_students_grade_level` CHECK ((`grade_level` between 10 and 12))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `subjects`
--

DROP TABLE IF EXISTS `subjects`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `subjects` (
  `subject_id` varchar(36) NOT NULL,
  `subject_code` varchar(32) NOT NULL,
  `subject_name` varchar(100) NOT NULL,
  `description` varchar(500) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`subject_id`),
  UNIQUE KEY `ux_subjects_center_id_subject_id` (`center_id`,`subject_id`),
  UNIQUE KEY `ux_subjects_center_id_subject_code` (`center_id`,`subject_code`),
  CONSTRAINT `fk_subjects_centers_tenant` FOREIGN KEY (`center_id`) REFERENCES `centers` (`center_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `teachers`
--

DROP TABLE IF EXISTS `teachers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `teachers` (
  `teacher_id` varchar(36) NOT NULL,
  `department` varchar(150) DEFAULT NULL,
  `bio` varchar(500) DEFAULT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`teacher_id`),
  UNIQUE KEY `ux_teachers_center_id_teacher_id` (`center_id`,`teacher_id`),
  CONSTRAINT `fk_teachers_users_profile` FOREIGN KEY (`center_id`, `teacher_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `twin_update_history`
--

DROP TABLE IF EXISTS `twin_update_history`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `twin_update_history` (
  `history_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `student_id` varchar(36) NOT NULL,
  `subject_id` varchar(36) NOT NULL,
  `topic_node_id` bigint unsigned NOT NULL,
  `attempt_id` bigint unsigned DEFAULT NULL,
  `analysis_id` bigint unsigned DEFAULT NULL,
  `event_source` varchar(32) NOT NULL,
  `previous_mastery` decimal(5,2) NOT NULL,
  `new_mastery` decimal(5,2) NOT NULL,
  `mastery_delta` decimal(6,2) NOT NULL,
  `effective_reasoning_quality` decimal(5,2) DEFAULT NULL,
  `calculation_version` varchar(20) NOT NULL,
  `calculation_breakdown` json NOT NULL,
  `explanation` varchar(1000) NOT NULL,
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  PRIMARY KEY (`history_id`),
  KEY `ix_twin_update_history_center_id_attempt_id` (`center_id`,`attempt_id`),
  KEY `ix_twin_update_history_center_id_subject_id` (`center_id`,`subject_id`),
  KEY `ix_twin_update_history_center_id_topic_node_id_created_at` (`center_id`,`topic_node_id`,`created_at`),
  KEY `ix_twin_update_history_center_student_subject_created_at` (`center_id`,`student_id`,`subject_id`,`created_at`),
  KEY `ix_twin_update_history_center_id_analysis_id` (`center_id`,`analysis_id`),
  CONSTRAINT `fk_twin_update_history_attempts_attempt` FOREIGN KEY (`center_id`, `attempt_id`) REFERENCES `attempts` (`center_id`, `attempt_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_twin_update_history_knowledge_nodes_topic_node` FOREIGN KEY (`center_id`, `topic_node_id`) REFERENCES `knowledge_nodes` (`center_id`, `node_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_twin_update_history_reasoning_analyses_analysis` FOREIGN KEY (`center_id`, `analysis_id`) REFERENCES `reasoning_analyses` (`center_id`, `analysis_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_twin_update_history_students_student` FOREIGN KEY (`center_id`, `student_id`) REFERENCES `students` (`center_id`, `student_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_twin_update_history_subjects_subject` FOREIGN KEY (`center_id`, `subject_id`) REFERENCES `subjects` (`center_id`, `subject_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_twin_update_history_effective_reasoning_quality` CHECK (((`effective_reasoning_quality` is null) or (`effective_reasoning_quality` between 0 and 100))),
  CONSTRAINT `ck_twin_update_history_event_source` CHECK ((`event_source` in (_utf8mb4'AIAnalysis',_utf8mb4'RuleFallback',_utf8mb4'TeacherOverride',_utf8mb4'Replay'))),
  CONSTRAINT `ck_twin_update_history_new_mastery` CHECK ((`new_mastery` between 0 and 100)),
  CONSTRAINT `ck_twin_update_history_previous_mastery` CHECK ((`previous_mastery` between 0 and 100))
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `user_roles`
--

DROP TABLE IF EXISTS `user_roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `user_roles` (
  `center_id` varchar(36) NOT NULL,
  `user_id` varchar(36) NOT NULL,
  `role_id` varchar(36) NOT NULL,
  `account_type` varchar(32) NOT NULL,
  `status` varchar(32) NOT NULL,
  `assigned_at` datetime(6) NOT NULL,
  `assigned_by_user_id` varchar(36) NOT NULL,
  `revoked_at` datetime(6) DEFAULT NULL,
  `revoked_by_user_id` varchar(36) DEFAULT NULL,
  `revoke_reason` varchar(500) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`center_id`,`user_id`,`role_id`),
  KEY `ix_user_roles_center_assigned_by_user` (`center_id`,`assigned_by_user_id`),
  KEY `ix_user_roles_center_revoked_by_user` (`center_id`,`revoked_by_user_id`),
  KEY `ix_user_roles_center_role_account_type` (`center_id`,`role_id`,`account_type`),
  KEY `ix_user_roles_center_role_status_user` (`center_id`,`role_id`,`status`,`user_id`),
  KEY `ix_user_roles_center_user_account_type` (`center_id`,`user_id`,`account_type`),
  KEY `ix_user_roles_center_user_status` (`center_id`,`user_id`,`status`),
  CONSTRAINT `fk_user_roles_roles_account_type` FOREIGN KEY (`center_id`, `role_id`, `account_type`) REFERENCES `roles` (`center_id`, `role_id`, `account_type`) ON DELETE RESTRICT,
  CONSTRAINT `fk_user_roles_users_account_type` FOREIGN KEY (`center_id`, `user_id`, `account_type`) REFERENCES `users` (`center_id`, `user_id`, `role_name`) ON DELETE RESTRICT,
  CONSTRAINT `fk_user_roles_users_assigned_by` FOREIGN KEY (`center_id`, `assigned_by_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_user_roles_users_revoked_by` FOREIGN KEY (`center_id`, `revoked_by_user_id`) REFERENCES `users` (`center_id`, `user_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_user_roles_account_type` CHECK ((`account_type` in (_utf8mb4'Student',_utf8mb4'Teacher',_utf8mb4'CenterManager',_utf8mb4'PlatformAdmin'))),
  CONSTRAINT `ck_user_roles_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Revoked')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `user_id` varchar(36) NOT NULL,
  `username` varchar(100) NOT NULL,
  `password_hash` varchar(500) NOT NULL,
  `role_name` varchar(32) NOT NULL,
  `display_name` varchar(200) NOT NULL,
  `status` varchar(32) NOT NULL,
  `last_login_at` datetime(6) DEFAULT NULL,
  `auth_version` int unsigned NOT NULL DEFAULT '1',
  `center_id` varchar(36) NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `created_by` varchar(36) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  `updated_by` varchar(36) DEFAULT NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT '0',
  `deleted_at` datetime(6) DEFAULT NULL,
  `deleted_by` varchar(36) DEFAULT NULL,
  `row_version` bigint unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `ux_users_center_id_user_id` (`center_id`,`user_id`),
  UNIQUE KEY `ux_users_center_id_username` (`center_id`,`username`),
  UNIQUE KEY `ux_users_center_id_user_id_role_name` (`center_id`,`user_id`,`role_name`),
  KEY `ix_users_center_id_role_name_status` (`center_id`,`role_name`,`status`),
  CONSTRAINT `fk_users_centers_tenant` FOREIGN KEY (`center_id`) REFERENCES `centers` (`center_id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_users_role_name` CHECK ((`role_name` in (_utf8mb4'Student',_utf8mb4'Teacher',_utf8mb4'CenterManager',_utf8mb4'PlatformAdmin'))),
  CONSTRAINT `ck_users_status` CHECK ((`status` in (_utf8mb4'Active',_utf8mb4'Locked',_utf8mb4'Disabled')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-09-14  6:27:30
