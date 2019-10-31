/* eslint-disable no-undef */
import { ApplicationConfig } from './applicationConfigurations'
import { createExcelApp } from 'rt-platforms'
import { createOpenFinWindow, createOrBringToFrontOpenFinApplication } from '../utils'
import { Application } from 'openfin/_v2/main'

export async function open(
  config: ApplicationConfig,
): Promise<Window | fin.OpenFinWindow | Application | void> {
  const { provider, url, name } = config
  const { windowOptions } = provider

  // Not under openfin -> open as url on browser
  if (typeof fin === 'undefined') {
    return window.open(config.url, config.name)
  }

  // open as url through openfin
  if (provider.platformName === 'browser') {
    return new Promise((resolve, reject) =>
      fin.desktop.System.openUrlWithBrowser(config.url, resolve, reject),
    )
  }

  // open new openfin application
  if (provider.platformName === 'openfin') {
    switch (provider.applicationType) {
      case 'window':
        return createOpenFinWindow({ name, url, windowOptions })
      case 'download':
        return launchLimitChecker(config)
      case 'excel':
        const excelApp = await createExcelApp(provider.platformName)
        return excelApp.open()
      case 'application':
      default:
        return createOrBringToFrontOpenFinApplication({ name, url, windowOptions })
    }
  }
}

async function launchLimitChecker(config: ApplicationConfig) {
  const app = fin.Application.wrap({ uuid: config.name })
  fin.desktop.System.launchExternalProcess({
    alias: 'LimitChecker',
    listener(result) {
      console.log('the exit code', result.exitCode)
    },
  })
  return app
}
