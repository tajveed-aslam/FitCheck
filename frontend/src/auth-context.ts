import { createContext, useContext } from 'react'

export interface Session {
  token: string
  email: string
  isGuest: boolean
  expiresAt: string
}

export interface AuthContextValue {
  session: Session | null
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  startGuest: () => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
