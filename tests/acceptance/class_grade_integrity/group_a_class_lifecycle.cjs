const { ChromeClient, loginUser, logoutUser, delay, WEB_URL, SEED_PASSWORD } = require('./chrome_client.cjs');

async function runGroupA() {
  const client = new ChromeClient();
  await client.start();

  try {
    console.log('=== RUNNING GROUP A: CENTER MANAGER - CLASS LIFECYCLE & GRADE INTEGRITY ===');
    await loginUser(client, 'manager');

    // Navigate to /quan-ly/lop-hoc
    await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
    await delay(1200);
    await client.waitSelector('button');

    // Helper to create a class
    async function createClass(className, gradeLevel) {
      console.log(`Creating class: ${className} (Grade ${gradeLevel})...`);
      await client.click('#btn-open-create-class');
      await delay(600);
      await client.waitSelector('#input-class-name');

      await client.fill('#input-class-name', className);
      await client.fill('#input-academic-year', '2026-2027');

      // Select Subject Tiếng Anh
      await client.eval(`(() => {
        const sel = document.querySelector('#select-subject');
        if (!sel) throw new Error('Missing #select-subject select');
        const opt = Array.from(sel.options).find(o => o.text.includes('Anh') || o.value.includes('4000'));
        if (opt) sel.value = opt.value;
        else if (sel.options.length > 1) sel.selectedIndex = 1;
        sel.dispatchEvent(new Event('change', { bubbles: true }));
      })()`);
      await delay(300);

      // Select Teacher English
      await client.eval(`(() => {
        const sel = document.querySelector('#select-teacher');
        if (!sel) throw new Error('Missing #select-teacher select');
        const opt = Array.from(sel.options).find(o => o.text.toLowerCase().includes('english') || o.text.toLowerCase().includes('anh'));
        if (opt) sel.value = opt.value;
        else if (sel.options.length > 1) sel.selectedIndex = 1;
        sel.dispatchEvent(new Event('change', { bubbles: true }));
      })()`);
      await delay(300);

      // Select Grade Level
      await client.eval(`((grade) => {
        const sel = document.querySelector('#select-grade-level');
        if (sel) {
          sel.value = String(grade);
          sel.dispatchEvent(new Event('change', { bubbles: true }));
        }
      })(${gradeLevel})`);
      await delay(300);

      // Submit modal
      await client.click('#btn-submit-create-class');
      await delay(1800);
    }

    // Helper to open Add Students modal for a class
    async function openAddStudentsModal(className, includeOtherGrades = false) {
      console.log(`Opening Add Students modal for ${className} (includeOtherGrades=${includeOtherGrades})...`);
      await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
      await delay(1200);

      // Find row for class and click Chi tiết
      await client.eval(`((cName) => {
        const rows = Array.from(document.querySelectorAll('tr'));
        const row = rows.find(r => r.textContent.includes(cName));
        if (!row) throw new Error('Class row not found: ' + cName);
        const detailBtn = Array.from(row.querySelectorAll('button')).find(b => b.textContent.includes('Chi tiết'));
        if (!detailBtn) throw new Error('Detail button not found for ' + cName);
        detailBtn.click();
      })(${JSON.stringify(className)})`);
      await delay(1000);
      await client.waitSelector('#btn-open-add-students');

      await client.click('#btn-open-add-students');
      await delay(800);
      await client.waitSelector('#checkbox-include-other-grades');

      if (includeOtherGrades) {
        await client.eval(`(() => {
          const chk = document.querySelector('#checkbox-include-other-grades');
          if (chk && !chk.checked) chk.click();
        })()`);
        await delay(800);
      }
    }

    // Ensure baseline classes exist
    const hasG10 = await client.eval(`Boolean(document.body.innerText.includes('UIACC-GRADE10-CLASS'))`);
    if (!hasG10) {
      await createClass('UIACC-GRADE10-CLASS', 10);
    }

    const hasG11 = await client.eval(`Boolean(document.body.innerText.includes('UIACC-GRADE11-CLASS'))`);
    if (!hasG11) {
      await createClass('UIACC-GRADE11-CLASS', 11);
    }

    const hasInactive = await client.eval(`Boolean(document.body.innerText.includes('UIACC-INACTIVE-CLASS'))`);
    if (!hasInactive) {
      await createClass('UIACC-INACTIVE-CLASS', 10);
      await client.eval(`(async (seedPw) => {
        const loginRes = await fetch('/api/v1/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'manager', password: seedPw })
        });
        const { data } = await loginRes.json();
        const token = data.accessToken;

        const listRes = await fetch('/api/v1/classes?pageSize=50', {
          headers: { 'Authorization': 'Bearer ' + token }
        });
        const listData = await listRes.json();
        const inactiveClass = (listData.data || []).find(c => c.className === 'UIACC-INACTIVE-CLASS');
        if (inactiveClass) {
          const detailRes = await fetch('/api/v1/classes/' + inactiveClass.classId, {
            headers: { 'Authorization': 'Bearer ' + token }
          });
          const detail = await detailRes.json();
          await fetch('/api/v1/classes/' + inactiveClass.classId, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({
              className: 'UIACC-INACTIVE-CLASS',
              teacherId: detail.data?.teacher?.teacherId || detail.data?.teacherId || 'd0000000-0000-0000-0001-000000000003',
              gradeLevel: 10,
              status: 'Archived',
              rowVersion: detail.data?.rowVersion || ''
            })
          });
        }
      })(${JSON.stringify(SEED_PASSWORD)})`);
      await delay(800);
    }

    // Refresh class list to show all classes
    await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
    await delay(1200);

    // Scenario 01: Class Grade Level Displayed
    await client.captureScreenshot('01_class_grade_level_displayed.png');
    console.log('PASS: 01_class_grade_level_displayed.png captured');

    // Scenario 02: Add Student Same Grade Success
    console.log('Testing Scenario 02: Add student same grade...');
    await openAddStudentsModal('UIACC-GRADE10-CLASS', false);

    // Select student01 (Duy Bảo Trịnh - Grade 10)
    await client.eval(`(() => {
      const labels = Array.from(document.querySelectorAll('label'));
      const lbl = labels.find(l => l.textContent.includes('Duy Bảo Trịnh') || l.textContent.includes('student01'));
      if (!lbl) throw new Error('student01 Duy Bảo Trịnh not found in candidates list');
      const cb = lbl.querySelector('input[type="checkbox"]');
      if (cb && !cb.checked) cb.click();
    })()`);
    await delay(400);

    // Confirm addition
    await client.click('#btn-submit-add-students');
    await delay(1500);

    await client.captureScreenshot('02_add_student_same_grade_success.png');
    console.log('PASS: 02_add_student_same_grade_success.png captured');

    // Scenario 03 & 04 & 05: Cross Grade Student Warning, Validation, Success
    console.log('Testing Scenario 03 & 04: Cross-grade warning and validation...');
    await openAddStudentsModal('UIACC-GRADE10-CLASS', true);

    // Select student05 (Bảo Lễ Hồ - Grade 11) into Grade 10 class
    await client.eval(`(() => {
      const labels = Array.from(document.querySelectorAll('label'));
      const lbl = labels.find(l => l.textContent.includes('Bảo Lễ Hồ') || l.textContent.includes('student05'));
      if (!lbl) throw new Error('student05 Bảo Lễ Hồ not found in candidates list');
      const cb = lbl.querySelector('input[type="checkbox"]');
      if (cb && !cb.checked) cb.click();
    })()`);
    await delay(600);

    // Scenario 03: Warning Displayed
    await client.captureScreenshot('03_add_student_cross_grade_warning.png');
    console.log('PASS: 03_add_student_cross_grade_warning.png captured');

    // Scenario 04: Empty Reason Validation Rejected
    await client.click('#btn-submit-add-students');
    await delay(600);

    await client.captureScreenshot('04_add_student_cross_grade_validation.png');
    console.log('PASS: 04_add_student_cross_grade_validation.png captured');

    // Scenario 05: Valid Reason Submitted & Success
    console.log('Testing Scenario 05: Add cross-grade student with valid reason...');
    await client.eval(`(() => {
      const chk = document.querySelector('#checkbox-allow-grade-mismatch, #checkbox-allow-grade-mismatch-legacy, input[type="checkbox"][id*="allow-grade"]');
      if (chk && !chk.checked) chk.click();
    })()`);
    await delay(400);

    const validReason = 'Học sinh vượt lớp có thành tích xuất sắc và hoàn thành bài thi khảo sát năng lực';
    await client.fill('#input-grade-mismatch-reason, #input-grade-mismatch-reason-legacy, input[id*="grade-mismatch-reason"]', validReason);
    await delay(400);

    await client.click('#btn-submit-add-students');
    await delay(1800);

    await client.captureScreenshot('05_add_student_cross_grade_success.png');
    console.log('PASS: 05_add_student_cross_grade_success.png captured');

    // Scenario 06: Pagination Persistence showing preserved reason text
    console.log('Testing Scenario 06: Pagination persistence with visible reason text...');
    await openAddStudentsModal('UIACC-GRADE11-CLASS', true);

    // Select student02 (Grade 10) or student03 into Grade 11 class
    await client.eval(`(() => {
      const labels = Array.from(document.querySelectorAll('label'));
      const lbl = labels.find(l => l.textContent.includes('Khối 10'));
      if (!lbl) throw new Error('Grade 10 candidate not found for pagination test');
      const cb = lbl.querySelector('input[type="checkbox"]');
      if (cb && !cb.checked) cb.click();
    })()`);
    await delay(500);

    // Check allow checkbox to expose reason input
    await client.eval(`(() => {
      const chk = document.querySelector('#checkbox-allow-grade-mismatch, #checkbox-allow-grade-mismatch-legacy, input[type="checkbox"][id*="allow-grade"]');
      if (chk && !chk.checked) chk.click();
    })()`);
    await delay(400);

    const testReason = 'Học sinh học trước chương trình theo diện bồi dưỡng nhân tài cấp trung tâm';
    await client.fill('#input-grade-mismatch-reason, #input-grade-mismatch-reason-legacy, input[id*="grade-mismatch-reason"]', testReason);
    await delay(500);

    // Click Next Page then Previous Page
    await client.eval(`(() => {
      const nextBtn = document.querySelector('#btn-next-candidates');
      if (nextBtn && !nextBtn.disabled) nextBtn.click();
    })()`);
    await delay(800);

    await client.eval(`(() => {
      const prevBtn = document.querySelector('#btn-prev-candidates');
      if (prevBtn && !prevBtn.disabled) prevBtn.click();
    })()`);
    await delay(800);

    // Ensure the reason field is scrolled into view so text is clearly captured
    await client.eval(`(() => {
      const el = document.querySelector('#input-grade-mismatch-reason, #input-grade-mismatch-reason-legacy, input[id*="grade-mismatch-reason"]');
      if (el) el.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(500);

    // Capture screenshot 06 showing the reason text preserved and visible
    await client.captureScreenshot('06_pagination_cross_grade_cache.png');
    console.log('PASS: 06_pagination_cross_grade_cache.png captured');

    // Close modal
    await client.eval(`(() => {
      const closeBtn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Hủy') || b.textContent.includes('Đóng')
      );
      if (closeBtn) closeBtn.click();
    })()`);
    await delay(800);

    // Scenario 07: Delete Empty Class Success
    console.log('Testing Scenario 07: Delete empty class...');
    await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
    await delay(1200);

    const hasEmpty = await client.eval(`Boolean(document.body.innerText.includes('UIACC-EMPTY-CLASS'))`);
    if (!hasEmpty) {
      await createClass('UIACC-EMPTY-CLASS', 10);
      await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
      await delay(1200);
    }

    // Click delete on UIACC-EMPTY-CLASS
    await client.eval(`(() => {
      const rows = Array.from(document.querySelectorAll('tr'));
      const row = rows.find(r => r.textContent.includes('UIACC-EMPTY-CLASS'));
      if (!row) throw new Error('UIACC-EMPTY-CLASS row not found');
      const delBtn = Array.from(row.querySelectorAll('button')).find(b => b.textContent.includes('Xóa'));
      if (!delBtn) throw new Error('Delete button not found for empty class');
      delBtn.click();
    })()`);
    await delay(800);

    // Capture confirmation modal
    await client.captureScreenshot('07_delete_empty_class_success.png');
    console.log('PASS: 07_delete_empty_class_success.png captured');

    // Confirm deletion
    await client.click('#btn-confirm-delete-class');
    await delay(1800);

    // Scenario 08: Delete Class with Enrolled Students Blocked
    console.log('Testing Scenario 08: Delete class with students blocked...');
    await client.navigate(`${WEB_URL}/quan-ly/lop-hoc`);
    await delay(1200);

    await client.eval(`(() => {
      const rows = Array.from(document.querySelectorAll('tr'));
      const row = rows.find(r => r.textContent.includes('UIACC-GRADE10-CLASS'));
      if (!row) throw new Error('UIACC-GRADE10-CLASS row not found');
      const delBtn = Array.from(row.querySelectorAll('button')).find(b => b.textContent.includes('Xóa'));
      if (!delBtn) throw new Error('Delete button not found for UIACC-GRADE10-CLASS');
      delBtn.click();
    })()`);
    await delay(800);

    // Confirm deletion to trigger business error
    await client.click('#btn-confirm-delete-class');
    await delay(1200);

    await client.captureScreenshot('08_delete_class_with_students_blocked.png');
    console.log('PASS: 08_delete_class_with_students_blocked.png captured');

    console.log('=== GROUP A COMPLETED SUCCESSFULLY ===');
  } finally {
    await client.close();
  }
}

if (require.main === module) {
  runGroupA().catch((err) => {
    console.error('Group A failed:', err);
    process.exit(1);
  });
}

module.exports = { runGroupA };
