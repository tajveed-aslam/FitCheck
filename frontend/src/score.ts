export type ScoreTone = 'strong' | 'good' | 'partial' | 'weak'

/** Bands match the scoring guide given to the model in the API's prompt. */
export function scoreBand(score: number): { label: string; tone: ScoreTone } {
  if (score >= 85) return { label: 'Strong match', tone: 'strong' }
  if (score >= 70) return { label: 'Good match', tone: 'good' }
  if (score >= 50) return { label: 'Partial match', tone: 'partial' }
  return { label: 'Weak match', tone: 'weak' }
}
