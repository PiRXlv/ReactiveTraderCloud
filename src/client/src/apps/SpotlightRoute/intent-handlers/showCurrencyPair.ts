import { InteropTopics, PlatformAdapter } from 'rt-platforms'
import { defaultConfig, windowOrigin } from './defaultWindowConfig'

let currencyPairWindow: Window | undefined

export function showCurrencyPair(currencyPair: string, platform: PlatformAdapter) {
  currencyPair = currencyPair.toUpperCase()

  if (!currencyPairWindow || currencyPairWindow.closed) {
    platform.window
      .open(
        {
          ...defaultConfig,
          width: 380,
          height: 180,
          url: `${windowOrigin}/spot/${currencyPair}?tileView=Normal`,
        },
        () => (currencyPairWindow = undefined),
      )
      .then(w => (currencyPairWindow = w))
  } else if (platform.hasFeature('interop')) {
    platform.interop.publish(InteropTopics.FilterCurrencyPair, currencyPair)
  }
}
