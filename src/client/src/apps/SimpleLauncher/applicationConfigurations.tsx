import { excelIcon, limitCheckerIcon, reactiveAnalyticsIcon, reactiveTraderIcon, searchIcon, } from './icons/index'
import { EXCEL_ADAPTER_NAME, PlatformName } from 'rt-platforms'

// Safer than location.origin due to browser support
const ORIGIN = `${location.protocol}//${location.host}`

const defaultWindowOptions: OpenFinWindowOptions = {
  autoShow: true,
  defaultWidth: 1280,
  defaultHeight: 900,
  minWidth: 800,
  minHeight: 600,
  resizable: true,
  maximizable: true,
  defaultCentered: true,
  frame: false,
  shadow: true,
  icon: `${ORIGIN}/static/media/icon.ico`,
  accelerator:
    process.env.NODE_ENV !== 'development'
      ? {}
      : {
          devtools: true,
          reload: true,
          reloadIgnoringCache: true,
          zoom: true,
        },
}

const excelPreloadScripts: fin.DownloadPreloadOption[] = [
  // OpenFin Excel API not included here (not included in standard package)
  {
    url: `${ORIGIN}/plugin/service-loader.js`,
  },
  {
    url: `${ORIGIN}/plugin/fin.desktop.Excel.js`,
  },
]

type ApplicationType = 'window' | 'download' | 'application' | 'excel'

interface Provider {
  platformName: PlatformName
  applicationType: ApplicationType
  windowOptions?: OpenFinWindowOptions
}

export interface ApplicationConfig {
  name: string
  url?: string
  icon: JSX.Element
  provider?: Provider
}

const excelJSAppConfig: ApplicationConfig = {
  name: 'Excel',
  icon: excelIcon,
  provider: {
    platformName: 'openfin',
    applicationType: 'excel',
    windowOptions: {
      preloadScripts: excelPreloadScripts,
      icon: `${ORIGIN}/static/media/excel-icon.ico`,
    },
  },
}

const excelLegacyAppConfig: ApplicationConfig = {
  name: 'Excel',
  icon: excelIcon,
  url: `${ORIGIN}/static/excel/instructions.html`,
  provider: {
    platformName: 'openfin',
    applicationType: 'application',
    windowOptions: {
      ...defaultWindowOptions,
      defaultWidth: 800,
      defaultHeight: 600,
      minWidth: 520,
      minHeight: 260,
      frame: true,
      alwaysOnTop: false,
      icon: `${ORIGIN}/static/media/excel-icon.ico`,
    },
  },
}

const excelAppConfig = EXCEL_ADAPTER_NAME === 'JS' ? excelJSAppConfig : excelLegacyAppConfig

const baseAppConfigs: ApplicationConfig[] = [
  {
    name: 'Reactive Trader',
    url: `${ORIGIN}`,
    icon: reactiveTraderIcon,
    provider: {
      platformName: 'openfin',
      applicationType: 'application',
      windowOptions: {
        ...defaultWindowOptions,
        preloadScripts: excelPreloadScripts,
        icon: `${ORIGIN}/static/media/rt-icon.ico`,
      },
    },
  },
  {
    name: 'Reactive Analytics',
    url: 'http://demo-reactive-analytics.adaptivecluster.com/',
    icon: reactiveAnalyticsIcon,
    provider: {
      platformName: 'openfin',
      applicationType: 'application',
      windowOptions: {
        ...defaultWindowOptions,
        frame: true,
        icon: `${ORIGIN}/static/media/ra-icon.ico`,
      },
    },
  },
  {
    name: 'Limit Checker',
    url: `${ORIGIN}/plugin/LimitChecker.zip`,
    icon: limitCheckerIcon,
    provider: {
      platformName: 'openfin',
      applicationType: 'download',
      windowOptions: {
        ...defaultWindowOptions,
        icon: `${ORIGIN}/static/media/limit-checker-icon.ico`,
      },
    },
  },
  excelAppConfig,
]

const spotlightConfig: ApplicationConfig = {
  name: 'Spotlight',
  url: `${ORIGIN}/spotlight`,
  icon: searchIcon,
  provider: {
    platformName: 'openfin',
    applicationType: 'application',
    windowOptions: {
      ...defaultWindowOptions,
    },
  },
}

export const appConfigs =
  process.env.NODE_ENV === 'development' ? [...baseAppConfigs, spotlightConfig] : baseAppConfigs
