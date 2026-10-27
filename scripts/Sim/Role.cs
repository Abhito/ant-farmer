using Godot;

namespace AntFarmer.Sim;

public enum Role
{
    Gather,     // collect food and bring it home
    Defend,     // guard a spot near the nest
    Intercept,  // hunt enemy carriers anywhere
}

/// <summary>A role plus an optional target. Null targets are picked by the behavior.</summary>
public readonly record struct RoleOrder(Role Role, int? FoodId = null, Vector2I? Post = null, int? TargetAntId = null)
{
    public static RoleOrder Gather(int? foodId = null) => new(Role.Gather, FoodId: foodId);

    public static RoleOrder Defend(Vector2I? post = null) => new(Role.Defend, Post: post);

    public static RoleOrder Intercept(int? targetAntId = null) => new(Role.Intercept, TargetAntId: targetAntId);

    public override string ToString() => Role switch
    {
        Role.Gather => FoodId is int f ? $"Gather(pile {f})" : "Gather(nearest)",
        Role.Defend => Post is Vector2I p ? $"Defend({p.X},{p.Y})" : "Defend(nest)",
        Role.Intercept => TargetAntId is int a ? $"Intercept(ant {a})" : "Intercept(nearest carrier)",
        _ => Role.ToString(),
    };
}
