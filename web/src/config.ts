import builtIn from '../public/config.json';

/**
 * Client settings. public/config.json is the single source: it is compiled in as the defaults and fetched
 * again beside the page at startup, so an operator can retune a deployed client by editing dist/config.json.
 * Nothing here is gameplay; it is how the client looks and connects. The player's preferences and every key
 * binding are content (ui_options and input_actions under content/core/ui) with the player's settings over them.
 */

export interface ServerSettings {
  /** Empty: the page's own host when a game server answers there, otherwise whatever the player types. */
  host: string;
  /** 0: the page's port, or 443/80 by scheme. */
  port: number;
  /** null: secure (wss) when the page itself is https. */
  secure: boolean | null;
  path: string;
  account: string;
  /** Look for a game server at the page's own origin (a plain GET on the socket path describes it). */
  probeSameOrigin: boolean;
  /** Hello features to ask for, such as entity_delta; a `?full-states` link asks for none. */
  features: string[];
}

export interface WorldSettings {
  chunkRadius: number;
  maxChunkRequestsInFlight: number;
  chunkRequestTimeoutSeconds: number;
  chunkRetrySeconds: number;
  meshBuildsPerFrame: number;
  pingSeconds: number;
}

export interface CameraSettings {
  mode: string;
  isoYawDegrees: number;
  isoPitchDegrees: number;
  isoSize: number;
  isoSizeMin: number;
  isoSizeMax: number;
  isoZoomStep: number;
  isoDistance: number;
  isoRotateStepDegrees: number;
  isoFollowRate: number;
  thirdFovDegrees: number;
  thirdDistance: number;
  thirdDistanceMin: number;
  thirdDistanceMax: number;
  thirdZoomStep: number;
  thirdPitchDegrees: number;
  thirdPitchMinDegrees: number;
  thirdPitchMaxDegrees: number;
  thirdRotateStepDegrees: number;
  thirdEyeHeight: number;
  thirdFollowRate: number;
  orbitSensitivity: number;
  pitchSensitivity: number;
  wheelStepPixels: number;
  near: number;
  far: number;
}

export interface MotionSettings {
  interpolationDelayMs: number;
  maxExtrapolationMs: number;
  selfSmoothing: number;
}

export interface InputSettings {
  /** How near the nearest interactable must be for the interact and pick-up keys. */
  interactReach: number;
}

export interface RenderSettings {
  maxPixelRatio: number;
  antialias: boolean;
  shadows: boolean;
  background: string;
  fogColor: string;
  fogDensity: number;
  sunColor: string;
  sunIntensity: number;
  sunPitchDegrees: number;
  sunYawDegrees: number;
  ambientSky: string;
  ambientGround: string;
  ambientIntensity: number;
}

/** A rule matches a cell when the cell has every listed surface tag and every listed cell flag. */
export interface SurfaceRule {
  tags?: string[];
  flags?: string[];
  color?: string;
  mix?: number;
  darken?: number;
}

export interface TerrainPaletteSettings {
  base: SurfaceRule[];
  fallback: string;
  structure: SurfaceRule[];
  structureFallback: string;
  tints: SurfaceRule[];
  skirtDarken: number;
  layers: SurfaceRule[];
  layerFallback: string;
  waterColor: string;
  waterOpacity: number;
}

export interface FloraRule {
  match: string[];
  color: string;
  tree: boolean;
}

export interface EntityPaletteSettings {
  player: string;
  drops: Record<string, string>;
  projectile: string;
  field: string;
  fieldOpacity: number;
  /** The ground mark where a telegraphed strike will land, and how opaque it is at its height. */
  telegraph: string;
  telegraphOpacity: number;
  poi: string;
  fount: string;
  flora: FloraRule[];
  floraFallback: string;
  trunk: string;
  factions: Record<string, string>;
  rarities: Record<string, string>;
  creatureFallback: string;
  labelColors: Record<string, string>;
  labelDefault: string;
  deadOpacity: number;
}

export interface ClientConfig {
  server: ServerSettings;
  world: WorldSettings;
  camera: CameraSettings;
  motion: MotionSettings;
  input: InputSettings;
  render: RenderSettings;
  terrain: TerrainPaletteSettings;
  entities: EntityPaletteSettings;
}

/** The compiled-in defaults: the same file the page fetches, checked against the types above. */
export const DEFAULTS: ClientConfig = builtIn;

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Deep-merges plain objects; arrays and scalars from the override replace the base. Unknown keys are kept. */
export function merge<T>(base: T, override: unknown): T {
  if (!isPlainObject(base) || !isPlainObject(override)) {
    return (override === undefined ? base : override) as T;
  }
  const out: Record<string, unknown> = { ...base };
  for (const [key, value] of Object.entries(override)) {
    out[key] = key in out ? merge(out[key], value) : value;
  }
  return out as T;
}

/** Defaults, then config.json beside the page (if it loads), so a deployment can change any field without a rebuild. */
export async function loadConfig(url = 'config.json'): Promise<ClientConfig> {
  try {
    const response = await fetch(url, { cache: 'no-cache' });
    if (!response.ok) {
      return DEFAULTS;
    }
    return merge(DEFAULTS, await response.json());
  } catch (error) {
    console.warn('config.json could not be read; using built-in settings', error);
    return DEFAULTS;
  }
}
