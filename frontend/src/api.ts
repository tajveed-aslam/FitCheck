export type Importance = 'high' | 'medium' | 'low'

export interface MissingKeyword {
  keyword: string
  importance: Importance
}

export interface ImprovementTip {
  title: string
  detail: string
}

export interface AnalysisSummary {
  id: string
  title: string
  company: string | null
  matchScore: number
  fileName: string
  createdAt: string
}

export interface Analysis {
  id: string
  title: string
  company: string | null
  fileName: string
  matchScore: number
  summary: string
  matchedSkills: string[]
  missingKeywords: MissingKeyword[]
  tips: ImprovementTip[]
  jobDescription: string
  cvText: string
  model: string
  durationMs: number
  createdAt: string
}

export interface AuthResponse {
  token: string
  expiresAt: string
  email: string
  isGuest: boolean
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Paths below already start with /api, so accept the base URL with or without a trailing "/api".
const API_BASE = (import.meta.env.VITE_API_BASE_URL ?? '').trim().replace(/\/+$/, '').replace(/\/api$/i, '')

let authToken: string | null = null
let onUnauthorized: (() => void) | null = null

export function configureAuth(token: string | null, unauthorizedHandler: (() => void) | null) {
  authToken = token
  onUnauthorized = unauthorizedHandler
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(path: string, init: RequestInit & { json?: unknown } = {}): Promise<T> {
  const { json, ...rest } = init
  const headers = new Headers(rest.headers)
  if (authToken) headers.set('Authorization', `Bearer ${authToken}`)
  // FormData bodies set their own multipart Content-Type (with boundary); only JSON needs it here.
  if (json !== undefined) headers.set('Content-Type', 'application/json')

  let response: Response
  try {
    response = await fetch(API_BASE + path, {
      ...rest,
      headers,
      body: json !== undefined ? JSON.stringify(json) : rest.body,
    })
  } catch {
    throw new ApiError(0, "Can't reach the server. Check your connection and try again.")
  }

  if (response.status === 401 && authToken) onUnauthorized?.()

  if (!response.ok) {
    throw new ApiError(response.status, await readError(response))
  }

  return (response.status === 204 ? undefined : await response.json()) as T
}

async function readError(response: Response): Promise<string> {
  if (response.status === 413) return 'That file is too large. CVs must be under 5 MB.'
  try {
    const problem = (await response.json()) as ProblemDetails
    const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : []
    if (fieldErrors.length > 0) return fieldErrors.join(' ')
    if (problem.detail) return problem.detail
    if (problem.title) return problem.title
  } catch {
    // Non-JSON error body; fall through.
  }
  return `Request failed (${response.status}).`
}

export const api = {
  health: () => request<{ status: string }>('/api/health'),

  register: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', json: { email, password } }),
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', json: { email, password } }),
  guest: () => request<AuthResponse>('/api/auth/guest', { method: 'POST' }),

  listAnalyses: () => request<AnalysisSummary[]>('/api/analyses'),
  getAnalysis: (id: string) => request<Analysis>(`/api/analyses/${id}`),
  createAnalysis: (cv: File, jobDescription: string, title?: string) => {
    const form = new FormData()
    form.append('cv', cv)
    form.append('jobDescription', jobDescription)
    if (title) form.append('title', title)
    return request<Analysis>('/api/analyses', { method: 'POST', body: form })
  },
  deleteAnalysis: (id: string) => request<void>(`/api/analyses/${id}`, { method: 'DELETE' }),
}
