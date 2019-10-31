import { InteropTopics, PlatformAdapter } from 'rt-platforms'
import { stringify } from 'query-string'
import { defaultConfig, windowOrigin } from './defaultWindowConfig'
import { BlotterFilters, validateFilters } from '../../MainRoute/widgets/blotter'

let blotterWindow: Window = null

export function showBlotter(filters: BlotterFilters, platform: PlatformAdapter) {
  if (blotterWindow && !blotterWindow.closed) {
    if (platform.hasFeature('interop')) {
      platform.interop.publish(InteropTopics.FilterBlotter, filters)
    } else {
      console.log(`Interop publishing is not available, skipping updating blotter filters`)
    }
    blotterWindow.focus()
    return
  }

  const baseUrl = `${windowOrigin}/blotter`
  const queryString = stringify(validateFilters(filters))
  const url = queryString ? `${baseUrl}/?${queryString}` : baseUrl

  platform.window
    .open(
      {
        ...defaultConfig,
        width: 1100,
        url,
      },
      () => (blotterWindow = null),
    )
    .then(w => (blotterWindow = w))
}
