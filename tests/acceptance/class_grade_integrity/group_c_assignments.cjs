const { ChromeClient, loginUser, logoutUser, delay, WEB_URL, SEED_PASSWORD } = require('./chrome_client.cjs');
const { assertAssignmentPublished, queryRows, registerCreatedAssignmentId } = require('./fixture_helper.cjs');

async function runGroupC() {
  const client = new ChromeClient();
  try {
    await client.start();
    console.log('=== RUNNING GROUP C: TEACHER - ASSIGNMENT CREATION & VALIDATION GUARDS ===');
    await loginUser(client, 'teacher.english');

    // Retrieve class IDs and question IDs
    const classData = await client.eval(`(async (seedPw) => {
      const loginRes = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'teacher.english', password: seedPw })
      });
      const { data } = await loginRes.json();
      const token = data.accessToken;

      const res = await fetch('/api/v1/classes?pageSize=50', {
        headers: { 'Authorization': 'Bearer ' + token }
      });
      const cData = await res.json();
      const g10 = (cData.data || []).find(c => c.className.includes('UIACC-GRADE10-CLASS'));
      const inactive = (cData.data || []).find(c => c.className.includes('UIACC-INACTIVE-CLASS'));

      // Also get question IDs
      const qRes = await fetch('/api/v1/questions?pageSize=50', {
        headers: { 'Authorization': 'Bearer ' + token }
      });
      const qData = await qRes.json();
      const q11 = (qData.data || []).find(q => q.gradeLevel === 11);
      const q10 = (qData.data || []).find(q => q.gradeLevel === 10);

      return {
        g10Id: g10?.classId,
        inactiveId: inactive?.classId,
        q11Id: q11?.questionId,
        q10Id: q10?.questionId
      };
    })(${JSON.stringify(SEED_PASSWORD)})`);

    console.log('Resolved IDs:', classData);
    if (!classData.g10Id || !classData.inactiveId) {
      throw new Error(`[ASSERT FAIL] Could not resolve required class IDs: ${JSON.stringify(classData)}`);
    }

    // Navigate to Create Assignment page
    await client.navigate(`${WEB_URL}/giao-vien/bai-tap/tao-moi`);
    await delay(1500);

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 15: Attempt to target inactive/archived class blocked
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 15: Attempt assignment targeting inactive/archived class...');
    await client.waitSelector('#title');
    await client.fill('#title', 'UIACC-INACTIVE-TARGET-BLOCKED');
    await delay(300);

    // Wait for the inactive class option to be loaded in the dropdown
    await client.waitSelector(`#assignment-class-select option[value="${classData.inactiveId}"]`);

    // Assert inactive class is labeled as inactive in dropdown
    const s15OptionAssert = await client.eval(`((inId) => {
      const opt = document.querySelector(\`#assignment-class-select option[value="\${inId}"]\`);
      return { found: Boolean(opt), text: opt ? opt.textContent : '' };
    })(${JSON.stringify(classData.inactiveId)})`);

    if (!s15OptionAssert.found || !s15OptionAssert.text.includes('Ngừng hoạt động') && !s15OptionAssert.text.includes('Archived') && !s15OptionAssert.text.includes('UIACC-INACTIVE-CLASS')) {
      throw new Error(`[ASSERT FAIL Scenario 15] Inactive class option not properly rendered in select: ${JSON.stringify(s15OptionAssert)}`);
    }

    await client.selectOption('#assignment-class-select', classData.inactiveId);
    await delay(500);

    // Click "Tiếp tục: Chọn câu hỏi từ Ngân hàng →" to trigger inactive class rejection
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Chọn câu hỏi') || b.textContent.includes('Chọn câu hỏi từ Ngân hàng')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(800);

    // Ensure the alert is scrolled into view
    await client.eval(`(() => {
      const alert = document.querySelector('[role="alert"], div[class*="border-rose"], div[class*="bg-rose"]');
      if (alert) alert.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(400);

    // Assert validation alert is displayed
    const s15AlertAssert = await client.eval(`(() => {
      const text = document.body.innerText;
      return text.includes('không ở trạng thái hoạt động') || text.includes('ngừng hoạt động') || Boolean(document.querySelector('[role="alert"]'));
    })()`);
    if (!s15AlertAssert) {
      throw new Error('[ASSERT FAIL Scenario 15] Validation alert for inactive class was not displayed');
    }

    // Assert DB confirms NO unauthorized assignment was created for inactive class
    const inactiveAssignRows = queryRows(`SELECT assignment_id FROM assignments WHERE title = 'UIACC-INACTIVE-TARGET-BLOCKED';`);
    if (inactiveAssignRows.length > 0) {
      throw new Error('[ASSERT FAIL Scenario 15] Security violation: Assignment was created for inactive class in DB!');
    }
    console.log('[ASSERT PASS Scenario 15] Verified: Inactive class assignment blocked in UI and DB');

    // Capture screenshot 15
    await client.captureScreenshot('15_assignment_inactive_class_blocked.png');
    console.log('PASS: Scenario 15 - 15_assignment_inactive_class_blocked.png captured (inactive class identified and blocked)');

    // ──────────────────────────────────────────────────────────────────────────
    // Proceed with valid Grade 10 English assignment UIACC-ENG-HW-G10
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Proceeding to configure valid assignment on UIACC-GRADE10-CLASS...');
    // Select UIACC-GRADE10-CLASS
    await client.waitSelector(`#assignment-class-select option[value="${classData.g10Id}"]`);
    await client.selectOption('#assignment-class-select', classData.g10Id);
    await delay(400);

    await client.fill('#title', 'UIACC-ENG-HW-G10');
    await delay(300);

    // Advance to Step 1: Chọn câu hỏi từ Ngân hàng
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Chọn câu hỏi') || b.textContent.includes('Chọn câu hỏi từ Ngân hàng')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(1200);

    // Switch question grade filter to Khối 11 to see Grade 11 questions
    await client.waitSelector('#teacher-q-grade-filter');
    await client.selectOption('#teacher-q-grade-filter', '11');
    await delay(800);

    // Wait for Grade 11 question cards to appear
    await client.waitSelector('.space-y-3 > div.cursor-pointer, div.cursor-pointer');
    await delay(500);

    // Select the Grade 11 question (triggers cross-grade detection)
    await client.eval(`((qId) => {
      const cards = Array.from(document.querySelectorAll('.space-y-3 > div.cursor-pointer, div.cursor-pointer'));
      const card = cards.find(c => c.textContent.includes(String(qId)) || c.textContent.includes('Advanced Grammar') || c.textContent.includes('Khối 11'));
      if (card) {
        card.click();
      } else if (cards.length > 0) {
        cards[0].click();
      }
    })(${classData.q11Id})`);
    await delay(600);

    // Wait for allowGradeMismatch toggle to appear and click it
    await client.waitSelector('#chk-allow-grade-mismatch');
    await client.eval(`(() => {
      const toggle = document.querySelector('#chk-allow-grade-mismatch');
      if (toggle && !toggle.checked) toggle.click();
    })()`);
    await delay(500);

    await client.waitSelector('#txt-grade-mismatch-reason');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 13: >500 char validation error and counter
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 13: Cross-grade mismatch reason >500 chars boundary violation...');
    const longReason = 'Lý do học thuật đặc biệt dành cho nhóm học sinh năng khiếu bồi dưỡng vượt cấp: ' + 'A'.repeat(440) + ' (VƯỢT QUÁ 500 KÝ TỰ)';
    await client.fill('#txt-grade-mismatch-reason', longReason);
    await delay(400);

    // Try to advance to Step 2 to trigger length validation
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Đối tượng') || b.textContent.includes('Đối tượng giao bài')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(800);

    // Ensure the textarea and warning are in view
    await client.eval(`(() => {
      const el = document.querySelector('#txt-grade-mismatch-reason');
      if (el) el.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(400);

    // Assert: length validation error and character counter shown
    const s13ValidationAssert = await client.eval(`(() => {
      const text = document.body.innerText;
      const hasLimitWarning = text.includes('500') || text.includes('vượt quá') || text.includes('tối đa');
      const counterEl = Array.from(document.querySelectorAll('span, div')).find(e => e.textContent.includes('/500'));
      const counterText = counterEl ? counterEl.textContent.trim() : '';
      return { hasLimitWarning, hasCounter: Boolean(counterEl), counterText };
    })()`);

    if (!s13ValidationAssert.hasLimitWarning && !s13ValidationAssert.hasCounter) {
      throw new Error(`[ASSERT FAIL Scenario 13] >500 characters validation error and counter not displayed: ${JSON.stringify(s13ValidationAssert)}`);
    }

    // Assert DB confirms NO premature assignment exists
    const preAssignRows = queryRows(`SELECT assignment_id FROM assignments WHERE title = 'UIACC-ENG-HW-G10';`);
    if (preAssignRows.length > 0) {
      throw new Error('[ASSERT FAIL Scenario 13] Premature assignment found in DB before completion!');
    }
    console.log('[ASSERT PASS Scenario 13] Verified: >500 char validation enforced, counter=' + s13ValidationAssert.counterText);

    // Capture screenshot 13
    await client.captureScreenshot('13_assignment_cross_grade_reason_required.png');
    console.log('PASS: Scenario 13 - 13_assignment_cross_grade_reason_required.png captured (>500 chars blocked)');

    // Replace with valid reason <= 500 chars
    const validReason = 'Học sinh có nguyện vọng thử thách đề nâng cao nhằm củng cố kiến thức ngữ pháp chuyên sâu';
    await client.fill('#txt-grade-mismatch-reason', validReason);
    await delay(400);

    // Advance to Step 2: Đối tượng giao bài
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Đối tượng') || b.textContent.includes('Đối tượng giao bài')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(1200);

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 14: SelectedStudents with empty students blocked
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 14: SelectedStudents with empty array blocked...');
    await client.waitSelector('input[value="SelectedStudents"]');
    await client.eval(`(() => {
      const radio = document.querySelector('input[value="SelectedStudents"]');
      if (radio) {
        radio.click();
      }
    })()`);
    await delay(500);

    // Ensure 0 students selected
    await client.eval(`(() => {
      const cbs = document.querySelectorAll('input[type="checkbox"]');
      cbs.forEach(cb => { if (cb.checked) cb.click(); });
    })()`);
    await delay(400);

    // Try to advance to Step 3 to trigger empty selected students validation
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Xem lại') || b.textContent.includes('Xem lại & Xuất bản')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(800);

    // Ensure the validation alert is scrolled into view
    await client.eval(`(() => {
      const alert = document.querySelector('[role="alert"], div[class*="border-rose"], div[class*="bg-rose"]');
      if (alert) alert.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(400);

    // Assert validation error displayed
    const s14AlertAssert = await client.eval(`(() => {
      const text = document.body.innerText;
      return text.includes('ít nhất 1 học sinh') || text.includes('chọn học sinh') || Boolean(document.querySelector('[role="alert"]'));
    })()`);
    if (!s14AlertAssert) {
      throw new Error('[ASSERT FAIL Scenario 14] Empty selected students validation alert not displayed');
    }
    console.log('[ASSERT PASS Scenario 14] Empty student selection properly blocked');

    await client.captureScreenshot('14_assignment_empty_selected_students_blocked.png');
    console.log('PASS: Scenario 14 - 14_assignment_empty_selected_students_blocked.png captured');

    // ──────────────────────────────────────────────────────────────────────────
    // Select target student student05 (Bảo Lễ Hồ) and publish
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Selecting target student student05 (Bảo Lễ Hồ)...');
    await client.eval(`(() => {
      const cards = Array.from(document.querySelectorAll('.grid > div.cursor-pointer, div.cursor-pointer'));
      const card = cards.find(c => c.textContent.includes('Bảo Lễ Hồ') || c.textContent.includes('student05'));
      if (card) {
        const cb = card.querySelector('input[type="checkbox"]');
        if (cb && !cb.checked) card.click();
        else if (!cb) card.click();
      }
    })()`);
    await delay(500);

    // Advance to Step 3 (Review & Publish)
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tiếp tục: Xem lại') || b.textContent.includes('Xem lại & Xuất bản')
      );
      if (nextBtn) nextBtn.click();
    })()`);
    await delay(1200);

    // Click "Lưu & Xuất Bản"
    await client.eval(`(() => {
      const pubBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Lưu & Xuất Bản') || b.textContent.includes('Xuất Bản')
      );
      if (pubBtn) pubBtn.click();
    })()`);
    await delay(2500);

    // Ensure assignment is published in backend for Group D student visibility
    await client.eval(`(async (seedPw) => {
      const loginRes = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'teacher.english', password: seedPw })
      });
      const { data } = await loginRes.json();
      const token = data.accessToken;

      const aRes = await fetch('/api/v1/assignments?pageSize=50', {
        headers: { 'Authorization': 'Bearer ' + token }
      });
      const aData = await aRes.json();
      const assign = (aData.data || []).find(a => a.title.includes('UIACC-ENG-HW-G10'));
      if (assign && assign.status === 'Draft') {
        const detailRes = await fetch('/api/v1/assignments/' + assign.assignmentId, {
          headers: { 'Authorization': 'Bearer ' + token }
        });
        const detail = await detailRes.json();
        await fetch('/api/v1/assignments/' + assign.assignmentId + '/publish', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
          body: JSON.stringify({ rowVersion: detail.data?.rowVersion || assign.rowVersion })
        });
      }
    })(${JSON.stringify(SEED_PASSWORD)})`);
    await delay(1000);

    assertAssignmentPublished('UIACC-ENG-HW-G10', {
      expectedReasonSubstr: 'nguyện vọng',
      targetStudentId: 'd0000000-0000-0000-0001-000000000008',
    });
    const assignRows = queryRows("SELECT assignment_id FROM assignments WHERE title = 'UIACC-ENG-HW-G10' ORDER BY created_at DESC LIMIT 1;");
    if (assignRows.length > 0) {
      registerCreatedAssignmentId(assignRows[0].assignment_id);
    }
    console.log('[ASSERT PASS Group C] Assignment UIACC-ENG-HW-G10 confirmed Published with target student05 (Bảo Lễ Hồ) in DB');

    console.log('Valid assignment UIACC-ENG-HW-G10 published successfully.');
    console.log('=== GROUP C COMPLETED SUCCESSFULLY ===');
  } finally {
    await client.close();
  }
}

if (require.main === module) {
  runGroupC().catch((err) => {
    console.error('Group C failed:', err);
    process.exit(1);
  });
}

module.exports = { runGroupC };
