using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;

namespace HollowWake.Core.Weaves;

public sealed partial class WeaveRuntime
{
    // Both execution and observation build cast-local contributions here, then use the ordinary fold.
    private static void AddChargeModifiers(List<Modifier> modifiers, ChargeUpSpec? charge, double fill)
    {
        if (charge is null) return;
        foreach (var scale in charge.Scales)
            modifiers.Add(new Modifier { Attribute = scale.Attribute, Form = scale.Form,
                Value = scale.AtZero + (scale.AtFull - scale.AtZero) * fill, Scope = scale.Scope, Source = "charge" });
    }

    private static void AddOverchargeModifiers(List<Modifier> modifiers, OverchargeSpec? over, int stage, double sinceStage)
    {
        if (over is null || stage <= 0) return;
        foreach (var modifier in over.PerStage) modifiers.Add(modifier.Scaled(stage).WithSource("overcharge"));
        if (over.Spark is { } spark && sinceStage <= spark.Window + 1e-9)
            modifiers.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = spark.Bonus, Source = "spark" });
    }

    private static double RampValue(RampSpec ramp, double held)
    {
        var value = ramp.Per * Curves.Evaluate(ramp.Curve, held);
        if (ramp.Max is { } max) value = Math.Min(value, max);
        if (ramp.Min is { } min) value = Math.Max(value, min);
        return value;
    }

    private static List<Modifier>? UseModifiers(CompiledWeave weave, CastState state, double now, double spent, List<Modifier>? modifiers)
    {
        var casting = weave.Casting;
        if (spent > 0 && casting.Spend is { } spend)
            foreach (var scale in spend.Scales)
                Add(new Modifier { Attribute = scale.Attribute, Form = scale.Form, Value = scale.PerCharge * spent, Scope = scale.Scope, Source = "spend" });
        if (state.UsesSpent > 0 && casting.Uses is { } uses)
            foreach (var modifier in uses.Empower) Add(modifier.Scaled(state.UsesSpent));
        if (casting.SelfStack is { } stack)
        {
            var count = StacksNow(state.Slot, stack, now);
            if (count > 0)
                foreach (var modifier in stack.Modifiers) Add(modifier.Scaled(count));
        }
        if (state.Flawless && weave.Form.Timing is { } timing && timing.Bonus != 0)
            Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = timing.Bonus, Source = "timing" });
        return modifiers;

        void Add(Modifier modifier) => (modifiers ??= new List<Modifier>()).Add(modifier);
    }
}
