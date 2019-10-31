import { InteropTopics, PlatformAdapter, PlatformWindow } from 'rt-platforms'
import { stringify } from 'query-string'
import { defaultConfig, windowOrigin } from './defaultWindowConfig'
import { BlotterFilters, validateFilters } from '../../MainRoute/widgets/blotter'

let blotterWindow: PlatformWindow | undefined

function updatedExistingBlotter(
  blotterWindow: PlatformWindow,
  filters: BlotterFilters,
  platform: PlatformAdapter,
) {
  if (platform.hasFeature('interop')) {
    platform.interop.publish(InteropTopics.FilterBlotter, filters)
  } else {
    console.log(`Interop publishing is not available, skipping updating blotter filters`)
  }
  blotterWindow.restore()
  blotterWindow.bringToFront()
}

export async function showBlotter(filters: BlotterFilters, platform: PlatformAdapter) {
  if (blotterWindow) {
    updatedExistingBlotter(blotterWindow, filters, platform)
    return
  }

  const baseUrl = `${windowOrigin}/blotter`
  const queryString = stringify(validateFilters(filters))
  const url = queryString ? `${baseUrl}/?${queryString}` : baseUrl

  blotterWindow = await platform.windowApi.open(
    {
      ...defaultConfig,
      width: 1100,
      url,
    },
    () => (blotterWindow = undefined),
  )
}
