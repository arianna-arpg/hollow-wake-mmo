// Isolated executable semantics for the proposed contract, not production protocol code.
// Numeric fixture fields are already resolved server values; this module never evaluates damage.
export function progress(record, serverNow) {
  if (record.phase === 'ended' || record.endsAt == null) return null;
  if (record.endsAt <= record.startsAt) return 1;
  return Math.max(0, Math.min(1, (serverNow - record.startsAt) / (record.endsAt - record.startsAt)));
}

export class ForecastView {
  constructor(epoch) {
    this.epoch = epoch;
    this.watermark = -1;
    this.lastSequence = -1;
    this.records = new Map();
    this.tombstones = new Map();
    this.cues = [];
  }

  accept(packet, { baseline = false } = {}) {
    if (packet.zoneEpoch !== this.epoch || packet.sequence <= this.watermark) return false;
    const previous = this.records.get(packet.castId);
    const terminal = this.tombstones.get(packet.castId);
    if (terminal != null || (previous && previous.revision >= packet.revision)) return false;
    this.lastSequence = Math.max(this.lastSequence, packet.sequence);
    this.records.set(packet.castId, structuredClone(packet));
    if (packet.phase === 'ended') this.tombstones.set(packet.castId, packet.revision);
    if (!baseline && packet.emissionOrdinal > (previous?.emissionOrdinal ?? 0))
      this.cues.push({ castId: packet.castId, kind: 'release', ordinal: packet.emissionOrdinal });
    // Confirmed emission is not a confirmed hit. No damage cue originates here.
    return true;
  }

  baseline({ zoneEpoch, sequence, active }) {
    if (zoneEpoch !== this.epoch || sequence <= this.lastSequence || sequence <= this.watermark) return false;
    // A complete baseline at S cannot resurrect a cast ended by a newer event.
    this.records.clear();
    this.tombstones.clear();
    for (const packet of active) this.accept({ ...packet, zoneEpoch, sequence }, { baseline: true });
    this.lastSequence = sequence;
    this.watermark = sequence;
    return true;
  }

  state(castId, serverNow, grace) {
    const record = this.records.get(castId);
    if (!record) return { phase: 'absent', progress: null };
    if (record.phase === 'ended') return { phase: 'ended', reason: record.reason, progress: null };
    if (serverNow > record.validUntil + grace) return { phase: 'unknown_end', progress: null };
    return { ...record, progress: progress(record, serverNow) };
  }
}

export function directionAfterCommit({ facing, requested, committed, maxTurnRadiansPerSecond }, dt) {
  if (committed) return facing;
  const delta = Math.atan2(Math.sin(requested - facing), Math.cos(requested - facing));
  return facing + Math.max(-maxTurnRadiansPerSecond * dt, Math.min(maxTurnRadiansPerSecond * dt, delta));
}

export function displayThread(id, aliases) { return aliases[id]?.name ?? id; }
