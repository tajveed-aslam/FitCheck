# Progress — FitCheck
_Last updated: 2026-10-07 by Claude Code_

## Goal
Resume-to-job-description match analyzer: upload a CV (PDF/DOCX) + paste a JD → match score %, matched skills,
missing keywords (with importance), 3 improvement tips, score gauge + highlighted gaps, per-user history.
Stack: ASP.NET Core 8 Web API, React + TS (Vite), PostgreSQL (EF Core), JWT, Gemini (OpenAI optional).
Built on APITestGen's foundation (auth, guest demo, LLM clients, errors, deploy config were copied and renamed).

## Done
- Backend (`backend/FitCheck.Api`, port **5081**): auth (register/login/guest/me), `POST/GET/DELETE /api/analyses`
  (multipart upload), EF migration `InitialCreate` (tables `users`, `analyses`).
- `DocumentTextExtractor`: PDF via PdfPig (`ContentOrderTextExtractor`), DOCX via Open XML SDK (paragraphs incl.
  tables); type decided by magic bytes; .doc / encrypted / corrupt / >10 pages / too little text → 400s.
- `Prompts`: JSON schema + scoring rubric; CV/JD wrapped in tags, closing tags in pasted text neutralised.
- `MatchResultParser`: clamps score, dedupes, max 25 matched / 20 missing, ≤3 tips; "missing" keywords found
  verbatim in the CV are moved to matched (`ContainsTerm`, word-boundary aware).
- `AnalysisService` split into `Prepare` (validation + extraction, no LLM) and `AnalyzeAsync`; controller takes
  `AnalysisQuota` (guests 5/h, users 30/h) only between them, so rejected uploads don't burn quota.
  Coarse `UploadPolicy` (60/h) middleware limit + per-IP guest-session limit remain.
- Frontend (`frontend/`, port **5174**): landing with preview gauge, login, workspace with drag-and-drop upload,
  "Try with sample CV & job" (`public/sample-cv.pdf`, fictional "Alex Morgan", source `docs/sample-cv.html`),
  results (SVG gauge, chips, tips, JD/CV tabs with highlights, copy/download Markdown report), history sidebar.
- Tests: 43 xUnit offline (real PDFs/DOCX built in memory) + live Gemini test (passes; ignores a planted
  "score 100" instruction). Browser E2E via Playwright + installed Edge: 15/15. API edge cases: 12/12.
- Neon: database `fitcheck` created in the same Neon project as APITestGen (direct endpoint, see gotchas).

- **Deployed and live (2026-10-07):**
  - Frontend (Vercel project `fitcheck`): https://fitcheck-rho-three.vercel.app (`fitcheck.vercel.app` is someone else's).
  - API (Render, Docker, Frankfurt, free): https://fitcheck-api-3jw1.onrender.com, health at `/api/health`.
  - Render env `Cors__Origins__0` was entered with a trailing slash; the API now strips trailing slashes from
    configured origins (same fix pushed to APITestGen).
  - Live browser E2E (Playwright + Edge): 15/15, real analysis 75% in ~20 s.
- Portfolio: card with demo link, screenshot `public/screenshots/fitcheck-landing.png` and note, plus a mention in
  About.tsx — pushed.

## Status
Project complete. Possible follow-ups (not requested): apply the "quota only for valid requests" pattern to
APITestGen; cleanup job for expired guest accounts; OCR for scanned PDFs.

## Next steps
- None pending. If a URL changes, update Render `Cors__Origins__0`, this README and the portfolio card.

## Decisions & gotchas
- Same secrets pattern as APITestGen: `backend/FitCheck.Api/appsettings.Development.json` is gitignored and never
  published (`CopyToPublishDirectory=Never`). It reuses the APITestGen Gemini key and Neon credentials.
- Neon: connecting to a **non-existent** database through the `-pooler` host times out (no error). Create DBs first
  (direct host, `CREATE DATABASE`), then `MigrateOnStartup` creates tables.
- Rate limits must not count requests that fail validation — that's why quota is taken in the controller after
  `Prepare`. (APITestGen still counts every POST at the middleware level; possible follow-up.)
- Only extracted CV text is stored, never the file.
- In Playwright tests use exact matches: "Delete" also matches the history items' "Delete <title>" buttons, and
  "Guest demo" also matches the form hint.
