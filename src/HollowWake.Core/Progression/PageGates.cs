using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Progression;

/// <summary>A page as the hero has it: sealed (with the reason) when it exists but cannot be used from here, and its pips.</summary>
public readonly record struct PageView(string Id, bool Sealed, string? Reason, int Pips, string? Container);

/// <summary>
/// The pages of the core pane for one hero, from the <c>ui_pages</c> content: a page whose gate fails does not exist,
/// pips count what is unspent on it, both expressions over the hero (<see cref="HeroScope"/>, with
/// <c>points.{currency}</c>). Container pages come from the containers the hero owns, one page each, sealed with a
/// reason when the container cannot be reached from where the hero stands.
/// </summary>
public static class PageGates
{
    /// <summary>A source of container pages: the containers of a kind the hero owns, with whether each is reachable.</summary>
    public delegate void ContainerPages(Simulation sim, Entity hero, string kind, List<PageView> into, string pageId, int pips);

    private static ContainerPages? _containers;

    /// <summary>Lets the container service list container pages (one source; registering again replaces it).</summary>
    public static void RegisterContainers(ContainerPages source) => _containers = source;

    public static List<PageView> For(Simulation sim, Entity hero)
    {
        var pages = new List<PageView>();
        var scope = new HeroScope(sim, hero);
        foreach (var page in sim.Content.Each<UiPageDef>().OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            if (page.Gate is not null && !Holds(page.Gate, scope))
            {
                continue;
            }
            var pips = page.Pips is null ? 0 : Count(page.Pips, scope);
            if (page.Container is not null)
            {
                _containers?.Invoke(sim, hero, page.Container, pages, page.Id, pips);
                continue;
            }
            pages.Add(new PageView(page.Id, false, null, pips, null));
        }
        return pages;
    }

    private static bool Holds(Expr gate, IExprScope scope)
    {
        try
        {
            return gate.IsTrue(scope);
        }
        catch (ExprEvalException)
        {
            return false;
        }
    }

    private static int Count(Expr pips, IExprScope scope)
    {
        try
        {
            return (int)Math.Max(0, Math.Round(pips.Evaluate(scope)));
        }
        catch (ExprEvalException)
        {
            return 0;
        }
    }
}
