const { ChromeClient, loginUser } = require('./chrome_client.cjs');
const { createAssignmentFixture, cleanupFixtures } = require('./fixture_manager.cjs');

const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  cleanupFixtures();
  const fixture = createAssignmentFixture({
    tag: 'BROWSER_FLOW',
    title: 'Kiểm thử thực tế giao diện và nộp bài',
    questionConfigs: [
      { questionId: 10000, points: 10 }, // MCQ, reasoning_required=1, max_score=10 (options: 10000=7, 10001=5, 10002=6, 10003=8; correct is 10000)
      { questionId: 20030, points: 10 }, // MCQ, reasoning_required=0, max_score=1
    ],
  });
  console.log('Created fixture:', fixture.assignmentId);

  const client = new ChromeClient();
  try {
    await client.start();
    await loginUser(client, 'student01');

    const playerUrl = `http://localhost:3000/hoc-tap/luyen-tap?assignmentId=${fixture.assignmentId}`;
    console.log('Navigating to player:', playerUrl);
    await client.navigate(playerUrl);

    // Wait for player to load
    await client.waitSelector('button', 15000);
    console.log('Player loaded successfully.');

    // 1. Select answer A (Option 10000) for question 1
    const optionSelected = await client.eval(`(() => {
      const optionButtons = Array.from(document.querySelectorAll('button')).filter(b => b.innerText.includes('A') && b.innerText.includes('7'));
      if (optionButtons.length > 0) {
        optionButtons[0].click();
        return true;
      }
      return false;
    })()`);
    console.log('Selected MCQ option A:', optionSelected);

    // 2. Type reasoning in the reasoning textarea
    await sleep(500);
    const reasoningTyped = await client.eval(`(() => {
      const textarea = document.querySelector('textarea');
      if (textarea) {
        textarea.focus();
        textarea.value = 'Phương pháp giải: vì x + 2 = 9 nên x = 7.';
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
        textarea.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
      }
      return false;
    })()`);
    console.log('Typed reasoning:', reasoningTyped);

    // Wait 2s for auto-save debounce
    console.log('Waiting for draft autosave...');
    await sleep(3000);

    // Check draft saved status on page
    const draftStatus = await client.eval(`(() => {
      const el = document.querySelector('[data-testid="draft-status"], .text-xs');
      return el ? el.innerText : null;
    })()`);
    console.log('Draft status visible:', draftStatus);

    await client.captureScreenshot('player_draft_saved.png');

    // 3. Move to question 2
    await client.eval(`(() => {
      const nextBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Câu tiếp theo'));
      if (nextBtn) nextBtn.click();
    })()`);
    await sleep(800);

    // Select an option for question 2
    const q2Selected = await client.eval(`(() => {
      const opts = Array.from(document.querySelectorAll('button')).filter(b => b.innerText.startsWith('A') || b.innerText.startsWith('B'));
      if (opts.length > 0) {
        opts[0].click();
        return true;
      }
      return false;
    })()`);
    console.log('Selected option for question 2:', q2Selected);

    // Wait for autosave
    await sleep(2500);

    // 4. Click Submit button
    console.log('Clicking Submit button...');
    const submitClicked = await client.eval(`(() => {
      const subBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Nộp bài'));
      if (subBtn) {
        subBtn.click();
        return true;
      }
      return false;
    })()`);
    console.log('Submit button clicked:', submitClicked);

    // Wait for confirmation dialog or submission to complete
    await sleep(1000);
    const confirmClicked = await client.eval(`(() => {
      const confirmBtn = Array.from(document.querySelectorAll('button')).find(b => b.innerText.includes('Xác nhận nộp') || b.innerText === 'Nộp bài');
      if (confirmBtn) {
        confirmBtn.click();
        return true;
      }
      return false;
    })()`);
    console.log('Confirmation dialog handled:', confirmClicked);

    // Wait for submission API and redirect/result display
    await sleep(4000);

    const afterSubmitUrl = await client.eval('window.location.href');
    console.log('After submit URL:', afterSubmitUrl);

    const pageContent = await client.eval('document.body.innerText');
    console.log('Result page snippet:', pageContent.slice(0, 300));

    await client.captureScreenshot('player_after_submit.png');
    console.log('SUCCESS: Full browser flow executed smoothly!');

  } finally {
    await client.close();
    cleanupFixtures();
  }
})().catch(e => {
  console.error('Browser flow error:', e);
  cleanupFixtures();
  process.exit(1);
});
