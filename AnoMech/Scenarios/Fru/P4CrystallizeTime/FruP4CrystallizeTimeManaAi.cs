using AnoMech.Core.Game.Ai;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

public sealed class FruP4CrystallizeTimeManaAi : IScenarioAi<FruP4CrystallizeTimeState>
{
    private static readonly Sign[] WyrmfangSigns = [Sign.Attack1, Sign.Attack2, Sign.Attack3, Sign.Attack4];
    private readonly FruP4CrystallizeTimeAi movement = new();

    public string Name => "Mana (Wyrmfang 1-4)";
    public string Group => "Mana";

    public void Run(FruP4CrystallizeTimeState state, SimWorld world)
    {
        movement.Run(state, world);
        world.Events.Add(FruP4CrystallizeTimeState.DebuffsAt + 0.1f, () => MarkWyrmfangPlayers(state, world));
    }

    public void Tick(FruP4CrystallizeTimeState state, SimWorld world, float delta)
        => movement.Tick(state, world, delta);

    private static void MarkWyrmfangPlayers(FruP4CrystallizeTimeState state, SimWorld world)
    {
        for (var i = 0; i < WyrmfangSigns.Length; i++)
            if (world.Party.Get(state.WyrmfangMarkOrder[i]) is { } member)
                world.Party.Mark(WyrmfangSigns[i], member);
    }
}
