using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongState;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongManaAi(bool playerBaitsSomberDance) : IScenarioAi<FruP4DarklitDragonsongState>
{
    public string Name => playerBaitsSomberDance ? "Mana · you take Somber Dance" : "Mana · Nukemaru";
    public string Group => "Mana";

    private const float S = 1f / 2.358f;

    private static readonly PartyRole[] ManaDpsOrder =
        [PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps];

    private FruP4DarklitDragonsongState state = null!;
    private SimWorld world = null!;
    private PartyRole[] preWaterSwap = [];
    private PartyRole[] postWaterSwap = [];
    private PartyRole somberTank;
    private bool baitEast;

    public void Run(FruP4DarklitDragonsongState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        preWaterSwap = BuildPreWaterSwapFormation();
        postWaterSwap = SwapFreeWaterToOtherSide(preWaterSwap);
        somberTank = ChooseSomberTank();
        var ai = new AiManager(world);
        ai.Move(2f, () => ByRole(OpeningBait), arrivalTime: 6.6f);
        ai.Move(6.65f, () => ByRole(OpeningDodge), arrivalTime: 8.8f);
        ai.Move(14.5f, () => ByRole(Lineup), arrivalTime: 24.2f);
        ai.Move(24.2f, () => ByRole(role => Bowtie(role, preWaterSwap)), arrivalTime: 28.2f);
        ai.Move(28.2f, () => ByRole(role => Bowtie(role, postWaterSwap)), arrivalTime: TowersAt);
        ai.Move(33.1f, () => ByRole(SpiritTakerSpread), arrivalTime: 35.6f);
        ai.Move(35.65f, () => ByRole(WaterStack), arrivalTime: 40f);
        world.Events.Add(40.5f, BaitSomberDanceAwayFromOracle);
        ai.Move(40.6f, () => ByRole(role => role == somberTank ? FromSim(0, baitEast ? 43 : -43) : null), arrivalTime: 43.8f);
        ai.Move(42.4f, () => ByRole(role => role == somberTank ? null : DanceParty(role)), arrivalTime: 43.8f);
        ai.Move(45f, () => ByRole(role => role == somberTank ? JustPastOracleFromCenter() : null), jitter: 0f, arrivalTime: 47f);
        ai.Move(50.15f, () => ByRole(AkhMorn), arrivalTime: 57.7f);
    }

    private PartyRole[] BuildPreWaterSwapFormation()
    {
        var cycle = Enumerable.Range(0, 4).Select(i => state.ChainLink(i).Item1).ToArray();
        var healerIndex = Array.FindIndex(cycle, r => r is PartyRole.RegenHealer or PartyRole.ShieldHealer);
        var nwChained = cycle[healerIndex];
        var neChained = cycle[(healerIndex + 2) % 4];
        var southA = cycle[(healerIndex + 1) % 4];
        var southB = cycle[(healerIndex + 3) % 4];
        var (swChained, seChained) = OrderSouthChained(neChained, southA, southB);

        var freeHealer = nwChained == PartyRole.RegenHealer ? PartyRole.ShieldHealer : PartyRole.RegenHealer;
        var chainedTank = cycle.First(r => r.IsTank());
        var freeTank = chainedTank == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;
        var freeDps = ManaDpsOrder.Where(r => !cycle.Contains(r)).ToArray();

        return [nwChained, neChained, seChained, swChained, freeHealer, freeTank, freeDps[1], freeDps[0]];
    }

    private static (PartyRole Sw, PartyRole Se) OrderSouthChained(PartyRole neChained, PartyRole southA, PartyRole southB)
    {
        if (!southA.IsTank() && !southB.IsTank())
            return ManaDpsIndex(southA) < ManaDpsIndex(southB) ? (southA, southB) : (southB, southA);
        var southTank = southA.IsTank() ? southA : southB;
        var southDps = southA.IsTank() ? southB : southA;
        return ManaDpsIndex(southDps) < ManaDpsIndex(neChained) ? (southDps, southTank) : (southTank, southDps);
    }

    private static int ManaDpsIndex(PartyRole role) => Array.IndexOf(ManaDpsOrder, role);

    private PartyRole[] SwapFreeWaterToOtherSide(PartyRole[] formation)
    {
        var after = formation.ToArray();
        var tetherWaterIdx = Array.IndexOf(after, state.TetherWater);
        var freeWaterIdx = Array.IndexOf(after, state.FreeWater);
        if ((tetherWaterIdx < 2) == (freeWaterIdx < 6))
        {
            var partnerIdx = 11 - freeWaterIdx;
            (after[freeWaterIdx], after[partnerIdx]) = (after[partnerIdx], after[freeWaterIdx]);
        }
        return after;
    }

    private PartyRole ChooseSomberTank()
    {
        var player = world.Party.PlayerRole;
        if (playerBaitsSomberDance && player.IsTank()) return player;
        return world.Party.Get(PartyRole.MainTank) is { } mainTank && world.Party.IsBotDriven(mainTank) ? PartyRole.MainTank : PartyRole.OffTank;
    }

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP4)?.Position ?? Vector3.Zero;

    private Vector3 JustPastOracleFromCenter()
    {
        var oracle = OraclePosition();
        return oracle.LengthSquared() > 0.01f ? oracle + Vector3.Normalize(oracle) * 2f : oracle;
    }

    private void BaitSomberDanceAwayFromOracle()
    {
        var x = OraclePosition().X;
        baitEast = x < -4f * S || MathF.Abs(x) < 4f * S && state.EastWing;
    }

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3 Quadrant(int index, float north, float east)
        => FromSim(index % 4 < 2 ? north : -north, index % 4 is 1 or 2 ? east : -east);

    private int Index(PartyRole role) => Array.IndexOf(postWaterSwap, role);
    private bool North(PartyRole role) => Index(role) % 4 < 2;

    private static Vector3? Middle(PartyRole role) => FromSim((int)role % 2 * 0.3f, (int)role * 0.08f);

    private static Vector3? OpeningBait(PartyRole role)
        => (role == PartyRole.MainTank ? new Vector3(-7.07f, 0, -7.07f) : new Vector3(7.07f, 0, -7.07f)) + Middle(role)!.Value * 0.5f;

    private static Vector3? OpeningDodge(PartyRole role)
        => role == PartyRole.MainTank ? new Vector3(0, 0, 7f) : Middle(role)!.Value;

    private static Vector3? Lineup(PartyRole role) => role switch
    {
        PartyRole.RegenHealer => FromSim(0.2f, -11.3f),
        PartyRole.ShieldHealer => FromSim(4.5f, -5),
        PartyRole.MainTank => FromSim(4.5f, 5),
        PartyRole.OffTank => FromSim(0.2f, 11.3f),
        PartyRole.MeleeDpsA => FromSim(-21.2f, -11.3f),
        PartyRole.MeleeDpsB => FromSim(-25.5f, -5),
        PartyRole.PhysRangedDps => FromSim(-25.5f, 5),
        _ => FromSim(-21.2f, 11.3f),
    };

    private static Vector3? Bowtie(PartyRole role, PartyRole[] order)
    {
        var index = Array.IndexOf(order, role);
        return index < 4 ? Quadrant(index, 19, 5.2f) : Quadrant(index, 6.7f, 14.6f);
    }

    private Vector3? SpiritTakerSpread(PartyRole role) => Index(role) switch
    {
        0 => new Vector3(-3.5f, 0, -6.5f),
        1 => new Vector3(3.5f, 0, -6.5f),
        2 => new Vector3(3.5f, 0, 13.5f),
        3 => new Vector3(-3.5f, 0, 13.5f),
        4 => new Vector3(-9.5f, 0, -1f),
        5 => new Vector3(9.5f, 0, -1f),
        6 => new Vector3(9.5f, 0, 6.5f),
        _ => new Vector3(-9.5f, 0, 6.5f),
    };

    private Vector3? WaterStack(PartyRole role)
        => FromSim(North(role) ? 17.3f : -17.3f, state.EastWing ? -15.5f : 15.5f) + Middle(role)!.Value * 0.3f;

    private Vector3? DanceParty(PartyRole role) => FromSim(North(role) ? 15 : -15, 0) + Middle(role)!.Value * 0.3f;

    private static Vector3? AkhMorn(PartyRole role) => role == PartyRole.MainTank ? new Vector3(-9.3f, 0, 0) : Vector3.Zero;

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
