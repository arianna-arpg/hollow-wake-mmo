# Axes report lint inventory

Compared with main 6e7e241: 504 to 561 smells, with 197 notes unchanged. The comparison normalizes only the archived source directory prefix. There are 57 added stable finding keys: 50 form-read findings and 7 string-switch findings. No findings were suppressed. The prior steward count of 49 and 8 is superseded by this exact output.

Both scans use the updated lint implementation with the shared AxisBindings fallback metadata. Baseline source and content were archived from 6e7e241; the current scan uses this branch. These findings remain visible as observability debt, with each reason below.

## src/HollowWake.Core/Weaves/Axes/DeliveryAxes.cs:408

Finding: Compares 'effective' with '0', 'false' and 'none' in this file, a switch on strings written as ifs; content or a registry should carry these words.

The report interprets existing binding-condition false tokens (0, false, none). This vocabulary should become shared metadata when the handler declarations own it.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/DeliveryAxes.cs|return effective is not null && effective.Length > 0 && effective is not (§0§ or §1§ or §2§);
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:84

Finding: Compares 'kind' with 'buffet', 'knockback', 'pull' and 'recall' in this file, a switch on strings written as ifs; content or a registry should carry these words.

The report determines the body reached by movement effects using the existing movement kind words. Moving this body-selection metadata to the handler registry remains a follow-up.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|if (kind == §0§)
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:313

Finding: Compares 't' with '$thread' and '$threads' in this file, a switch on strings written as ifs; content or a registry should carry these words.

The report expands the compiler existing Thread placeholders for attribution. Sharing their resolver is a follow-up; it does not change effect resolution.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|return list.Select(JsonNum.StringOf).OfType<string>().SelectMany(t => t == §0§ ? weave : t == §1§ ? weave.Take(1) : new[] { t });
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:315

Finding: Compares 'word' with '$thread' and '$threads' in this file, a switch on strings written as ifs; content or a registry should carry these words.

The report expands the compiler existing Thread placeholders for attribution. Sharing their resolver is a follow-up; it does not change effect resolution.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|return word == §0§ ? weave : word == §1§ ? weave.Take(1) : new[] { word };
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:83

Finding: Compares with the content name 'knockback' and compares 'kind' with 'buffet', 'knockback', 'pull' and 'recall' in this file, a switch on strings written as ifs; content or a registry should carry these words.

The report determines the body reached by movement effects using the existing movement kind words. Moving this body-selection metadata to the handler registry remains a follow-up.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|var mover = Word(n, §0§) ?? (kind is §1§ or §2§ or §3§ ? §4§ : §5§);
```

## src/HollowWake.Core/Weaves/Axes/TriggerAxes.cs:101

Finding: A switch on the strings 'form' and 'preset' inside a system, where a registry or an axis should carry the words.

The report labels compiler provenance as form or preset; these are source categories, not gameplay behavior selection.

```text
code.string_switch|src/HollowWake.Core/Weaves/Axes/TriggerAxes.cs|var (source, at) = trigger.Source switch
```

## src/HollowWake.Core/Weaves/AxisReport.cs:394

Finding: A switch on the strings '$threads', '$thread' and '$thread_status' inside a system, where a registry or an axis should carry the words.

The report expands the compiler existing Thread placeholders for attribution. Sharing their resolver is a follow-up; it does not change effect resolution.

```text
code.string_switch|src/HollowWake.Core/Weaves/AxisReport.cs|private string Resolve(string word) => word switch
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:203

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|Raw(r, Payload, §0§, spark.Bonus, Fraction, AxisSource.Form, at + §1§, §2§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:202

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|Raw(r, Timing, §0§, spark.Window, Seconds, AxisSource.Form, at + §1§, §2§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:437

Finding: Reads the form's 'Id' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|Writer = source == AxisSource.Preset && r.Preset is { } preset ? §0§ + preset.Id : §1§ + r.Weave.Form.Id,
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:87

Finding: Reads the form's 'ChannelInterval' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|else if (r.Weave.Form.ChannelInterval is { } legacy)
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:175

Finding: Reads the form's 'Overcharge' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|if (r.Weave.Form.Overcharge is not { } over)
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:271

Finding: Reads the form's 'Toggle' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|if (r.Weave.Form.Toggle is not { } toggle)
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:74

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|presetBlock is not null && ReferenceEquals(block, presetBlock) ? (AxisSource.Preset, §0§ + name) : (AxisSource.Form, §1§ + name);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:387

Finding: Reads the form's 'ToString' and takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Economy, §0§ + m.Attribute, number, word, ScaleUnit(m.Form), source, §1§, m.Attribute, m.Form.ToString().ToLowerInvariant());
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:347

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Economy, §0§, seconds, seconds is null ? cooldown.Source : null, Seconds, AxisSource.Form, §1§,
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:59

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Economy, §0§, upkeep.Ramp, null, FractionPerSecond, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:403

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Payload, §0§ + scale.Attribute, scale.PerCharge, null, ScaleUnit(scale.Form), source, §1§, scale.Attribute,
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:198

Finding: Reads the form's 'ToString' and takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§ + m.Attribute, number, word, ScaleUnit(m.Form), AxisSource.Form, §1§, m.Attribute, m.Form.ToString().ToLowerInvariant());
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:114

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§ + ramp.Attribute, ramp.Per, null, PerHeldSecond(ramp.Form), source, §1§, ramp.Attribute, detail);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:167

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§ + scale.Attribute, scale.AtFull, null, ScaleUnit(scale.Form), source, §1§, scale.Attribute,
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:24

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§, null, form.CastMode.ToString().ToLowerInvariant(), Word, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:25

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§, null, weave.CastScope, Word, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:31

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§, null, §1§, Flag, AxisSource.Form, §2§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:275

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§, null, §1§, Flag, AxisSource.Form, §2§);#2
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:180

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Add(Timing, §0§, r.Fold(§1§, over.Stages), null, Count, AxisSource.Form, at + §2§, §3§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:192

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Economy, §0§ + resource, amount, Resource, AxisSource.Form, at + §1§ + resource);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:49

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Economy, §0§ + resource, amount, Resource, AxisSource.Form, §1§ + resource);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:55

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Economy, §0§ + resource, amount, ResourcePerSecond, AxisSource.Form, §1§ + resource);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:27

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, form.CastMove, Fraction, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:26

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, form.CastTime, Seconds, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:28

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, form.Recovery, Seconds, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:89

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, legacy, Seconds, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:189

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, over.FullHold, Seconds, AxisSource.Form, at + §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:183

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, over.Time, Seconds, AxisSource.Form, at + §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:276

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|r.Quantity(Timing, §0§, toggle.MaxDuration, Seconds, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:404

Finding: Reads the form's 'ToString' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|scale.Form.ToString().ToLowerInvariant() + §0§);
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:21

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|var form = weave.Form;
```

## src/HollowWake.Core/Weaves/Axes/CastingAxes.cs:328

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/CastingAxes.cs|var form = weave.Form;#2
```

## src/HollowWake.Core/Weaves/Axes/MechanismAxes.cs:28

Finding: Reads the form's 'Mechanisms' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/MechanismAxes.cs|r.Weave.Form.Mechanisms.HasExact(mechanism) ? AxisSource.Form
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:155

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|Scan(r, require, §0§, (AxisSource.Form, §1§), (axis, body, p, who) => Place(r, seen, axis, body, p, who, null));
```

## src/HollowWake.Core/Weaves/Axes/StatusAxes.cs:153

Finding: Reads the form's 'Target' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/StatusAxes.cs|if (weave.Form.Target?.Require is { } require)
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:19

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Add(Targeting, §0§, null, form.Targeting.ToString().ToLowerInvariant(), Word, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:34

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Add(Targeting, §0§, null, require.Source, Condition, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:37

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Add(Targeting, §0§, null, target.Fallback, Word, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:31

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Add(Targeting, §0§, null, target.Kind, Word, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:40

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Add(Targeting, §0§, null, §1§, Flag, AxisSource.Form, §2§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:22

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Quantity(Targeting, §0§, range, Units, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:36

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|r.Quantity(Targeting, §0§, target.SearchRadius, Units, AxisSource.Form, §1§);
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:54

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|var form = r.Weave.Form;
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:18

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|var form = weave.Form;
```

## src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs:57

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TargetingAxes.cs|yield return (form.Gates[i], AxisSource.Form, §0§);
```

## src/HollowWake.Core/Weaves/Axes/TriggerAxes.cs:103

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/Axes/TriggerAxes.cs|§0§ => (AxisSource.Form, §1§),
```

## src/HollowWake.Core/Weaves/AxisReport.cs:268

Finding: Reads the form's 'Attributes' and 'Id' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/AxisReport.cs|: Weave.Form.Attributes.ContainsKey(attribute) ? §0§ + Weave.Form.Id
```

## src/HollowWake.Core/Weaves/AxisReport.cs:371

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/AxisReport.cs|return (AxisSource.Form, §0§ + field);
```

## src/HollowWake.Core/Weaves/AxisReport.cs:807

Finding: Takes the whole form where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/AxisReport.cs|var form = weave.Form;
```

## src/HollowWake.Core/Weaves/AxisReport.cs:597

Finding: Reads the form's 'Effects' where the runtime should read the compiled weave, into which a preset or glyph can merge any block.

The report reads or attributes a form value to describe its origin. It does not make a runtime cast decision; compiled preset overrides are read separately. The scanner also matches AxisSource.Form and modifier Form enum labels.

```text
code.form_read|src/HollowWake.Core/Weaves/AxisReport.cs|var own = weave.Form.Effects.Count + (preset?.Effects.Count ?? 0);
```
