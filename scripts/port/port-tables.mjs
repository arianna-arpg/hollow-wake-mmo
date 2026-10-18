// The unit and name mapping from the prototype (arianna-arpg/arpg-game-world) onto Hollow Wake MMO's vocabulary.
// Read by port-prototype.mjs when the drafts are generated; nothing here runs at game time. The decisions it
// encodes are recorded in docs/design/parity/port-ledger.md (the mapping section) and in the parity analysis
// (section 4: units converted once, damage types to Threads by role, statuses to our roster).

// Screen units per world unit. The prototype measures in screen units; ours is cells. Three anchors agree
// within a third: a body radius of 15 against our 0.45 (33), a basic melee reach of 55 from the edge against
// our Strike's 1.9 (29) and a walking pace of 200 a second against our 5 (40). 32 keeps a 55-unit swing at
// 1.7, a 95-unit nova at 3.0 and a 460-unit cast at 14.4, each beside the hand-authored numbers of forms.yaml.
export const UNIT = 32;

// Damage types to Threads by role. Chaos is Umbra where the theme is decay, the grave or a curse, else Void.
export const THREADS = { physical: 'stone', fire: 'ember', cold: 'tide', lightning: 'storm', chaos: 'void' };
export const UMBRA_WORDS = /poison|venom|rot|decay|blight|grave|bone|soul|shadow|gloom|wither|plague|corpse|undead|necro|dark|bleak|skelet|crypt|carrion|barrow|wraith|ghoul|spirit|death|reap|curse|hex|doom|drain|leech|umbral|shade|mire|fester|spore/i;

// Prototype attribute names to ours (requirements and attribute grants).
export const CORE_ATTRIBUTES = {
  strength: 'might', prowess: 'might', fortitude: 'might',
  dexterity: 'grace', finesse: 'grace',
  intelligence: 'will', wisdom: 'will', willpower: 'will', charisma: 'will',
  vitality: 'vigor',
};

// Prototype status ids to our roster. An id absent here keeps its own id as a new status in our vocabulary;
// an entry of null is a status that is deliberately not ported (its fabric is not part of this port).
export const STATUSES = {
  burn: 'burning', bleed: 'bleeding', chill: 'chilled', frozen: 'frozen', shock: 'shocked', stun: 'stunned',
  poison: 'poison', scalded: 'scalding', hemorrhage: 'hemorrhage', impaled: 'impaled', doom: 'doom',
  vulnerable: 'vulnerable', weaken: 'weakened', brittle: 'brittle', exposed: 'exposed', blind: 'blinded',
  disarm: 'disarmed', silence: 'silenced', rooted: 'rooted', taunted: 'taunted', unravelling: 'unraveling',
  warded: 'warded_shell', rally: 'rallied', addled: 'addled',
  impaled_physical: 'impaled', impaled_fire: 'impaled', impaled_cold: 'impaled', impaled_lightning: 'impaled', impaled_chaos: 'impaled',
  // World stand states, wet reads, clouds, vents, the fourth wall and time: fabrics outside this port.
  wading: null, swimming: null, soaked: null, sodden: null, mired: 'mired', bogged: null, seabed: null, crestborne: null,
  cloudform: null, aloft: null, cloudhaven: null, windlane: null, stormlaced: null, silverlined: null, windswept: null,
  caroming: null, vent_submerged: null, vent_sinking: null, mu_veiled: null, mu_faint: null, mu_sworn: null,
  stasis: null, temporal_drag: null, companion_recovery: null, fogveiled: null, mistfed: null, canopied: null,
  basking_torpor: null, basking_fury: null, slippery: null, caulfed: null, caulmired: null, blightfed: null, blightmired: null,
  lamprey_flop: null, swallowed: null, burrowed: null, seized: null, harried: null, bolted: 'bolted',
  wisp_ridden_pale: null, wisp_ridden_flame: null, wisp_ridden_grave: null, wisp_ridden_gloam: null,
  infected_miasma: null, infected_adrenal: null, infected_mutant: null, moonlit: null, concealed: null,
  widdershins: null, bewilder: 'bewildered', disoriented: 'disoriented', transfixed: 'transfixed',
  unhorsed: null, winded_gasp: null, guttered: null, sap_starved: null, wilted: null, sporemired: null,
  faintness: 'faintness', swoon: 'swoon', queasy: 'queasy', retching: 'retching', beheld: null, seen: null,
  heatstroke: 'heatstroke', sunscorched: 'sunscorched', waxlight: 'waxlight', melting: 'melting',
  entranced: 'entranced', petrifying: 'petrifying', petrified: 'petrified', harrowing: 'harrowing', horrified: 'horrified',
  sundered: 'sundered', scorch: 'scorch', wither_agony: 'wither_agony', unmade: 'unmade', jinxed: 'jinxed',
  maddened: 'maddened', reeling: 'reeling', voided: 'voided', prey_marked: 'prey_marked', sear: 'seared',
  decay: 'decay', torment: 'torment', contagion: 'contagion', despair: 'despair', agony: 'agony',
  indecision: 'indecision', befuddlement: 'befuddled', hedonism: 'hedonism', belligerence: 'belligerence',
  furor: 'furor', verdant_communion: 'verdant_communion', stone_communion: 'stone_communion',
  attuned_physical: 'attuned_stone', attuned_fire: 'attuned_ember', attuned_cold: 'attuned_tide',
  attuned_lightning: 'attuned_storm', attuned_chaos: 'attuned_void', kindled: 'kindled', consecrated: 'consecrated',
  phasing: 'phasing', winded: 'winded', smothered: 'smothered', gloomveiled: 'gloomveiled', hearthglow: 'hearthglow',
  emboldened: 'emboldened', quickborn: 'quickborn', blooming: 'blooming', living_bomb: 'living_bomb',
  fulgur_brand: 'fulgur_brand', doombrand: 'doombrand', powder_charge: 'powder_charge', soulchill: 'soulchill',
  bog_rot: 'bog_rot', brine_burn: 'brine_burn', sulphur_sting: 'sulphur_sting', ensnared: 'ensnared',
  flamewreathed: 'flamewreathed', starfire: 'starfire', stonelashed: 'stonelashed',
};

// Statuses of ours that deal damage over time: an application sized from the hit reads last_damage.
export const DOT_STATUSES = new Set(['burning', 'bleeding', 'poison', 'scalding', 'hemorrhage', 'sapped', 'kindling_brand', 'scorch',
  'wither_agony', 'decay', 'torment', 'bog_rot', 'brine_burn', 'sulphur_sting', 'starfire', 'flamewreathed', 'stonelashed', 'contagion']);

// Prototype tags to ours. Damage-type tags come from the Threads instead. Tags absent here keep their name.
export const TAGS = {
  aoe: 'area', warcry: 'shout', fire: 'ember', cold: 'tide', lightning: 'storm', physical: 'stone', chaos: 'void',
  'cast:timed': 'cast.timed', 'cast:instant': 'cast.instant', 'cast:channel': 'cast.channel', 'cast:held': 'cast.held',
};
// Tags the prototype folds on at load, or that name a fabric this port does not bring.
export const DROPPED_TAGS = new Set(['construct', 'conjure', 'throng', 'satellite', 'aurora', 'guardian', 'creeper']);

// Prototype mechanism names (support gates) to our probes.
export const MECHANISMS = {
  periodicConstruct: 'periodic_construct', cooldown: 'cooldown', engagement: 'engagement', strikes: 'strikes',
  displaces: 'displaces', bankless: 'bankless', guard: 'guard', landing: 'landing',
  'flight:mints': 'flight:children', 'flight:spends': 'flight:spends', 'flight:children': 'flight:children',
  surface: 'surface', 'surface:standing': 'surface:standing',
  'status:power': 'status', 'status:stacking': 'status',
};

// Prototype trigger kinds (cast-on-X supports) to our trigger kinds; null is a kind we lack.
export const TRIGGER_KINDS = {
  meleeHit: { on: 'hit', tags: ['melee'] }, crit: { on: 'crit' }, damageTaken: { on: 'hurt' },
  channelBeat: { on: 'channel_beat' }, overchargeStage: { on: 'overcharge_stage' }, channelFinish: { on: 'channel_end' },
  statusApply: { on: 'status_applied' }, block: { on: 'block' }, kill: { on: 'kill' }, highRoll: null,
  guardBeat: { on: 'guard_beat' }, flawless: { on: 'flawless' },
};

// Prototype proc triggers to our trigger kinds; null is a kind we lack (the seam is named in the ledger).
export const PROC_TRIGGERS = {
  hit: 'hit', kill: 'kill', collision: 'collision', statusApply: 'status_applied', block: 'block', evade: 'evade',
  esBreak: 'broken', esRechargeStart: null, esFilled: null, poiseBreakDealt: 'break', poiseBroken: 'broken',
  poiseBracket: null, poiseRearmed: null, chargeGain: 'charge_gained', buffGain: 'status_applied', orbPickup: null,
  surface: null, hurt: 'hurt', miss: null, foiled: null, cast: 'cast', condition: null, pulse: 'interval',
  minionDeath: 'minion_died', heal: 'heal', lastGasp: 'cheat_death', struck: 'hurt',
};

// Prototype conditions to expressions over the bearer.
export const CONDITIONS = {
  lowLife: 'has("low_life")', fullLife: 'has("full_life")', lowMana: 'has("low_mana")', fullMana: 'has("full_mana")',
  hasEs: 'shield_pct > 0', fullEs: 'has("full_shield")', lowEs: 'has("low_shield")', guarding: 'has("guarding")',
  stationary: 'has("stationary")', moving: 'has("moving")', poised: '!has("pool.poise.broken")', poiseBroken: 'has("pool.poise.broken")',
  esRecharging: 'has("pool.shield.recharging")', comboVaried: 'has("combo_varied")', comboRepeated: 'has("combo_repeated")',
  recentlyHit: 'has("recently_hit")', recentlyCrit: 'has("recently_crit")', recentlyKilled: 'has("recently_killed")',
  recentlyHurt: 'has("recently_hurt")', notHurtRecently: '!has("recently_hurt")', recentlyBlocked: 'has("recently_blocked")',
  recentlyEvaded: 'has("recently_evaded")', recentlyMoved: 'has("recently_moved")', recentlyHealed: 'has("recently_healed")',
  strided: null,
};

// Victim-scope tags: vs:<name> to vs.<name>. Statuses map through STATUSES; these are the rest.
export const VICTIM_TAGS = {
  closeRing: 'near', lowLife: 'low_life', fullLife: 'full_life', behind: 'behind', elite: 'elite', empowered: 'elite',
  higherLevel: 'higher_level', heavier: 'heavier', immobile: 'immobile', rooted: 'rooted', spent: 'spent',
  uprooted: 'uprooted', bonded: 'bonded', faltering: 'faltering', aloft: 'aloft', composite: 'composite', torment: 'torment',
};

// Plain prototype stats to our attributes. `scope` adds tags, `form` overrides the modifier's form, `scale`
// multiplies the value, `link` turns the line into a link from an attribute. Missing names are reported.
export const STATS = {
  damage: { attribute: 'damage' },
  effectDuration: { attribute: 'effect.duration' },
  minionDamage: { attribute: 'minion.damage' },
  minionLife: { attribute: 'minion.life' },
  aoeRadius: { attribute: 'area.radius' },
  aoeScale: { attribute: 'area.radius' },
  castSpeed: { attribute: 'cast.speed' },
  manaCost: { attribute: 'cost' },
  moveSpeed: { attribute: 'move.speed' },
  armor: { attribute: 'armor' },
  attackSpeed: { attribute: 'attack.speed' },
  evasion: { attribute: 'evasion' },
  poise: { attribute: 'poise.max' },
  accuracy: { attribute: 'accuracy' },
  mana: { attribute: 'mana.max' },
  critMulti: { attribute: 'crit.multiplier' },
  energyShield: { attribute: 'shield.max' },
  healPower: { attribute: 'healing' },
  statusMagnitude: { attribute: 'ailment.potency' },
  life: { attribute: 'life.max' },
  lifeRegen: { attribute: 'life.regen' },
  statusChance: { attribute: 'ailment.chance' },
  critChance: { attribute: 'crit.chance' },
  minionMoveSpeed: { attribute: 'minion.move_speed' },
  cooldownRecovery: { attribute: 'cooldown.recovery' },
  poiseDamage: { attribute: 'poise.damage' },
  guardStrength: { attribute: 'guard.strength' },
  manaRegen: { attribute: 'mana.regen' },
  damageTaken: { attribute: 'damage.taken' },
  projectileSpeed: { attribute: 'projectile.speed' },
  insight: { attribute: 'insight.max' },
  coldRes: { attribute: 'resist.tide' },
  fireRes: { attribute: 'resist.ember' },
  lightningRes: { attribute: 'resist.storm' },
  chaosRes: { attribute: 'resist.void' },
  coldResMax: { attribute: 'resist.tide.max' },
  fireResMax: { attribute: 'resist.ember.max' },
  lightningResMax: { attribute: 'resist.storm.max' },
  chaosResMax: { attribute: 'resist.void.max' },
  coldPen: { attribute: 'pen.tide' },
  firePen: { attribute: 'pen.ember' },
  lightningPen: { attribute: 'pen.storm' },
  chaosPen: { attribute: 'pen.void' },
  armorPen: { attribute: 'pen.armor' },
  lifeLeech: { attribute: 'leech.life' },
  manaLeech: { attribute: 'leech.mana' },
  esLeech: { attribute: 'leech.shield' },
  lifeRegenPct: { attribute: 'life.regen', link: 'life.max' },
  manaRegenPct: { attribute: 'mana.regen', link: 'mana.max' },
  poiseRegenPct: { attribute: 'poise.regen', link: 'poise.max' },
  blockChance: { attribute: 'block.chance' },
  blockValue: { attribute: 'block.value' },
  knockback: { attribute: 'knockback', form: 'increased', scale: 1 / 130 },
  displaceForce: { attribute: 'knockback', form: 'increased', scale: 1 / 130 },
  minionMaxCount: { attribute: 'minion.max' },
  minionHaste: { attribute: 'minion.speed' },
  minionRegenPct: { attribute: 'minion.regen' },
  minionRegen: { attribute: 'minion.regen' },
  minionDamageTaken: { attribute: 'minion.damage_taken' },
  lifeOnKill: { attribute: 'on_kill.life' },
  manaOnKill: { attribute: 'on_kill.mana' },
  esOnKill: { attribute: 'on_kill.shield' },
  lifeOnHit: { attribute: 'on_hit.life' },
  esOnHit: { attribute: 'on_hit.shield' },
  poiseOnHit: { attribute: 'on_hit.poise' },
  addedPhysical: { attribute: 'added.stone' },
  addedFire: { attribute: 'added.ember' },
  addedCold: { attribute: 'added.tide' },
  addedLightning: { attribute: 'added.storm' },
  addedChaos: { attribute: 'added.void' },
  esRechargeRate: { attribute: 'shield.recharge_rate' },
  esRechargeDelay: { attribute: 'shield.recharge_delay' },
  chainCount: { attribute: 'chain.count' },
  projectileCount: { attribute: 'projectile.count' },
  skillCharges: { attribute: 'uses.max' },
  skillChargeRate: { attribute: 'uses.rate' },
  pierceCount: { attribute: 'projectile.pierce' },
  repeatCount: { attribute: 'repeat.count' },
  repeatScale: { attribute: 'repeat.scale' },
  repeatRetarget: { attribute: 'repeat.retarget' },
  repeatLock: { attribute: 'repeat.lock' },
  addedCooldown: { attribute: 'cooldown.added' },
  triggerChance: { attribute: 'trigger.chance' },
  ailmentStacks: { attribute: 'status.stacks' },
  ailmentResist: { attribute: 'status.resistance' },
  ambushBonus: { attribute: 'damage', form: 'more', scope: ['vs.behind'] },
  erraticPower: { attribute: 'projectile.erratic', scale: 1 },
  spinPower: { attribute: 'projectile.spin' },
  spiralPower: { attribute: 'projectile.spiral' },
  weavePower: { attribute: 'projectile.wave' },
  homingPower: { attribute: 'projectile.homing' },
  orbitPower: { attribute: 'projectile.orbit' },
  accelPower: { attribute: 'projectile.accel' },
  bouncePower: { attribute: 'projectile.bounce' },
  projReturn: { attribute: 'projectile.return' },
  projPulse: { attribute: 'area.pulses' },
  forkCount: { attribute: 'projectile.forks' },
  forks: { attribute: 'projectile.forks' },
  phasing: { attribute: 'phasing' },
  channelMobility: { attribute: 'cast.mobility', scope: ['cast.channel'] },
  castMobility: { attribute: 'cast.mobility' },
  meleeReach: { attribute: 'melee.reach' },
  swingArc: { attribute: 'area.angle' },
  randomArc: { attribute: 'aim.spread' },
  reflex: { attribute: 'reflex' },
  cullThreshold: { attribute: 'cull.threshold' },
  critAvoid: { attribute: 'crit.avoid' },
  dotCrit: { attribute: 'crit.dot' },
  addedEffectiveness: { attribute: 'added.effectiveness' },
  staggerWindow: { attribute: 'stagger.fraction' },
  hitCap: { attribute: 'hit.cap' },
  lastGasp: { attribute: 'cheat_death.chance' },
  summonCount: { attribute: 'summon.count' },
  summonDuration: { attribute: 'duration', scope: ['summon'] },
  constructMaxCount: { attribute: 'summon.max', scope: ['construct'] },
  constructCastRate: { attribute: 'cast.speed', scope: ['construct'] },
  stormCount: { attribute: 'area.strikes' },
  cascadeCount: { attribute: 'area.cascade' },
  scatterCount: { attribute: 'area.scatter' },
  pulseCount: { attribute: 'area.pulses' },
  fuseDelay: { attribute: 'fuse.delay' },
  fusePower: { attribute: 'fuse.power' },
  overchargeStages: { attribute: 'overcharge.stages' },
  reloadSpeed: { attribute: 'cast.speed', scope: ['reload'] },
  spreadAngle: { attribute: 'projectile.spread' },
  volleySpacing: null,
  projectileSize: null,
  overmatch: { attribute: 'damage', form: 'more', scope: ['vs.higher_level'] },
  giantsbane: { attribute: 'damage', form: 'more', scope: ['vs.heavier'] },
  regicide: { attribute: 'damage', form: 'more', scope: ['vs.elite'] },
  siegebreaker: { attribute: 'damage', form: 'more', scope: ['vs.immobile'] },
  limbreaver: null, spentbane: { attribute: 'damage', form: 'more', scope: ['vs.spent'] },
  uprooter: { attribute: 'damage', form: 'more', scope: ['vs.rooted'] },
  bondbreaker: null, quailbane: { attribute: 'damage', form: 'more', scope: ['vs.faltering'] },
  thorns: { attribute: 'thorns' },
  thornsReflect: { attribute: 'thorns' },
  projBounce: { attribute: 'projectile.bounce' },
  aoeScatter: { attribute: 'area.scatter' },
  tetherDamage: { attribute: 'damage', scope: ['tether'] },
  strength: { attribute: 'might' }, prowess: { attribute: 'might' }, fortitude: { attribute: 'might' }, dexterity: { attribute: 'grace' }, finesse: { attribute: 'grace' },
  intelligence: { attribute: 'will' }, wisdom: { attribute: 'will' }, willpower: { attribute: 'will' }, charisma: { attribute: 'will' }, vitality: { attribute: 'vigor' },
  threatGen: { attribute: 'threat' },
  lowLifeLine: { attribute: 'low_life.line' },
  fortify: null, endurance: null, weight: null, shoveAuthority: null, impactDamage: null, gripPower: null,
  luck: null, luckyChance: null, damageSpread: null, highRollWindow: null,
  conduitRate: null, conduitEfficiency: null, detectability: null, detectionRange: null,
  wardGain: { attribute: 'ward.max' }, wardDecay: null, insightSap: null, insightPen: null, recuperate: null,
  healTaken: null, overheal: null, restorePower: null, restorePctMax: null, orbShedRate: null,
  trample: null, plyRend: null, comboWindow: null, mirageCount: null, mirageDamage: null,
  brimPower: null, brimFill: null, brimDecay: null, channelThorns: null, channelTurnRate: null,
  guardParry: null, guardParryPower: null, guardAegis: null, bashPower: null, bashFloor: null, bashInvert: null,
  curseRupture: null, dotPropagates: null, afflictionYield: null, hitToAffliction: null, remnantChance: null,
  minionUndying: null, minionSize: null, corpseBatch: null, esShred: null, vampiricShare: null, multiTarget: null,
  possessDuration: null, possessPower: null, throngYield: null, wriggle: null, mercVigor: null, mercHireDiscount: null,
  strideReach: null, traction: null, levitation: null, reservedDamage: null, maxManaDamage: null, echoMight: null,
  proximityDamage: null, doomDot: null, impalePower: null, poiseOvercharge: null, esRechargeSteadfast: null,
  afflictionExpiry: null, noiseOnHit: null, finalRoundDamage: null, moveExplode: null,
};

// Generated stat families: a regular expression and how the match becomes an attribute (or why it cannot).
export const STAT_FAMILIES = [
  { re: /^apply_(.+)$/, to: m => ({ attribute: `status.${status(m[1])}.chance` }), needsStatus: 1 },
  { re: /^minionApply_(.+)$/, to: m => ({ attribute: `minion.status.${status(m[1])}.chance` }), needsStatus: 1 },
  { re: /^damageVs_(.+)$/, to: m => ({ attribute: 'damage', scope: [`vs.${victim(m[1])}`] }) },
  { re: /^convert_(\w+?)_(\w+)$/, to: m => ({ attribute: `convert.${THREADS[m[1]]}.${THREADS[m[2]]}` }) },
  { re: /^extraAs_(\w+)$/, to: m => ({ attribute: `extra.${THREADS[m[1]]}` }) },
  { re: /^addedMin_(\w+)$/, to: m => ({ attribute: `added.${THREADS[m[1]]}`, scale: 0.5, note: 'min and max fold into one added value' }) },
  { re: /^addedMax_(\w+)$/, to: m => ({ attribute: `added.${THREADS[m[1]]}`, scale: 0.5, note: 'min and max fold into one added value' }) },
  { re: /^proc_(.+)$/, to: m => ({ attribute: `proc.${m[1]}` }), needsProc: 1 },
  { re: /^procPower_(.+)$/, to: () => null, why: 'proc power dial: ours has no magnitude dial per proc (the payload reads its own numbers)' },
  { re: /^dotLeech_(.+)$/, to: m => ({ attribute: `status.${status(m[1])}.leech` }), needsStatus: 1 },
  { re: /^popPower_(.+)$/, to: m => ({ attribute: `status.${status(m[1])}.pop` }), needsStatus: 1 },
  { re: /^slot[gG]raft_(\d+)_(.+)$/, to: m => ({ attribute: `glyph.slot${m[1]}.${m[2]}` }), needsGlyph: 2 },
  { re: /^skill[gG]rant_(.+)$/, to: () => null, why: 'skill grants (parity row D21, open)' },
  { re: /^chargeCap_(.+)$/, to: m => ({ attribute: `charge.${m[1]}.max` }) },
  { re: /^chargeRegen_(.+)$/, to: () => null, why: 'charge regeneration per kind: ours has no accrual clock on a charge' },
  { re: /^takenAs_(\w+?)_(\w+)$/, to: m => ({ attribute: `taken_as.${THREADS[m[1]]}.${THREADS[m[2]]}` }) },
  { re: /^costDamage_(\w+)$/, to: () => null, why: 'cost as damage: a link from the last_cost gauge, authored per weave' },
  { re: /^(orbOnHit|orbOnKill|orbRefund|orbShed)_/, to: () => null, why: 'resource orbs (parity row C23, open)' },
  { re: /^(sympathy|wornThrong|satelliteCount|creeperCount|guardianCount|auroraCapacity|survivalEase|comboStat|throngFind|mercSlots)_/, to: () => null, why: 'a fabric outside this port' },
  { re: /^wornConduit_/, to: () => null, why: 'conduits (parity row H7, open)' },
];

export function status(id) {
  const s = STATUSES[id];
  return s === undefined ? id : s;
}

export function victim(id) {
  if (VICTIM_TAGS[id]) return VICTIM_TAGS[id];
  const s = STATUSES[id];
  return s === undefined ? id : (s ?? id);
}

// Prototype monsters a summon raises, onto our patterns (named differences, each).
export const PATTERNS = {
  zombie: 'remnant_shade', skeleton_warrior: 'remnant_shade', skeleton_archer: 'remnant_archer',
  skeletal_pyromancer: 'remnant_archer', skeletal_cryomancer: 'remnant_archer', skeletal_stormcaller: 'remnant_archer',
  skeletal_venomancer: 'remnant_archer', skeletal_cleric: 'sprig', flame_sprite: 'cinder_imp', fungal_sporeling: 'sprig',
  chitin_drone: 'sprig', chitin_lancer: 'wolf', raging_spirit: 'remnant_shade', formic_worker: 'sprig', formic_soldier: 'wolf',
  harvester: 'stone_warden', rubblekin: 'stone_warden', rootwild_burrling: 'tangle', heat_double: 'shade_echo',
  hellbore_engine: 'stone_warden', ash_whelp: 'cinder_imp', imp: 'cinder_imp', cherub: 'sprig', lampad_chorister: 'sprig',
  plaguefather: 'stone_warden', stone_golem: 'stone_warden', bone_golem: 'stone_warden', arcane_familiar: 'remnant_archer',
  spirit_wisp: 'remnant_shade', fire_golem: 'stone_warden', ice_golem: 'stone_warden', blood_golem: 'stone_warden',
  blade_wraith: 'remnant_shade', swarmling: 'sprig', hunting_falcon: 'wolf', mender_sprite: 'sprig', crimson_bat: 'wolf',
  feeding_thrall: 'remnant_shade', vampire_thrall: 'remnant_shade', spiderling: 'sprig', scree_skitter: 'sprig',
  giant_maggot: 'sprig', decay_wraith: 'remnant_shade', bombard_demon: 'stone_warden', flame_core: 'cinder_imp',
  fire_cultist: 'remnant_archer', frost_witch: 'remnant_archer', brute: 'stone_warden', resonant_shardling: 'cinder_imp',
  prism_creeper: 'cinder_imp', lumen_wisp: 'remnant_shade', mushroomling: 'sprig', fungal_puffball: 'sprig', rockgrub: 'boar',
  cave_bat: 'wolf', lesser_ooze: 'tangle', blood_mite: 'sprig', bound_scarecrow: 'stone_warden', carrion_crow: 'wolf',
  warren_rat: 'wolf', fester_rat: 'wolf', grave_shambler: 'remnant_shade', lesser_reaper: 'remnant_shade', broodling: 'sprig',
  scald_spawn: 'tide_crawler', cirrus_fingerling: 'remnant_shade', shadow_self: 'shade_echo', cinder_sprite: 'cinder_imp', phantasm: 'shade_echo',
};

// Prototype construct kinds onto our device patterns.
export const CONSTRUCTS = {
  totem: 'ember_totem', sentry: 'ember_totem', pylon: 'ember_totem', eruptor: 'ember_totem', relic: 'ember_totem',
  trap: 'snare_trap', mine: 'snare_trap', pod: 'snare_trap', dome: 'lumen_dome', barrier: 'device', gate: 'device',
  pad: 'device', tree: 'device', echo: 'shade_echo',
};

// Effect kinds of the prototype that this port cannot express, with the parity row or fabric.
export const BLOCKED_EFFECTS = {
  grabSeize: 'the grab fabric (row C37, later)', grabThrow: 'the grab fabric (row C37, later)',
  conjure: 'cloud bridges (row C36, later)', litePour: 'lightweight swarms (row C36, later)', lure: 'false attraction (row C36, later)',
  vent: 'fog banks (row C36, later)', kindle: 'lightwells (row C36, later)', townPortal: 'portals (row C36, later)',
  throngDirect: 'the throng fabric (row C38, later)', possess: 'possession (row C38, later)', possessEnd: 'possession (row C38, later)',
  shapeshift: 'shapeshifting (row C38, later)', mimicSelect: 'mimicry (row C38, later)',
  tame: 'companions (row C35, later)', whistleCompanion: 'companions (row C35, later)', companionStance: 'companions (row C35, later)',
  dragCorpses: 'corpse piles (row C26, open)', relocateConstruct: 'relocating constructs (row C20, open)',
  shatterConstructs: 'ported as detonate on owned constructs (row C27)', payLedger: 'toggle ledgers (row C29, open)',
  recallImpales: 'recalling impales (row C33, open)', terrain: 'timed terrain (row C11, open)', siphonOrb: 'resource orbs (row C23, open)',
};
