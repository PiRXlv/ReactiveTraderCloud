import * as React from 'react'
import { useContext } from 'react'
import { BasePlatformAdapter } from './platformAdapter'

const PlatformContext = React.createContext<BasePlatformAdapter>(null)
export const { Provider: PlatformProvider } = PlatformContext

export function usePlatform() {
  return useContext(PlatformContext)
}
