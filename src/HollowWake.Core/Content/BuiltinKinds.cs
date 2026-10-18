using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Content;

/// <summary>Registers every built-in definition kind with its validator. Mods add theirs the same way.</summary>
internal static class BuiltinKinds
{
    private static bool _done;

    public static void RegisterAll()
    {
        if (_done)
        {
            return;
        }
        _done = true;
        Kinds.EntryKinds.Register();

        ContentKinds.Register<AttributeDef>("attributes", (d, c) =>
        {
            c.Expression(d.Formula, "formula");
            if (d.Min is { } min && d.Max is { } max && min > max)
            {
                c.Error("min is greater than max.");
            }
        });
        ContentKinds.Register<ResourceDef>("resources", (d, c) =>
        {
            if (!c.Registry.Attributes.Contains(d.Max))
            {
                c.Warn($"max attribute '{d.Max}' is not declared.");
            }
        });
        ContentKinds.Register<ThreadDef>("threads", (d, c) =>
        {
            if (d.Opposes is not null)
            {
                c.Ref<ThreadDef>(d.Opposes, "opposes");
            }
            if (d.Status is not null)
            {
                c.Ref<StatusDef>(d.Status, "status");
            }
            if (d.Mitigation is not ("resist" or "armor" or "none"))
            {
                c.Error($"mitigation must be resist, armor or none, not '{d.Mitigation}'.");
            }
        });
        ContentKinds.Register<TagDef>("tags");
        ContentKinds.Register<RulesDef>("rules");
        ContentKinds.Register<SlotDef>("slots");
        ContentKinds.Register<FactionDef>("factions", (d, c) =>
        {
            foreach (var other in d.Relations.Keys)
            {
                c.Ref<FactionDef>(other, "relations");
            }
        });

        ContentKinds.Register<StatusDef>("statuses", (d, c) =>
        {
            c.Modifiers(d.Modifiers, "modifiers");
            c.Expression(d.Duration, "duration");
            if (d.BuildupInto is not null)
            {
                c.Ref<StatusDef>(d.BuildupInto, "buildup_into");
            }
            if (d.MaxStacks < 1)
            {
                c.Error("max_stacks must be at least 1.");
            }
        });
        ContentKinds.Register<FormDef>("forms", (d, c) =>
        {
            c.Expression(d.Range, "range");
            c.Expression(d.CastTime, "cast_time");
            c.Expression(d.Cooldown, "cooldown");
            foreach (var (k, e) in d.Costs)
            {
                c.Expression(e, $"costs.{k}");
                if (!c.Registry.Has<ResourceDef>(k))
                {
                    c.Warn($"cost resource '{k}' is not a declared resource.");
                }
            }
            if (d.Effects.Count == 0 && d.Mechanisms.IsEmpty)
            {
                c.Warn("form has no effects.");
            }
        });
        ContentKinds.Register<GlyphDef>("glyphs", (d, c) =>
        {
            c.Modifiers(d.Modifiers, "modifiers");
            c.Modifiers(d.PerLevel, "per_level");
            WeaveValidation.Glyph(d, c);
        });
        ContentKinds.Register<WeaveDef>("weaves", (d, c) =>
        {
            var refsOk = c.Ref<FormDef>(d.Form, "form");
            foreach (var t in d.Threads)
            {
                refsOk &= c.Ref<ThreadDef>(t, "threads");
            }
            refsOk &= WeaveValidation.GlyphRefs(d.Glyphs, c, "glyphs");
            c.Modifiers(d.Modifiers, "modifiers");
            if (refsOk)
            {
                WeaveValidation.Fits(new WeaveSpec { Preset = d.Id }, c, "preset");
            }
        });
        ContentKinds.Register<MonsterRarityDef>("monster_rarities", (d, c) => c.Modifiers(d.Modifiers, "modifiers"));
        ContentKinds.Register<MonsterAffixDef>("monster_affixes", (d, c) => c.Modifiers(d.Modifiers, "modifiers"));

        ContentKinds.Register<ItemBaseDef>("items", (d, c) =>
        {
            if (d.Slot is not null)
            {
                c.Ref<SlotDef>(d.Slot, "slot", required: false);
            }
            c.Modifiers(d.Implicits, "implicits");
            foreach (var (k, range) in d.Local)
            {
                if (range.Length is not (1 or 2))
                {
                    c.Error($"local.{k} must be [value] or [min, max].");
                }
            }
        });
        ContentKinds.Register<AffixDef>("affixes", (d, c) =>
        {
            c.Modifiers(d.Lines, "lines");
            if (d.Tiers.Count == 0)
            {
                c.Error("affix has no tiers.");
            }
            foreach (var t in d.Tiers)
            {
                if (t.Ranges.Count != d.Lines.Count)
                {
                    c.Error($"tier at level {t.Level} has {t.Ranges.Count} ranges for {d.Lines.Count} lines.");
                }
            }
        });
        ContentKinds.Register<UniqueDef>("uniques", (d, c) =>
        {
            c.Ref<ItemBaseDef>(d.Base, "base");
            c.Modifiers(d.Lines, "lines");
        });
        ContentKinds.Register<ItemRarityDef>("item_rarities");
        ContentKinds.Register<CurrencyDef>("currencies");
        ContentKinds.Register<LootTableDef>("loot", (d, c) =>
        {
            foreach (var roll in d.Rolls)
            {
                c.Expression(roll.Count, "rolls.count");
                foreach (var e in roll.Entries)
                {
                    switch (e.Kind)
                    {
                        case "item":
                            if (e.Item is not null)
                            {
                                c.Ref<ItemBaseDef>(e.Item, "entries.item");
                            }
                            break;
                        case "unique":
                            if (e.Unique is not null)
                            {
                                c.Ref<UniqueDef>(e.Unique, "entries.unique");
                            }
                            break;
                        case "table":
                            c.Ref<LootTableDef>(e.Table, "entries.table");
                            break;
                        case "currency":
                            c.Ref<CurrencyDef>(e.Currency, "entries.currency");
                            break;
                        case "nothing":
                            break;
                        default:
                            c.Error($"unknown loot entry kind '{e.Kind}'.");
                            break;
                    }
                    c.Expression(e.Condition, "entries.condition");
                }
            }
        });

        ContentKinds.Register<PatternDef>("patterns", (d, c) =>
        {
            if (d.Extends is not null)
            {
                c.Ref<PatternDef>(d.Extends, "extends");
            }
            if (d.Faction is not null)
            {
                c.Ref<FactionDef>(d.Faction, "faction", required: false);
            }
        });
        Kinds.PhysicalKinds.Register();
        Kinds.HitBodyKinds.Register();
        ContentKinds.Register<BehaviorDef>("behaviors", (d, c) =>
        {
            if (!d.States.ContainsKey(d.Initial))
            {
                c.Error($"initial state '{d.Initial}' is not defined.");
            }
            foreach (var (name, state) in d.States)
            {
                foreach (var t in state.Transitions)
                {
                    if (!d.States.ContainsKey(t.To))
                    {
                        c.Error($"state '{name}' transitions to unknown state '{t.To}'.");
                    }
                    c.Expression(t.When, $"states.{name}.transitions.when");
                }
            }
        });
        ContentKinds.Register<PassiveNodeDef>("passives", (d, c) =>
        {
            c.Modifiers(d.Modifiers, "modifiers");
            foreach (var link in d.Links)
            {
                c.Ref<PassiveNodeDef>(link, "links");
            }
            if (d.Kind == PassiveKind.Start && d.Calling is not null)
            {
                c.Ref<CallingDef>(d.Calling, "calling", required: false);
            }
        });
        ContentKinds.Register<CallingDef>("callings", (d, c) =>
        {
            c.Refs<ThreadDef>(d.Threads, "threads");
            c.Modifiers(d.Modifiers, "modifiers");
            foreach (var w in d.Weaves)
            {
                ValidateWeaveSpec(w, c, "weaves");
            }
            foreach (var g in d.Items)
            {
                c.Ref<ItemBaseDef>(g.Item, "items");
            }
            c.Refs<PassiveNodeDef>(d.Passives, "passives");
            if (d.StartNode is not null)
            {
                c.Ref<PassiveNodeDef>(d.StartNode, "start_node");
            }
        });

        ContentKinds.Register<BiomeDef>("biomes", (d, c) =>
        {
            if (d.Parent is not null)
            {
                c.Ref<BiomeDef>(d.Parent, "parent");
            }
            foreach (var f in d.Flora)
            {
                c.Ref<PatternDef>(f.Pattern, "flora.pattern");
            }
            foreach (var s in d.Spawns)
            {
                c.Ref<PatternDef>(s.Pattern, "spawns.pattern");
            }
            c.Refs<SpawnTableDef>(d.SpawnTables, "spawn_tables");
            foreach (var t in d.Threads.Keys)
            {
                c.Ref<ThreadDef>(t, "threads");
            }
        });
        ContentKinds.Register<SpawnTableDef>("spawn_tables", (d, c) =>
        {
            foreach (var s in d.Spawns)
            {
                c.Ref<PatternDef>(s.Pattern, "spawns.pattern");
            }
        });
        ContentKinds.Register<RegionArchetypeDef>("region_archetypes", (d, c) =>
        {
            foreach (var t in d.Threads.Keys)
            {
                c.Ref<ThreadDef>(t, "threads");
            }
            c.Refs<FactionDef>(d.Factions, "factions");
        });
        ContentKinds.Register<PoiDef>("pois", (d, c) =>
        {
            c.Refs<LandmarkDef>(d.Landmarks, "landmarks");
            foreach (var s in d.Inhabitants)
            {
                c.Ref<PatternDef>(s.Pattern, "inhabitants.pattern");
            }
            c.Refs<QuestTemplateDef>(d.Quests, "quests");
            c.Refs<FactionDef>(d.Factions, "factions");
        });
        ContentKinds.Register<LandmarkDef>("landmarks", (d, c) =>
        {
            if (d.Rows.Count == 0)
            {
                c.Error("landmark has no rows.");
            }
            var width = d.Rows.Count == 0 ? 0 : d.Rows.Max(r => r.Length);
            foreach (var row in d.Rows)
            {
                foreach (var ch in row)
                {
                    if (ch == ' ' || ch == '.')
                    {
                        continue;
                    }
                    if (!d.Legend.ContainsKey(ch.ToString()))
                    {
                        c.Error($"row character '{ch}' has no legend entry.");
                        break;
                    }
                }
            }
            foreach (var (k, cell) in d.Legend)
            {
                if (cell.Spawn is not null)
                {
                    c.Ref<PatternDef>(cell.Spawn, $"legend.{k}.spawn");
                }
            }
            _ = width;
        });
        ContentKinds.Register<RegionOverrideDef>("region_overrides", (d, c) =>
        {
            if (d.Archetype is not null)
            {
                c.Ref<RegionArchetypeDef>(d.Archetype, "archetype");
            }
            if (d.Faction is not null)
            {
                c.Ref<FactionDef>(d.Faction, "faction");
            }
        });
        ContentKinds.Register<ZoneOverrideDef>("zone_overrides", (d, c) =>
        {
            foreach (var p in d.Pois)
            {
                c.Ref<PoiDef>(p.Poi, "pois.poi");
                if (p.Landmark is not null)
                {
                    c.Ref<LandmarkDef>(p.Landmark, "pois.landmark");
                }
            }
            foreach (var s in d.Spawns)
            {
                c.Ref<PatternDef>(s.Pattern, "spawns.pattern");
            }
            c.Refs<QuestDef>(d.Quests, "quests");
        });
        ContentKinds.Register<WorldEventDef>("world_events", (d, c) =>
        {
            c.Expression(d.Duration, "duration");
            c.Modifiers(d.Modifiers, "modifiers");
            foreach (var s in d.Spawns)
            {
                c.Ref<PatternDef>(s.Pattern, "spawns.pattern");
            }
            if (d.Faction is not null)
            {
                c.Ref<FactionDef>(d.Faction, "faction");
            }
            c.Refs<QuestTemplateDef>(d.Quests, "quests");
        });

        ContentKinds.Register<QuestDef>("quests", (d, c) =>
        {
            c.Expression(d.Prerequisites, "prerequisites");
            c.Expression(d.Rewards.Xp, "rewards.xp");
            if (d.Objectives.Count == 0)
            {
                c.Error("quest has no objectives.");
            }
            foreach (var g in d.Rewards.Items)
            {
                c.Ref<ItemBaseDef>(g.Item, "rewards.items");
            }
            if (d.Rewards.Loot is not null)
            {
                c.Ref<LootTableDef>(d.Rewards.Loot, "rewards.loot");
            }
        });
        ContentKinds.Register<QuestTemplateDef>("quest_templates", (d, c) =>
        {
            foreach (var (name, slot) in d.Slots)
            {
                if (!double.IsFinite(slot.Within) || slot.Within < 0)
                    c.Error($"slot '{name}' within must be finite and nonnegative.");
                if (slot.LevelDelta is { } level && (double.IsNaN(level.Min) || double.IsNaN(level.Max) || level.Min > level.Max))
                    c.Error($"slot '{name}' level_delta must be an ordered window.");
                if (slot.LocalPopulation && slot.PatternTags.IsEmpty)
                    c.Error($"slot '{name}' local_population requires pattern_tags.");
            }
            if (d.Objectives.Count == 0)
            {
                c.Error("template has no objectives.");
            }
            foreach (var o in d.Objectives)
            {
                foreach (var slot in ExtractSlots(o.Target).Concat(ExtractSlots(o.Poi)).Concat(ExtractSlots(o.Text)))
                {
                    if (!d.Slots.ContainsKey(slot))
                    {
                        c.Error($"objective '{o.Id}' references undefined slot '${slot}'.");
                    }
                }
            }
        });
        ContentKinds.Register<RecipeDef>("recipes", (d, c) =>
        {
            foreach (var i in d.Inputs)
            {
                if (i.Item is not null)
                {
                    c.Ref<ItemBaseDef>(i.Item, "inputs.item");
                }
                else if (i.Tags.IsEmpty)
                {
                    c.Error("recipe input needs an item or tags.");
                }
            }
            c.Ref<ItemBaseDef>(d.Output.Item, "output.item");
            c.Expression(d.Quality, "quality");
            c.Expression(d.Xp, "xp");
        });
        ContentKinds.Register<ProcedureDef>("procedures");
        ContentKinds.Register<DecreeDef>("decrees", (d, c) => c.Modifiers(d.Modifiers, "modifiers"));
        ContentKinds.Register<StructureDef>("structures", (d, c) =>
        {
            c.Modifiers(d.Modifiers, "modifiers");
            if (d.Landmark is not null)
            {
                c.Ref<LandmarkDef>(d.Landmark, "landmark");
            }
        });
        ContentKinds.Register<AttributeFamilyDef>("attribute_families", (d, c) =>
        {
            // Expansion problems are reported when the families expand; here only what expansion cannot see.
            if (d.Bounds is { Min: { } min, Max: { } max } && min > max)
            {
                c.Error("bounds.min is greater than bounds.max.");
            }
            if (AttributeFamilies.PlaceholdersOf(d.Pattern).Count == 0)
            {
                c.Warn($"pattern '{d.Pattern}' has no placeholder, so the family declares one attribute.");
            }
        });

        // Each package validates its own fields and registers its own kinds in its own file.
        Kinds.CastingKinds.Register();
        Kinds.DeliveryKinds.Register();
        Kinds.MinionKinds.Register();
        Kinds.CombatKinds.Register();
        Kinds.EvaluationKinds.Register();
        Kinds.ProgressionKinds.Register();
        Kinds.InterfaceKinds.Register();
        // Effect parameters are checked in every kind's effect lists, so these come once all kinds exist.
        WeaveValidation.RegisterEffectChecks();
    }

    public static void ValidateWeaveSpec(WeaveSpec w, ValidationContext c, string field)
    {
        if (w.Preset is not null)
        {
            c.Ref<WeaveDef>(w.Preset, field + ".preset");
            return;
        }
        var refsOk = c.Ref<FormDef>(w.Form, field + ".form");
        foreach (var t in w.Threads)
        {
            refsOk &= c.Ref<ThreadDef>(t, field + ".threads");
        }
        refsOk &= WeaveValidation.GlyphRefs(w.Glyphs, c, field + ".glyphs");
        if (refsOk)
        {
            WeaveValidation.Fits(w, c, field);
        }
    }

    public static IEnumerable<string> ExtractSlots(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }
        var i = 0;
        while (i < text.Length)
        {
            var dollar = text.IndexOf('$', i);
            if (dollar < 0)
            {
                yield break;
            }
            var j = dollar + 1;
            while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_'))
            {
                j++;
            }
            if (j > dollar + 1)
            {
                yield return text.Substring(dollar + 1, j - dollar - 1);
            }
            i = j;
        }
    }
}
