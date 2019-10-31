import { PlatformFeatures, PlatformType, WindowConfig } from './types'
import { Context } from 'openfin-fdc3'
import DefaultRoute from './defaultRoute'
import Logo from './logo'
import { ApplicationEpic } from 'StoreTypes'

export type PlatformWindowApi = {
  open: (config: WindowConfig, onClose?: () => void) => Promise<PlatformWindow | null>
}

export type PlatformWindow = {
  close?: () => void
  maximize?: () => void
  minimize?: () => void
  resize?: () => void

  restore: () => void
  bringToFront: () => void
}

export abstract class BasePlatformAdapter {
  abstract readonly type: PlatformType

  abstract readonly allowTearOff: boolean

  abstract readonly name: string

  /**
   * Determines whether a platform has a given feature and performs a type guard for it
   * @param feature name of the feature
   */
  hasFeature<FeatureName extends keyof PlatformFeatures>(
    feature: FeatureName,
  ): this is Pick<PlatformFeatures, FeatureName> {
    return !!(this as any)[feature]
  }

  abstract readonly mainWindow: PlatformWindow

  abstract readonly windowApi: PlatformWindowApi

  notification: {
    notify: (message: object) => void
  }
  abstract fdc3: {
    broadcast?: (context: Context) => void
  }

  style = {
    height: '100%',
  }

  epics: Array<ApplicationEpic> = []

  PlatformHeader: React.FC<any> = () => null

  PlatformControls: React.FC<any> = () => null

  PlatformRoute: React.FC = DefaultRoute

  Logo: any = Logo
}
