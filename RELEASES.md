# Omnom releases

Every new deployment gets a unique beta version. The first is **beta 0.0.1**, followed by **beta 0.0.2**, **beta 0.0.3**, and so on, incrementing the patch number. This applies to preview, production, and redeployment attempts, including failed builds. Promotion of an existing deployment or read-only inspection keeps its existing version. Earlier deployments predate this versioning policy.

`frontend/package.json` is the single version source. Package SemVer `0.0.1-beta` displays as `beta 0.0.1` in the app footer. Keep the package lock synchronized. Deployment metadata and this log associate that version with a specific uploaded working tree; a Git SHA alone does not describe uncommitted CLI uploads.

## Release procedure

1. Check the latest attempted version in this log and deployed metadata, then choose the next unused patch number. Serialize releases so two people/agents do not allocate the same version.
2. Update package and lockfile together, for example `npm --prefix frontend version 0.0.2-beta --no-git-tag-version`. Record the prepared release here. Do not increment versions for local builds or documentation updates alone.
3. Run `dotnet test tests/Omnom.Tests`, `npm --prefix frontend test`, and `npm --prefix frontend run build`. Confirm the footer and generated bundle contain the intended version. Preserve existing diary/backup formats.
4. For Git integration, pushing the linked production branch creates a deployment: put the beta version in the commit subject so Vercel Git metadata identifies it, and do not also create a second CLI deployment with that same version. Check the resulting commit SHA and footer against that deployment. For a CLI release, confirm the linked Vercel project/team and intended target. Upload the authorized working tree using the existing CLI credentials; never print credentials. Attach `--meta appVersion=0.0.2-beta --meta 'releaseLabel=beta 0.0.2'` with the selected version. Use `--prod` only for an authorized production release. Do not push Git changes as a side effect of a CLI release.
5. Inspect that exact deployment until its state is Ready or failed. For CLI releases record its ID/URL and result below. For Git-triggered releases, the commit’s Vercel check and final handoff are the deployment receipt (SHA/ID/URL/result); link the project dashboard below rather than triggering another deployment solely to commit a post-build receipt. Confirm the intended production aliases after a production release. A successful upload is not a completed deployment.
6. Include the version and usable app URL in the final handoff. A new deployment/retry uses the next version; promotion or rollback of an existing deployment retains the version already embedded in that build and must be logged as such.

## beta 0.0.1 — October 20, 2026

- Package version: `0.0.1-beta`.
- Target: production; linked project `omnom`, team `team_mfq8lyWncLfiAY4Xkrf40VBO`.
- Status: **Ready — deployed to production**. Vercel confirmed `readyState=READY` and `aliasAssigned=true`.
- Production: [omnomai.vercel.app](https://omnomai.vercel.app); also assigned to `omnom-lake.vercel.app` and `omnom-victorys-projects-c1cb4594.vercel.app`.
- Runtime log check: `vercel logs <deployment-url> --level error --since 5m --no-follow` returned no logs. This is a bounded error-log query, not a claim that monitoring/drains are configured.
- Browser verification: production app loaded and visibly displayed **beta 0.0.1** in the footer; no captured console errors/warnings. No meal was submitted or saved. [Production screenshot](docs/verification/beta-0.0.1-production.png).
- Build log: cloud frontend build ran as `frontend@0.0.1-beta` and produced the matching local bundle `index-T1PuEMa2.js`. npm audit reported 10 dependency advisories (2 moderate, 8 high); dependency remediation was not part of this release.
- Next new deployment: **beta 0.0.2**, including a preview or rebuild.
- Deployment: `dpl_JCv9PNG4H11yJ4YJ6BuX2nXonqha`; [deployment URL](https://omnom-glvcy0rp5-victorys-projects-c1cb4594.vercel.app), [Vercel inspector](https://vercel.com/victorys-projects-c1cb4594/omnom/JCv9PNG4H11yJ4YJ6BuX2nXonqha).
- Metadata: `appVersion=0.0.1-beta`, `releaseLabel=beta 0.0.1`.
- Local verification: 92 backend tests, 25 frontend regressions, TypeScript/production build, package/lock/bundle version agreement, and `git diff --check` passed.
- Upload command: `vercel deploy --prod --yes --no-wait --scope team_mfq8lyWncLfiAY4Xkrf40VBO --meta appVersion=0.0.1-beta --meta 'releaseLabel=beta 0.0.1'`.
- Source: working tree based on `11239373c2da77dea10da4ff170903f70244f70f`, including existing local product improvements, roadmap/OM-001 work, and the visible release label. Not a clean-commit deployment.
- Changes: first numbered beta; editable reference-backed meal review, meal reuse/defaults, saved-meal editing and seven-day progress, current parsing safeguards, reconciled developer docs, and corrected local API proxy.
- Compatibility: browser diary key/version remains `omnom_diary_v1` / version 1; no account/sync migration or new schema change in the version-label work.

## beta 0.0.2 — October 20, 2026

- Package version: `0.0.2-beta`.
- Target: production, triggered by the authorized push to linked GitHub `VictoryMa8/omnom` branch `main`.
- Release mechanism: authorized Git push to `main`; inspect the associated Vercel commit check/release handoff for the final build result. [Project/deployment dashboard](https://vercel.com/victorys-projects-c1cb4594/omnom).
- Pre-push verification: 103 backend tests, 25 Node regressions, 18 browser smoke checks, frontend/.NET builds, and both 66-case offline evaluations completed successfully. Correctness failures in the evaluation baseline are measured results, documented under OM-003.
- Changes: includes all current project work, OM-001 documentation/setup, OM-002 desktop/mobile release gate, and OM-003's 66-case corpus, scorer, runner, offline baselines, and live opt-in request guards.
- Source: the release commit subject includes **beta 0.0.2**. Its Git history, Vercel commit check, and release handoff identify the exact SHA/deployment. No live provider evaluation was authorized or run.
- Compatibility: browser diary remains `omnom_diary_v1`, backup version 1. No sync/account or application-schema migration.
