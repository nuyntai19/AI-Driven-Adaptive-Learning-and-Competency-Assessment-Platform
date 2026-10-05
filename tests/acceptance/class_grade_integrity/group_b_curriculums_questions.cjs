const { ChromeClient, loginUser, logoutUser, delay, WEB_URL, API_URL, SEED_PASSWORD } = require('./chrome_client.cjs');
const {
  ensureQuestions20030And20031,
  assertCurriculumCreated,
  assertCurriculumClassLink,
  queryRows,
} = require('./fixture_helper.cjs');

async function runGroupB() {
  const client = new ChromeClient();
  await client.start();

  try {
    console.log('=== RUNNING GROUP B: TEACHER - CURRICULUMS & QUESTIONS GRADE INTEGRITY ===');
    // Log in specifically as teacher.english
    await loginUser(client, 'teacher.english');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 09: Curriculum Create Grade 10
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 09: Create curriculum Grade 10 under teacher.english...');
    await client.navigate(`${WEB_URL}/giao-vien/giao-trinh/tao-moi`);
    await delay(1500);
    await client.waitSelector('input.th-input');

    // Assert teacher identity visible in navbar / header
    const s09TeacherAssert = await client.eval(`(() => {
      const text = document.body.innerText.toLowerCase();
      return text.includes('teacher.english') || text.includes('giáo viên') || text.includes('tiếng anh');
    })()`);
    if (!s09TeacherAssert) {
      throw new Error('[ASSERT FAIL Scenario 09] Teacher identity not confirmed in UI');
    }

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

    // Capture screenshot 09
    await client.captureScreenshot('09_curriculum_create_grade10.png');
    console.log('PASS: Scenario 09 - 09_curriculum_create_grade10.png captured');

    // Submit curriculum creation
    await client.eval(`(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b =>
        b.textContent.includes('Tạo Giáo Trình') || b.textContent.includes('Tạo giáo trình')
      );
      if (!btn) throw new Error('Cannot find Create Curriculum button');
      btn.click();
    })()`);
    await delay(2500);

    // Assert curriculum created in DB with grade_level 10
    assertCurriculumCreated('UIACC-ENG-G10', 10);
    console.log('[ASSERT PASS Scenario 09] Curriculum UIACC-ENG-G10 confirmed created in DB with grade_level=10');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 10: Assign Same Grade Class (UIACC-GRADE10-CLASS)
    // ──────────────────────────────────────────────────────────────────────────
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

    // Assert UI shows success toast / message
    const s10SuccessAssert = await client.eval(`(() => {
      const text = document.body.innerText;
      return text.includes('thành công') || text.includes('Đã lưu') || Boolean(document.querySelector('.text-emerald-500, .bg-emerald-50, [role="alert"]'));
    })()`);
    if (!s10SuccessAssert) {
      throw new Error('[ASSERT FAIL Scenario 10] Curriculum save success feedback not shown');
    }

    // Assert DB links UIACC-ENG-G10 to UIACC-GRADE10-CLASS
    assertCurriculumClassLink('UIACC-ENG-G10', 'UIACC-GRADE10-CLASS', true);
    console.log('[ASSERT PASS Scenario 10] UIACC-GRADE10-CLASS confirmed linked to UIACC-ENG-G10 in DB');

    await client.captureScreenshot('10_curriculum_same_grade_assigned.png');
    console.log('PASS: Scenario 10 - 10_curriculum_same_grade_assigned.png captured');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 11: Attempt Cross-Grade Class (UIACC-GRADE11-CLASS) -> HTTP 409 Conflict
    // ──────────────────────────────────────────────────────────────────────────
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
      const banner = document.querySelector('#curriculum-feedback-banner, div[style*="rgba(239, 68, 68"], [role="alert"], div.bg-rose-50');
      if (banner) banner.scrollIntoView({ behavior: 'instant', block: 'center' });
    })()`);
    await delay(400);

    // Assert banner contains Vietnamese business message and NOT raw generic Axios error
    const s11BannerAssert = await client.eval(`(() => {
      const banner = document.querySelector('#curriculum-feedback-banner, div[style*="rgba(239, 68, 68"], [role="alert"], div.bg-rose-50');
      if (!banner) return { found: false, text: '' };
      const text = banner.innerText;
      const isGeneric = text.includes('Request failed with status code 409');
      const hasBusinessDetail = text.includes('Không thể gán lớp') || text.includes('Khối 11') || text.includes('khác khối') || text.includes('xung đột') || text.includes('409');
      return { found: true, text, isGeneric, hasBusinessDetail };
    })()`);

    if (!s11BannerAssert.found) {
      throw new Error('[ASSERT FAIL Scenario 11] HTTP 409 conflict banner not rendered on screen');
    }
    if (s11BannerAssert.isGeneric) {
      throw new Error(`[ASSERT FAIL Scenario 11] Banner leaked generic Axios status: "${s11BannerAssert.text}"`);
    }
    if (!s11BannerAssert.hasBusinessDetail) {
      throw new Error(`[ASSERT FAIL Scenario 11] Banner missing business explanation: "${s11BannerAssert.text}"`);
    }

    // Assert DB confirms UIACC-GRADE11-CLASS is NOT linked to UIACC-ENG-G10
    assertCurriculumClassLink('UIACC-ENG-G10', 'UIACC-GRADE11-CLASS', false);
    console.log('[ASSERT PASS Scenario 11] Verified: UI shows friendly Vietnamese detail "' + s11BannerAssert.text + '" and DB rejected cross-grade link');

    // Capture screenshot 11
    await client.captureScreenshot('11_curriculum_cross_grade_blocked.png');
    console.log('PASS: Scenario 11 - 11_curriculum_cross_grade_blocked.png captured (with friendly 409 conflict banner)');

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 12: Question Grade Level Compatibility
    // ──────────────────────────────────────────────────────────────────────────
    console.log('Testing Scenario 12: Question bank grade level display and activation...');
    ensureQuestions20030And20031();

    // Navigate to Question Bank
    await client.navigate(`${WEB_URL}/giao-vien/cau-hoi`);
    await delay(2000);

    // Programmatic assertion: verify question 20030 (Grade 10) and question 20031 (Grade 11) are rendered with correct grade badges
    const s12Assert = await client.eval(`(() => {
      const text = document.body.innerText;
      const hasQ10 = text.includes('20030') || text.includes('She speaks English very');
      const hasQ11 = text.includes('20031') || text.includes('Advanced Grammar');
      const hasGrade10Badge = text.includes('Khối 10') || text.includes('Grade 10');
      const hasGrade11Badge = text.includes('Khối 11') || text.includes('Grade 11');
      const hasActiveStatus = text.includes('Hoạt động') || text.includes('Active');

      return { hasQ10, hasQ11, hasGrade10Badge, hasGrade11Badge, hasActiveStatus };
    })()`);

    if (!s12Assert.hasQ10 || !s12Assert.hasQ11) {
      throw new Error(`[ASSERT FAIL Scenario 12] Questions 20030 and 20031 not rendered in question bank: ${JSON.stringify(s12Assert)}`);
    }
    if (!s12Assert.hasGrade10Badge || !s12Assert.hasGrade11Badge) {
      throw new Error(`[ASSERT FAIL Scenario 12] Grade level badges (Khối 10, Khối 11) missing in question bank: ${JSON.stringify(s12Assert)}`);
    }
    if (!s12Assert.hasActiveStatus) {
      throw new Error('[ASSERT FAIL Scenario 12] Active status badge missing for questions');
    }
    console.log('[ASSERT PASS Scenario 12] Verified: Question 20030 (Khối 10) and 20031 (Khối 11) active and correctly tagged with grade badges');

    await client.captureScreenshot('12_question_grade_level_compat.png');
    console.log('PASS: Scenario 12 - 12_question_grade_level_compat.png captured');

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
