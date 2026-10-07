export const SAMPLE_CV_URL = '/sample-cv.pdf'
export const SAMPLE_CV_NAME = 'sample-cv-alex-morgan.pdf'

export const sampleJobDescription = `Senior QA Automation Engineer, Northwind Payments (Remote, Europe)

Northwind Payments builds the checkout and payouts platform used by 4,000 online merchants. We're hiring a Senior QA Automation Engineer to own test strategy for our merchant dashboard and public REST APIs.

What you'll do
- Design and maintain our end-to-end test framework (Playwright with TypeScript)
- Build API test suites for our REST APIs and webhooks
- Run tests in CI/CD with GitHub Actions and keep the pipeline under 15 minutes
- Add performance testing with k6 for checkout flows
- Mentor two mid-level QA engineers and review their test code

Requirements
- 5+ years in test automation
- Strong Playwright or Cypress experience with TypeScript
- Solid API testing skills (REST APIs, Postman or similar)
- SQL for test data setup and validation
- Experience with CI/CD pipelines and Docker
- Excellent written English

Nice to have
- Payments or fintech domain experience
- Performance testing with k6 or JMeter
- AWS
- Contract testing (Pact)`

export async function loadSampleCv(): Promise<File> {
  const response = await fetch(SAMPLE_CV_URL)
  if (!response.ok) throw new Error('Could not load the sample CV.')
  return new File([await response.blob()], SAMPLE_CV_NAME, { type: 'application/pdf' })
}
