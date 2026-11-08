const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {
  BASE_URL,
  validateManifest,
  validateOwner,
  roleAccount
} = require('./safety.cjs');
const acceptance = require('../../../scripts/e2e/acceptance_stack.cjs');
const {
  docker
} = require('../../../scripts/ops/mysql_admin.cjs');
const fixtures = require('../../../scripts/e2e/fixture_manager.cjs');
function verify(baseUrl = BASE_URL) {
  const state = validateManifest(acceptance.manifest(), baseUrl);
  const labels = Object.fromEntries([state.api, state.web].map(name => [name, docker(['inspect', name, '--format', '{{index .Config.Labels "edutwin.e2e.owner"}}']).trim()]));
  validateOwner(state, labels);
  return state;
}
async function login(role) {
  verify();
  const account = roleAccount(role);
  const response = await fetch(`${BASE_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json'
    },
    body: JSON.stringify({
      ...account,
      password: acceptance.testPassword
    }),
    signal: AbortSignal.timeout(15000)
  });
  assert.equal(response.status, 200, `Disposable ${role} login failed (${response.status}); no credentials logged.`);
  const body = await response.json();
  assert.ok(body.data?.accessToken, 'Disposable login returned no access token.');
  const claims = JSON.parse(Buffer.from(body.data.accessToken.split('.')[1], 'base64url').toString());
  assert.equal(claims.iss, 'edutwin-acceptance', 'Refusing token from a primary runtime.');
  return body.data.accessToken;
}
function register(on, config) {
  on('before:run', () => {
    verify(config.baseUrl);
  });
  on('task', {
    'isolated:verify'() {
      const state = verify(config.baseUrl);
      return {
        runId: state.runId
      };
    },
    'isolated:protectedRoutes'() {
      verify(config.baseUrl);
      return require('./route-surface.cjs').protectedRoutes(path.resolve(__dirname, '../../..'));
    },
    'isolated:login': login,
    'isolated:credentials'(role) {
      verify(config.baseUrl);
      return {
        ...roleAccount(role),
        password: acceptance.testPassword
      };
    },
    'isolated:png'() {
      verify(config.baseUrl);
      return require('./fixtures.cjs').png().toString('base64');
    },
    'isolated:assignment'({
      title = 'Validation fixture'
    } = {}) {
      verify(config.baseUrl);
      assert.equal(typeof title, 'string');
      assert.ok(title.length <= 180);
      return fixtures.createAssignmentFixture({
        tag: 'CYPRESS',
        title,
        questionConfigs: [{
          questionId: 10000,
          points: 1
        }, {
          questionId: 10006,
          points: 1
        }]
      });
    },
    async 'isolated:submittedAssignment'() {
      verify(config.baseUrl);
      const fixture = fixtures.createAssignmentFixture({
        tag: 'UI-REVIEW',
        title: 'UI validation review fixture',
        questionConfigs: [{
          questionId: 10000,
          points: 1
        }, {
          questionId: 10006,
          points: 1
        }]
      });
      const token = await login('student');
      const send = (route, options = {}) => fetch(`${BASE_URL}/api/v1${route}`, {
        ...options,
        headers: {
          authorization: `Bearer ${token}`,
          'content-type': 'application/json'
        },
        signal: AbortSignal.timeout(15000)
      });
      const started = await send(`/students/me/assignments/${fixture.assignmentId}/start`, {
        method: 'POST',
        body: '{}'
      });
      assert.equal(started.status, 200, 'Review fixture assignment must be started.');
      const response = await send(`/students/me/assignments/${fixture.assignmentId}/submit`, {
        method: 'POST',
        body: JSON.stringify({
          answers: [10000, 10006].map(questionId => ({
            questionId: String(questionId),
            finalAnswer: '2',
            reasoningText: 'Một cộng một bằng hai.',
            confidence: 80,
            timeSpentSeconds: 20,
            answerChanges: 0
          }))
        })
      });
      assert.equal(response.status, 200, 'Real review fixture submission failed.');
      const result = await response.json();
      assert.ok(result.lastAnalysisJobId);
      for (let n = 0; n < 50; n++) {
        const statusResponse = await send(`/learning/analysis-jobs/${result.lastAnalysisJobId}`);
        assert.equal(statusResponse.status, 200);
        const status = (await statusResponse.json()).data;
        if (['Completed', 'FallbackCompleted'].includes(status.status)) return {
          assignmentId: fixture.assignmentId,
          studentId: 'd0000000-0000-0000-0001-000000000004'
        };
        assert.notEqual(status.status, 'FailedTerminal', 'Review fixture must have actual terminal feedback.');
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      throw new Error('Review fixture worker completion timed out.');
    },
    async 'isolated:reasoningAssignment'() {
      verify(config.baseUrl);
      const scopeJson = fixtures.execSql(`SELECT JSON_OBJECT('classId',c.class_id,'subjectId',c.subject_id,'teacherId',c.teacher_id,'grade',c.grade_level,'nodeId',CAST(n.node_id AS CHAR))
        FROM classes c JOIN class_students cs ON cs.center_id=c.center_id AND cs.class_id=c.class_id
        JOIN users u ON u.center_id=c.center_id AND u.user_id=c.teacher_id
        JOIN knowledge_graph_versions v ON v.center_id=c.center_id AND v.subject_id=c.subject_id AND v.grade_level=c.grade_level AND v.is_current=1
        JOIN knowledge_node_grades ng ON ng.center_id=v.center_id AND ng.graph_version_id=v.graph_version_id
        JOIN knowledge_nodes n ON n.center_id=ng.center_id AND n.node_id=ng.node_id
        WHERE c.center_id='10000000-0000-0000-0000-000000000001' AND cs.student_id='d0000000-0000-0000-0001-000000000004'
        AND u.username='teacher.english' AND c.status='Active' AND c.is_deleted=0 AND n.node_type='Topic' AND n.is_active=1 AND n.is_deleted=0
        ORDER BY c.class_id,n.node_id LIMIT 1;`).trim();
      assert.ok(scopeJson, 'An approved English class/topic fixture is required.');
      const scope = JSON.parse(scopeJson),
        token = await login('englishTeacher');
      const post = async (route, body, expected) => {
        const r = await fetch(`${BASE_URL}/api/v1${route}`, {
          method: 'POST',
          headers: {
            authorization: `Bearer ${token}`,
            'content-type': 'application/json'
          },
          body: JSON.stringify(body),
          signal: AbortSignal.timeout(15000)
        });
        assert.equal(r.status, expected, 'Authored reasoning fixture operation failed.');
        return r.json();
      };
      const authored = await post('/questions', {
        subjectId: scope.subjectId,
        primaryTopicNodeId: scope.nodeId,
        gradeLevel: scope.grade,
        visibility: 'Private',
        questionType: 'ShortAnswer',
        difficulty: 1,
        questionText: 'Choose the correct verb: She ___ to school.',
        correctAnswer: 'goes',
        solution: 'Chủ ngữ she dùng goes ở thì hiện tại đơn.',
        maxScore: 1,
        estimatedTimeSeconds: 60,
        reasoningRequired: true,
        languageCode: 'en',
        answerEvaluationMode: 'TextExact',
        options: [],
        knowledgeMappings: []
      }, 201);
      const q = authored.data;
      await post(`/questions/${q.questionId}/activate`, {
        rowVersion: q.rowVersion
      }, 200);
      return fixtures.createAssignmentFixture({
        tag: 'UI-REASONING',
        title: 'Required reasoning UI',
        classId: scope.classId,
        teacherId: scope.teacherId,
        questionConfigs: [{
          questionId: Number(q.questionId),
          points: 1
        }]
      });
    },
    async 'isolated:replyNotification'() {
      verify(config.baseUrl);
      const token = await login('teacher');
      const title = `UI reply validation ${require('node:crypto').randomUUID()}`;
      const draft = {
        title,
        body: 'Thông báo tổng hợp dùng kiểm tra phản hồi.',
        audienceKind: 'SelectedStudents',
        targetIds: ['d0000000-0000-0000-0001-000000000004'],
        priority: 'Normal',
        category: 'Information',
        allowReply: true
      };
      const post = async (route, body) => {
        const r = await fetch(`${BASE_URL}/api/v1${route}`, {
          method: 'POST',
          headers: {
            authorization: `Bearer ${token}`,
            'content-type': 'application/json'
          },
          body: JSON.stringify(body),
          signal: AbortSignal.timeout(15000)
        });
        assert.equal(r.status, 200, 'Notification fixture operation failed.');
        return r.json();
      };
      const preview = await post('/notifications/preview', draft);
      const sent = await post('/notifications/send', {
        ...draft,
        previewToken: preview.data.token,
        clientMessageId: require('node:crypto').randomUUID()
      });
      const student = await login('student');
      // Other specs also publish business events. Wait for this exact delivery,
      // allowing the real worker to drain earlier events instead of faking an inbox row.
      const deadline = Date.now() + 90000;
      while (Date.now() < deadline) {
        const r = await fetch(`${BASE_URL}/api/v1/notifications?pageSize=100`, {
          headers: {
            authorization: `Bearer ${student}`
          },
          signal: AbortSignal.timeout(10000)
        });
        assert.equal(r.status, 200);
        const data = await r.json();
        const delivery = data.data.find(item => item.title === title);
        if (delivery) return {
          messageId: sent.data.messageId,
          deliveryId: delivery.deliveryId
        };
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      throw new Error('Notification fixture was not delivered by the worker.');
    },
    'isolated:deadLetterNotification'() {
      verify(config.baseUrl);
      const eventId = require('node:crypto').randomUUID();
      // Represent a failed dispatch only for the recovery form's input controls.
      // This fixture does not assert the worker's retry/dead-letter transitions.
      fixtures.execSql(`START TRANSACTION;
        INSERT INTO notification_events(event_id,center_id,deduplication_key,event_type,title,body,recipient_user_ids,created_at)
        VALUES('${eventId}','10000000-0000-0000-0000-000000000001','ui-redrive:${eventId}','SystemNotice',
          'UI recovery fixture','Synthetic recovery form input',JSON_ARRAY('d0000000-0000-0000-0001-000000000004'),UTC_TIMESTAMP());
        INSERT INTO notification_dispatches(event_id,center_id,state,available_at,attempt_count,delivered_count,suppressed_count,last_error_code,row_version)
        VALUES('${eventId}','10000000-0000-0000-0000-000000000001','DeadLetter',UTC_TIMESTAMP(),5,0,0,'DISPATCH_FAILED',1);
        COMMIT;`);
      return {
        eventId
      };
    },
    'isolated:count'(domain) {
      verify(config.baseUrl);
      const tables = {
        questions: 'questions',
        classes: 'classes',
        students: 'students',
        teachers: 'teachers',
        attempts: 'attempts',
        curriculums: 'curriculums',
        nodes: 'knowledge_nodes',
        adaptiveSessions: 'adaptive_practice_sessions'
      };
      assert.ok(Object.hasOwn(tables, domain), 'Unsupported count domain.');
      return Number(fixtures.execSql(`SELECT COUNT(*) FROM ${tables[domain]};`).trim());
    },
    'isolated:assertAdaptiveSchema'() {
      const state = verify(config.baseUrl);
      const tables = fixtures.execSql(`SELECT table_name FROM information_schema.tables WHERE table_schema='${state.database}' AND table_name IN ('adaptive_practice_sessions','adaptive_practice_session_items','adaptive_practice_session_summaries');`).trim().split(/\r?\n/).filter(Boolean);
      assert.equal(tables.length, 3, 'Adaptive migrations missing: this is a failed precondition, not a skipped/passed test.');
      return true;
    }
  });
  on('after:run', results => {
    const dir = path.join(config.projectRoot, 'results/cypress');
    fs.mkdirSync(dir, {
      recursive: true
    });
    // Only summary metadata; requests, responses, credentials and browser storage are deliberately excluded.
    fs.writeFileSync(path.join(dir, 'summary.json'), JSON.stringify({
      totalTests: results.totalTests,
      totalPassed: results.totalPassed,
      totalFailed: results.totalFailed,
      totalPending: results.totalPending,
      totalSkipped: results.totalSkipped,
      specs: (results.runs || []).map(run => ({
        spec: run.spec.relative,
        tests: run.tests.map(test => ({
          title: test.title.join(' / '),
          state: test.state
        }))
      }))
    }, null, 2));
  });
  return config;
}
module.exports = {
  register,
  verify
};
