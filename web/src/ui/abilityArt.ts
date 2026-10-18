import type { AbilityFace } from './logic/abilityIcons';
import type { Seat } from './logic/heroState';
import { css, type Skin } from './skin';

/** Every surface uses this same vector face; cooldowns and input remain with the caller. */
export function abilityArt(skin: Skin, face: AbilityFace, pixels: number, className = '', dormant = false): HTMLElement {
  const host = document.createElement('span');
  host.className = `ability-art ${className}`;
  host.setAttribute('aria-hidden', 'true');
  host.dataset.art = face.art;
  Object.assign(host.style, { display: 'inline-block', width: `${pixels}px`, height: `${pixels}px`, flex: 'none', pointerEvents: 'none', lineHeight: '0', verticalAlign: 'middle' });
  if (dormant) face = { ...face, tone: css(skin.state('dormant', '#808080')) };
  // Svg() only emits escaped path/matrix/paint data, with no URLs or executable markup.
  host.innerHTML = skin.names.abilityIcons.svg(face);
  return host;
}

export function seatArt(skin: Skin, seat: Seat, pixels: number, className = ''): HTMLElement {
  return abilityArt(skin, skin.names.abilityIcons.seat(seat.key, seat.info?.form, seat.info?.preset, seat.converted, seat.threads), pixels, className, seat.dormant !== null);
}
