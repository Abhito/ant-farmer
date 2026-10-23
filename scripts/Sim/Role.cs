using Godot;

namespace AntFarmer.Sim;

/// <summary>The job an ant is doing. Each role is carried out by its own behavior class.</summary>
public enum Role
{
    /// <summary>Collect food and bring it home (<see cref="GatherBehavior"/>).</summary>
    Gather,

    /// <summary>Guard a spot and steal from enemy carriers that come near it (<see cref="DefendBehavior"/>).</summary>
    Defend,

    /// <summary>Chase down enemy carriers anywhere and steal their food (<see cref="InterceptBehavior"/>).</summary>
    Intercept,
}

/// <summary>
/// A role plus its optional target. This is what a team's AI hands to an ant.
/// Targets left null are filled in by the behavior itself (e.g. Gather with no pile = nearest pile).
/// </summary>
public readonly record struct RoleOrder(Role Role, int? FoodId = null, Vector2I? Post = null, int? TargetAntId = null)
{
    /// <summary>Gather from a specific pile, or from the cheapest-to-reach pile if <paramref name="foodId"/> is null.</summary>
    public static RoleOrder Gather(int? foodId = null) => new(Role.Gather, FoodId: foodId);

    /// <summary>Guard <paramref name="post"/>, or the ant's own nest if null.</summary>
    public static RoleOrder Defend(Vector2I? post = null) => new(Role.Defend, Post: post);

    /// <summary>Chase a specific enemy ant, or whichever enemy carrier is closest if null.</summary>
    public static RoleOrder Intercept(int? targetAntId = null) => new(Role.Intercept, TargetAntId: targetAntId);

    public override string ToString() => Role switch
    {
        Role.Gather => FoodId is int f ? $"Gather(pile {f})" : "Gather(nearest)",
        Role.Defend => Post is Vector2I p ? $"Defend({p.X},{p.Y})" : "Defend(nest)",
        Role.Intercept => TargetAntId is int a ? $"Intercept(ant {a})" : "Intercept(nearest carrier)",
        _ => Role.ToString(),
    };
}
