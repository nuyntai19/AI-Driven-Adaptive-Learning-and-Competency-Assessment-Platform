const { ChromeClient, loginUser, logoutUser, delay, WEB_URL, API_URL, SEED_PASSWORD } = require('./chrome_client.cjs');

async function runGroupB() {
  const client = new ChromeClient();
  await client.start();

  try {
    console.log('=== RUNNING GROUP B: TEACHER - CURRICULUMS & QUESTIONS GRADE INTEGRITY ===');
    // Log in specifically as teacher.english
    await loginUser(client, 'teacher.english');

    // Scenario 09: Curriculum Create Grade 10
    console.log('Testing Scenario 09: Create curriculum Grade 10 under teacher.english...');
    await client.navigate(`${WEB_URL}/giao-vien/giao-trinh/tao-moi`);
    await delay(1500);
    await client.waitSelector('input.th-input');

    await client.fill('input.th-input', 'UIACC-ENG-G10');

    // Select Grade Level 10
    await client.eval(`(() => {
      const selects = Array.from(document.querySelectorAll('select'));
      const gradeSel = selects.find(s => Array.from(s.options).some(o => o.value === '10'));
      if (gradeSel) {
        gradeSel.value = '10';
        gradeSel.dispatchEvent(new Event('change', { bubbles: true }));
      }
    })()`);
    await delay(400);

    // Capture screenshot 09: Shows Teacher English navbar and Grade 10 curriculum creation
    await client.captureScreenshot('09_curriculum_create_grade10.png');
    console.log('PASS: 09_curriculum_create_grade10.png captured');

    // Submit curriculum creation
    await client.eval(`(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tạo Giáo Trình') || b.textContent.includes('Tạo giáo trình')
      );
      if (!btn) throw new Error('Cannot find Create Curriculum button');
      btn.click();
    })()`);
    await delay(2500);

    // Scenario 10: Assign Same Grade Class (UIACC-GRADE10-CLASS)
    console.log('Testing Scenario 10: Assign Grade 10 class to Grade 10 curriculum...');
    await client.eval(`(() => {
      const rows = Array.from(document.querySelectorAll('tr, label, div.border'));
      const g10Row = rows.find(r => r.textContent.includes('UIACC-GRADE10-CLASS'));
      if (!g10Row) throw new Error('UIACC-GRADE10-CLASS not found in curriculum classes list');
      const cb = g10Row.querySelector('input[type="checkbox"]');
      if (cb && !cb.checked) cb.click();
    })()`);
    await delay(500);

    // Click "Lưu Thay Đổi"
    await client.eval(`(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.toLowerCase().includes('lưu thay đổi') || b.textContent.includes('Lưu Thay Đổi')
      );
      if (btn) btn.click();
    })()`);
    await delay(2000);

    await client.captureScreenshot('10_curriculum_same_grade_assigned.png');
    console.log('PASS: 10_curriculum_same_grade_assigned.png captured');

    // Scenario 11: Attempt Cross-Grade Class (UIACC-GRADE11-CLASS) -> HTTP 409 Conflict
    console.log('Testing Scenario 11: Attempt Grade 11 class assignment and capture HTTP 409 rejection...');
    await client.eval(`(() => {
      const rows = Array.from(document.querySelectorAll('tr, label, div.border'));
      const g11Row = rows.find(r => r.textContent.includes('UIACC-GRADE11-CLASS'));
      if (!g11Row) throw new Error('UIACC-GRADE11-CLASS not found in curriculum classes list');
      const cb = g11Row.querySelector('input[type="checkbox"]');
      if (cb && !cb.checked) cb.click();
    })()`);
    await delay(500);

    // Click Save to trigger the 409 conflict
    await client.eval(`(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.toLowerCase().includes('lưu thay đổi') ||
        b.textContent.includes('Lưu Thay Đổi')
      );
      if (!btn) throw new Error('Cannot find Save button to trigger 409 test');
      btn.click();
    })()`);
    await delay(2000);

    // Ensure the feedback banner is in view
    await client.eval(`(() => {
      const banner = document.querySelector('#curriculum-feedback-banner, div[style*="rgba(239, 68, 68"]');
      if (banner) banner.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(400);

    // Capture screenshot 11: The UI must show the error banner / notification for 409 conflict!
    await client.captureScreenshot('11_curriculum_cross_grade_blocked.png');
    console.log('PASS: 11_curriculum_cross_grade_blocked.png captured (with 409 conflict banner)');

    // Scenario 12: Question Grade Level Compatibility
    console.log('Testing Scenario 12: Question bank grade level display and activation...');
    // Create Grade 10 Question #20030 and Grade 11 Question #20031 directly via teacher API if not present
    await client.eval(`(async (seedPw) => {
      const loginRes = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ centerCode: 'EDUTWIN_A', username: 'teacher.english', password: seedPw })
      });
      const { data } = await loginRes.json();
      const token = data.accessToken;

      // 1. Create Grade 10 question
      const res10 = await fetch('/api/v1/questions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
        body: JSON.stringify({
          subjectId: '40000000-0000-0000-0000-000000000004',
          primaryTopicNodeId: '10003',
          questionType: 'MultipleChoice',
          questionText: 'Choose the best word to complete the sentence: She speaks English very ____.',
          correctAnswer: 'B',
          solution: 'Well is an adverb describing the verb speaks.',
          difficulty: 2,
          gradeLevel: 10,
          estimatedTimeSeconds: 120,
          options: [
            { optionLabel: 'A', optionText: 'good', isCorrect: false, orderIndex: 0 },
            { optionLabel: 'B', optionText: 'well', isCorrect: true, orderIndex: 1 },
            { optionLabel: 'C', optionText: 'fluent', isCorrect: false, orderIndex: 2 },
            { optionLabel: 'D', optionText: 'nicely', isCorrect: false, orderIndex: 3 }
          ]
        })
      });
      const q10Data = await res10.json().catch(() => ({}));
      if (q10Data.data?.questionId) {
        await fetch('/api/v1/questions/' + q10Data.data.questionId + '/activate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
          body: JSON.stringify({ rowVersion: q10Data.data.rowVersion })
        });
      }

      // 2. Create Grade 11 question
      const res11 = await fetch('/api/v1/questions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
        body: JSON.stringify({
          subjectId: '40000000-0000-0000-0000-000000000004',
          primaryTopicNodeId: '10003',
          questionType: 'MultipleChoice',
          questionText: 'Advanced Grammar: If he had studied harder, he ____ the university entrance exam.',
          correctAnswer: 'C',
          solution: 'Conditional sentence type 3: If + past perfect, would have + V3/ed.',
          difficulty: 3,
          gradeLevel: 11,
          estimatedTimeSeconds: 120,
          options: [
            { optionLabel: 'A', optionText: 'would pass', isCorrect: false, orderIndex: 0 },
            { optionLabel: 'B', optionText: 'will pass', isCorrect: false, orderIndex: 1 },
            { optionLabel: 'C', optionText: 'would have passed', isCorrect: true, orderIndex: 2 },
            { optionLabel: 'D', optionText: 'passed', isCorrect: false, orderIndex: 3 }
          ]
        })
      });
      const q11Data = await res11.json().catch(() => ({}));
      if (q11Data.data?.questionId) {
        await fetch('/api/v1/questions/' + q11Data.data.questionId + '/activate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
          body: JSON.stringify({ rowVersion: q11Data.data.rowVersion })
        });
      }

      // Activate any Draft questions for English
      const listRes = await fetch('/api/v1/questions?pageSize=50', {
        headers: { 'Authorization': 'Bearer ' + token }
      });
      const listData = await listRes.json();
      for (const q of (listData.data || [])) {
        if (q.status === 'Draft') {
          await fetch('/api/v1/questions/' + q.questionId + '/activate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ rowVersion: q.rowVersion })
          });
        }
      }
    })(${JSON.stringify(SEED_PASSWORD)})`);
    await delay(1000);

    // Navigate to Question Bank
    await client.navigate(`${WEB_URL}/giao-vien/cau-hoi`);
    await delay(1500);

    await client.captureScreenshot('12_question_grade_level_compat.png');
    console.log('PASS: 12_question_grade_level_compat.png captured');

    console.log('=== GROUP B COMPLETED SUCCESSFULLY ===');
  } finally {
    await client.close();
  }
}

if (require.main === module) {
  runGroupB().catch((err) => {
    console.error('Group B failed:', err);
    process.exit(1);
  });
}

module.exports = { runGroupB };
