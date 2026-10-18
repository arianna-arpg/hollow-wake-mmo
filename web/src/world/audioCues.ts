/**
 * Where sound will attach when assets arrive, the twin of the desktop's AudioCues: a graph of gain nodes
 * (music, effects, ambience, interface) under the master, and one call per moment the animator and the
 * world raise (a footstep on a surface, a swing, a hit, a death, the water's edge). A cue plays the buffer
 * registered under its name, most specific first (footstep.grass before footstep); with nothing registered,
 * as today, it plays nothing and costs a map lookup. The context is created on the first cue after a user
 * gesture, as browsers require. No asset ships yet.
 */
export class AudioCues {
  private context: AudioContext | null = null;
  private readonly buses = new Map<string, GainNode>();
  private readonly buffers = new Map<string, AudioBuffer>();
  /** Cues raised, by name, for the smoke to see the hooks fire. */
  readonly raised = new Map<string, number>();
  private masterVolume = 0.8;

  /** Registers a buffer for a cue name (an asset pack's job); cues with no buffer stay silent. */
  register(cue: string, buffer: AudioBuffer): void {
    this.buffers.set(cue, buffer);
  }

  setMasterVolume(linear: number): void {
    this.masterVolume = Math.min(1, Math.max(0, linear));
    const master = this.buses.get('master');
    if (master) {
      master.gain.value = this.masterVolume;
    }
  }

  private ensure(): AudioContext | null {
    if (this.context !== null) {
      return this.context;
    }
    if (typeof AudioContext === 'undefined') {
      return null;
    }
    const context = new AudioContext();
    const master = context.createGain();
    master.gain.value = this.masterVolume;
    master.connect(context.destination);
    this.buses.set('master', master);
    for (const bus of ['music', 'effects', 'ambience', 'interface']) {
      const gain = context.createGain();
      gain.connect(master);
      this.buses.set(bus, gain);
    }
    this.context = context;
    return context;
  }

  /**
   * Raises a cue: `footstep` with a surface variant (`footstep.grass`), `swing`, `hit`, `death`, `splash`.
   * Plays the most specific registered buffer, if any, on the effects bus; distance attenuation is a gain from
   * how far the place is from the listener, when a listener position is given.
   */
  play(cue: string, variant: string | null, distance = 0): void {
    const name = variant === null ? cue : `${cue}.${variant}`;
    this.raised.set(name, (this.raised.get(name) ?? 0) + 1);
    const buffer = this.buffers.get(name) ?? this.buffers.get(cue);
    if (!buffer) {
      return;
    }
    const context = this.ensure();
    const bus = this.buses.get('effects');
    if (!context || !bus) {
      return;
    }
    const source = context.createBufferSource();
    source.buffer = buffer;
    const gain = context.createGain();
    gain.gain.value = Math.max(0, 1 - distance / 40);
    source.connect(gain);
    gain.connect(bus);
    source.start();
  }
}
