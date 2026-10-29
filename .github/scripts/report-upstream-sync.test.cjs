const { test } = require('node:test');
const assert = require('node:assert/strict');
const { report } = require('./report-upstream-sync.cjs');

const env = {
  RUNNER_TEMP: '/tmp', TARGET_BRANCH: 'uepacktool-fixes', UPSTREAM_BRANCH: 'master',
  GITHUB_SERVER_URL: 'https://github.com', GITHUB_REPOSITORY: 'neeetman/CUE4Parse',
  GITHUB_RUN_ID: '40790666863', UPSTREAM_SHA: 'abc123', BEHIND: '67',
};

test('conflicts are reported without any Issues API dependency', () => {
  const result = report({ ...env, RESULT: 'conflict' }, file =>
    file.endsWith('conflicts.log') ? 'EGame.cs\nFStaticMeshRenderData.cs' : '');
  assert.equal(result.failed, true);
  assert.match(result.summary, /merge conflict/);
  assert.match(result.summary, /EGame.cs/);
  assert.match(result.summary, /actions\/runs\/40790666863/);
});

for (const step of ['MERGE', 'SETUP', 'BUILD', 'TEST', 'PUSH']) {
  test(`${step} failure remains visible even without diagnostic files`, () => {
    const result = report({ ...env, RESULT: 'merged', [`${step}_OUTCOME`]: 'failure' }, () => '');
    assert.equal(result.failed, true);
    assert.match(result.reason, new RegExp(step.toLowerCase()));
  });
}

test('checkout failure and cancellation cannot become successful reports', () => {
  assert.equal(report({ ...env, JOB_STATUS: 'failure' }, () => '').failed, true);
  assert.equal(report({ ...env, RESULT: 'up-to-date', JOB_STATUS: 'cancelled' }, () => '').failed, true);
});

test('up-to-date and successfully pushed merges are successful', () => {
  assert.equal(report({ ...env, RESULT: 'up-to-date' }, () => '').failed, false);
  assert.equal(report({ ...env, RESULT: 'merged', PUSH_OUTCOME: 'success' }, () => '').failed, false);
  assert.equal(report({ ...env, RESULT: 'merged', PUSH_OUTCOME: 'skipped' }, () => '').failed, true);
});

test('diagnostics are bounded and escaped', () => {
  const result = report({ ...env, RESULT: 'conflict' }, () => 'x'.repeat(20000) + '<error>&');
  assert.ok(result.summary.length < 50000);
  assert.match(result.summary, /&lt;error&gt;&amp;/);
  assert.ok(!result.summary.includes('<error>'));
});
