import { Capacitor } from '@capacitor/core'
import { Geolocation } from '@capacitor/geolocation'

export type LocationResult =
  | { ok: true; lat: number; lon: number }
  | { ok: false; reason: 'denied' | 'unavailable' }

/** Checks without prompting, so the app can locate on start only if the user already allowed it. */
export async function locationGranted(): Promise<boolean> {
  try {
    return (await Geolocation.checkPermissions()).location === 'granted'
  } catch {
    return false // no Permissions API in this browser
  }
}

/**
 * Asks for the position only when the user explicitly chooses "use my location".
 * Native apps go through the OS permission dialog; browsers show their own prompt.
 */
export async function locateOnce(): Promise<LocationResult> {
  try {
    if (Capacitor.isNativePlatform()) {
      let status = await Geolocation.checkPermissions()
      if (status.location !== 'granted') status = await Geolocation.requestPermissions({ permissions: ['coarseLocation'] })
      if (status.location === 'denied') return { ok: false, reason: 'denied' }
    }
    // Low accuracy is enough to find stops and is faster and lighter on the battery.
    const pos = await Geolocation.getCurrentPosition({ enableHighAccuracy: false, timeout: 15000, maximumAge: 60000 })
    return { ok: true, lat: pos.coords.latitude, lon: pos.coords.longitude }
  } catch (e) {
    const denied = e instanceof Error && /denied|permission/i.test(e.message)
    return { ok: false, reason: denied ? 'denied' : 'unavailable' }
  }
}
