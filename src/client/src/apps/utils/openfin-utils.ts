import { Application } from 'openfin/_v2/main'

export interface ApplicationConfig {
  name: string
  url: string
  windowOptions?: OpenFinWindowOptions
}

export async function getExistingOpenFinApplication(
  uuid: string,
): Promise<fin.OpenFinApplication | undefined> {
  const allApps = await fin.System.getAllApplications()
  const targetApp = allApps.some(app => app.uuid === uuid)
  if (targetApp) {
    return fin.Application.wrap(uuid)
  }
}

async function restoreExistingApp(existingApp: fin.OpenFinApplication): Promise<fin.Application> {
  return new Promise((resolve, reject) => {
    existingApp.isRunning(isRunning => {
      if (!isRunning) {
        existingApp.run(() => resolve(existingApp), reject)
        return
      }
      const window = existingApp.getWindow()
      window.restore()
      window.bringToFront()
    })
  })
}

export async function createOrBringToFrontOpenFinApplication({
  name,
  url,
  windowOptions,
}: ApplicationConfig): Promise<Application> {
  const existingApp = await getExistingOpenFinApplication(name)
  if (existingApp) {
    return restoreExistingApp(existingApp)
  }
  return createAndRunOpenFinApplication({ name, url, windowOptions })
}

export async function createAndRunOpenFinApplication({
  name,
  url,
  windowOptions,
}: ApplicationConfig): Promise<fin.OpenFinApplication> {
  return new Promise((resolve, reject) => {
    const app: fin.OpenFinApplication = new fin.desktop.Application(
      {
        name,
        url,
        uuid: name,
        nonPersistent: true,
        mainWindowOptions: windowOptions,
      },
      () => app.run(() => resolve(app), reject),
      e => reject(e),
    )
  })
}

export function createOpenFinWindow({
  name,
  url,
  windowOptions,
}: ApplicationConfig): Promise<fin.OpenFinWindow> {
  return new Promise((resolve, reject) => {
    const window: fin.OpenFinWindow = new fin.desktop.Window(
      {
        url,
        name,
        ...windowOptions,
      },
      () => resolve(window),
      reject,
    )
  })
}
