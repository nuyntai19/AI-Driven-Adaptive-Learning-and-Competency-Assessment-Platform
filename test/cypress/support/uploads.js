// Real same-origin multipart fetch from a browser; no cy.intercept response stubs.
const { loginScreen } = require('../screens/login.screen');
function upload(token, route, { name, type = 'application/octet-stream', text = '', base64, fields = {} }) {
  loginScreen.open();
  return cy.window({ log: false }).then(async win => {
    const bytes = base64 ? Uint8Array.from(win.atob(base64), c => c.charCodeAt(0)) : text;
    const form = new win.FormData();
    form.append('file', new win.Blob([bytes], { type }), name);
    for (const [key, value] of Object.entries(fields)) form.append(key, String(value));
    const response = await win.fetch(`/api/v1${route}`, { method: 'POST', headers: { authorization: `Bearer ${token}` }, body: form });
    return { status: response.status, body: await response.json() };
  });
}
const importFields = { subjectId: '30000000-0000-0000-0000-000000000003', defaultTopicNodeId: '10000',
  gradeLevel: 10, legacyMode: true, shortAnswerEvaluationMode: 'NumericRational' };
const csv = 'QuestionType,Difficulty,QuestionText,CorrectAnswer,Solution,MaxScore,EstimatedTimeSeconds,ReasoningRequired,AnswerEvaluationMode\nShortAnswer,2,"Tính một cộng một",2,"Một cộng một bằng hai.",1,60,FALSE,NumericRational\n';
module.exports = { upload, importFields, csv };
