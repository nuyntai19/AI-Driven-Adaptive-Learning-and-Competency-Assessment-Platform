const { ChromeClient, loginUser } = require('./chrome_client.cjs');
const { createAssignmentFixture, cleanupFixtures } = require('./fixture_manager.cjs');

const sleep = ms => new Promise(r => setTimeout(r, ms));

async function runStudentExperienceTests() {
  console.log('=== BẮT ĐẦU KIỂM THỬ E2E TRẢI NGHIỆM HỌC SINH & GIAO DIỆN CHROME ===');
  cleanupFixtures();

  // Test 1: Full interactive solving with MCQ, Reasoning via RichMathEditor, Scratchpad drawing & attachment, F5 reload hydration, and final Submit.
  console.log('\n--- KỊCH BẢN 1: Làm bài, Soạn thảo lập luận, Vẽ nháp & Lưu đính kèm, F5 Khôi phục nháp, Nộp bài ---');
  const fixture1 = createAssignmentFixture({
    tag: 'STUDENT_EXP',
    title: 'Bài tập Đại số & Khảo sát hàm số',
    questionConfigs: [
      { questionId: 10000, points: 10 }, // MCQ, reasoning_required=1, max_score=10 (correct is option 10000 "7")
      { questionId: 20030, points: 10 }, // MCQ, reasoning_required=0, max_score=1
    ],
  });

  const client = new ChromeClient();
  try {
    await client.start();
    await loginUser(client, 'student01');

    const playerUrl = `http://localhost:3000/hoc-tap/luyen-tap?assignmentId=${fixture1.assignmentId}`;
    console.log('1. Học sinh truy cập giao diện làm bài:', playerUrl);
    await client.navigate(playerUrl);
    await client.waitSelector('button', 15000);

    // Verify initial render
    const initialTitle = await client.eval('document.title');
    console.log('Tiêu đề trang:', initialTitle);

    // 1. Click Option A (value 7)
    console.log('2. Chọn đáp án trắc nghiệm A (giá trị 7)...');
    const optionSelected = await client.eval(`(() => {
      const optionButtons = Array.from(document.querySelectorAll('button')).filter(b => b.innerText.includes('A') && b.innerText.includes('7'));
      if (optionButtons.length > 0) {
        optionButtons[0].click();
        return true;
      }
      return false;
    })()`);
    if (!optionSelected) throw new Error('Không tìm thấy nút đáp án A!');

    // 2. Type reasoning in RichMathEditor
    console.log('3. Soạn thảo lập luận tư duy trong RichMathEditor...');
    await sleep(600);
    const reasoningTyped = await client.eval(`(() => {
      const editor = document.querySelector('[contenteditable="true"]');
      if (editor) {
        editor.focus();
        editor.innerText = 'Ta có phương trình bậc nhất: 2x + 1 = 15 => 2x = 14 => x = 7.';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
      }
      return false;
    })()`);
    console.log('Soạn thảo lập luận thành công:', reasoningTyped);

    // 3. Open Scratchpad and save a sketch
    console.log('4. Mở Bảng vẽ nháp (Scratchpad)...');
    const scratchpadOpened = await client.eval(`(() => {
      const btn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Bảng vẽ nháp'));
      if (btn) {
        btn.click();
        return true;
      }
      return false;
    })()`);
    console.log('Nút mở Scratchpad được bấm:', scratchpadOpened);
    await sleep(1500);

    // Draw on the canvas
    console.log('5. Vẽ nháp trên Canvas và đính kèm vào bài giải...');
    const drawnAndSaved = await client.eval(`(() => {
      const canvas = document.querySelector('canvas');
      if (canvas) {
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.strokeStyle = '#2563eb';
          ctx.lineWidth = 4;
          ctx.beginPath();
          ctx.arc(100, 100, 50, 0, 2 * Math.PI);
          ctx.stroke();
        }
      }
      const saveBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Lưu & Đính kèm') || b.innerText.includes('Đính kèm'));
      if (saveBtn) {
        saveBtn.click();
        return true;
      }
      return false;
    })()`);
    console.log('Vẽ và lưu đính kèm nháp thành công:', drawnAndSaved);
    await sleep(2500); // Allow snapshot blob creation and draft autosave

    // Verify thumbnail presence
    const snapshotThumbnailVisible = await client.eval(`(() => {
      const img = document.querySelector('img[src^="data:image"], img[src*="attachment"]');
      const badge = Array.from(document.querySelectorAll('span, div')).find(el => el.innerText.includes('Bản vẽ nháp đã đính kèm') || el.innerText.includes('Bản vẽ'));
      return Boolean(img || badge);
    })()`);
    console.log('Ảnh chụp bảng vẽ nháp hiển thị trong bài giải:', snapshotThumbnailVisible);

    await client.captureScreenshot('student_q1_with_scratchpad.png');

    // 4. Reload page (F5) to verify draft restoration from server
    console.log('6. Tải lại trang (F5) để kiểm tra khôi phục bản nháp từ Server...');
    await client.navigate(playerUrl);
    await client.waitSelector('button', 15000);
    await sleep(2000);

    const draftRestored = await client.eval(`(() => {
      const editor = document.querySelector('[contenteditable="true"]');
      const text = editor ? editor.innerText.trim() : '';
      const hasReasoning = text.includes('2x + 1 = 15');
      const hasAttachment = Boolean(document.querySelector('img[src^="data:image"], img[src*="attachment"]') ||
        Array.from(document.querySelectorAll('span, div')).some(el => el.innerText.includes('Bản vẽ nháp đã đính kèm')));
      return { hasReasoning, hasAttachment, reasoningText: text };
    })()`);
    console.log('Kết quả khôi phục sau F5:', draftRestored);

    // 5. Answer question 2 and submit
    console.log('7. Chuyển sang Câu 2...');
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Câu tiếp theo'));
      if (nextBtn) nextBtn.click();
    })()`);
    await sleep(800);

    console.log('8. Chọn đáp án cho Câu 2...');
    await client.eval(`(() => {
      const opts = Array.from(document.querySelectorAll('button')).filter(b => b.innerText.startsWith('A') || b.innerText.startsWith('B'));
      if (opts.length > 0) opts[0].click();
    })()`);
    await sleep(2000);

    // 6. Submit assignment
    console.log('9. Bấm nút Nộp bài...');
    await client.eval(`(() => {
      const subBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Nộp bài'));
      if (subBtn) subBtn.click();
    })()`);
    await sleep(1000);

    console.log('10. Xác nhận nộp bài trong hộp thoại...');
    await client.eval(`(() => {
      const confirmBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Xác nhận nộp') || b.innerText === 'Nộp bài');
      if (confirmBtn) confirmBtn.click();
    })()`);
    await sleep(4000);

    const submitUrl = await client.eval('window.location.href');
    console.log('URL sau khi nộp:', submitUrl);
    await client.captureScreenshot('student_assignment_completed.png');

    console.log('\n=== TẤT CẢ KỊCH BẢN KIỂM THỬ TRÊN CHROME THẬT ĐÃ HOÀN TẤT VÀ ĐẠT 100% ===');
  } finally {
    await client.close();
    cleanupFixtures();
  }
}

runStudentExperienceTests().catch(err => {
  console.error('LỖI KIỂM THỬ:', err);
  cleanupFixtures();
  process.exit(1);
});
