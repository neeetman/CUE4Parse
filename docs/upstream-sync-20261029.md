# Upstream sync repair

## Compared revisions

- Fork `uepacktool-fixes`: `f742525f7c49cea6c893dee487f098ca21e1fa09`.
- Upstream `master`: `c0b3bbe22b6998dfb0090f4cba10390192fb3996` (67 commits ahead at inspection, rather than the 63 in failed run 40790666863).
- The merge has both revisions as parents, so the next daily sync recognizes these upstream commits as already integrated.

## Conflict decisions

`FStaticMeshRenderData.cs`: retain the fork's Dead Island 2 single-distance-field framing in the generic parser, including zero-LOD handling. Adopt upstream's removal of the old Delta Force skip/custom-bounds calls: upstream commit `1af207517745dce0e59f8dfae6c51ffb236de648` routes that game through its dedicated parser. Do not reintroduce the removed helper calls. The upstream dedicated Dead Island 2 parser and dispatch are also retained and tested separately.

`EGame.cs`: preserve Dakar's existing `GAME_UE4_27 + 40` numeric value for fork configuration compatibility. Assign ArcheAge War `+41` in this fork. **Numeric ArcheAge War configurations copied from upstream (`+40`) must be changed to `+41`, or use the enum name.** Keeping both at `+40` would incorrectly trigger Dakar morph parsing for ArcheAge War and ArcheAge War pak handling for Dakar. Future UE4.27 game additions must also be reviewed for numeric collisions.

The final diff against upstream retains the fork's FF7 static-mesh changes, disabled skeletal-section/bone-map handling, Dead Island 2 LOD extensions and version options, and Dakar morph parsing. These were not replaced wholesale by upstream files.

## Workflow behavior

- Always write the outcome, exact upstream SHA, run link and available diagnostics to Actions Summary. Report checkout/setup/fetch/merge/build/test/push failures, not just conflicts and compiler errors.
- Do not call the Issues API or request Issues write permission. A conflict or validation failure still makes the job fail after reporting.
- Save conflict diagnostics before aborting the merge. Build and parser-test output is captured in logs and summarized with bounded excerpts.
- Use `set -euo pipefail` so the first failed build cannot be hidden by a later successful command.
- Push only after both managed projects build and parser/reporting regressions pass. A rejected push is reported and never force-pushed.
- Exercise reporting regressions in the pull-request test workflow too.

## Validation

Local Linux validation used .NET SDK 10.0.403, Release configuration and `CUE4PARSE_SKIP_NATIVE=true`:

```bash
dotnet build CUE4Parse-Conversion/CUE4Parse-Conversion.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet test CUE4Parse.Tests/CUE4Parse.Tests.csproj -c Release -m:1 -p:UseSharedCompilation=false --logger 'console;verbosity=minimal'
node --test .github/scripts/report-upstream-sync.test.cjs
```

- Core + Conversion: build succeeded, zero errors; existing compiler/NuGet warnings remain.
- Full .NET suite: 225 passed, 1 pre-existing skipped test, 0 failed (226 total). This includes 14 new regressions for generic and dedicated Dead Island 2 distance-field framing, ordinary UE4 LOD framing, bounds/screen-size/sentinel alignment and distinct game IDs.
- Reporting: 9 Node tests passed, including conflict, setup/build/test/push failures, cancellation, no-op, success and bounded diagnostics.
- Workflow YAML parsed and every shell block passed `bash -n`.
- Executed the workflow's actual merge shell in a temporary repository with divergent conflicting commits: recorded file/diff diagnostics, aborted cleanly, preserved fork bytes, then generated a failing Summary without a token or any Issues API.
- Executed the actual build shell with a failing first mock build and successful second mock build: exit 23 propagated and the second command was never invoked.

Native compilation and proprietary FF7/Dakar/Dead Island 2 asset files were not exercised locally. The new parser tests use synthetic serialized streams; bundled upstream fixtures are covered by the full test suite. Pull-request CI remains responsible for validation on the normal GitHub runner, including its native build path.
