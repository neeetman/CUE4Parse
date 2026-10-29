const fs = require('node:fs');
const path = require('node:path');

// No GitHub API calls: reporting must also work with Issues disabled or a read-only token.
function report(env, read = file => fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '') {
  const failedStep = ['MERGE', 'SETUP', 'BUILD', 'TEST', 'PUSH']
    .find(step => ['failure', 'cancelled'].includes(env[`${step}_OUTCOME`]));
  const success = env.RESULT === 'up-to-date' ||
    (env.RESULT === 'merged' && env.PUSH_OUTCOME === 'success');
  const failed = !success || !!failedStep || ['failure', 'cancelled'].includes(env.JOB_STATUS);
  const reason = env.RESULT === 'conflict' ? 'merge conflict' :
    failedStep ? `${failedStep.toLowerCase()} failed or cancelled` :
    failed ? `sync incomplete (${env.RESULT || 'checkout/merge did not complete'})` : env.RESULT;
  const runUrl = `${env.GITHUB_SERVER_URL}/${env.GITHUB_REPOSITORY}/actions/runs/${env.GITHUB_RUN_ID}`;
  let summary = `## Upstream sync: ${reason}\n\n` +
    `Target: \`${env.TARGET_BRANCH}\`\n\nUpstream: \`${env.UPSTREAM_BRANCH}\` ` +
    `(\`${env.UPSTREAM_SHA || 'unavailable'}\`, ${env.BEHIND || 'unknown'} commits)\n\n` +
    `[Workflow logs](${runUrl})\n\n`;
  if (failed) summary += 'Sync did not complete successfully. Inspect the step outcomes before retrying.\n\n';
  for (const name of ['conflicts', 'build', 'test']) {
    const log = read(path.join(env.RUNNER_TEMP, 'upstream-sync', `${name}.log`));
    // Bound the summary size; complete command output remains in the workflow logs.
    if (log) summary += `### ${name}\n\n<pre>${log.slice(-16000)
      .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')}</pre>\n\n`;
  }
  return { failed, reason, summary };
}

if (require.main === module) {
  const result = report(process.env);
  fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, result.summary);
  console.log(result.summary);
  if (result.failed) {
    console.error(`::error::Upstream sync needs attention: ${result.reason}`);
    process.exitCode = 1;
  }
}

module.exports = { report };
