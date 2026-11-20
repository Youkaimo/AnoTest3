using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P2LightRampant.FruP2LightRampantState;

namespace AnoMech.Scenarios.Fru.P2LightRampant;

public sealed class FruP2LightRampantManaAi : IScenarioAi<FruP2LightRampantState>
{
    public string Name => "Mana";
    public string Group => "Mana";

    private const float S = 1f / 2.358f;

    private FruP2LightRampantState state = null!;
    private SimWorld world = null!;

    public void Run(FruP2LightRampantState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        var ai = new AiManager(world);
        ai.Move(3f, () => ByRole(StartingRing), arrivalTime: 8.75f);
        ai.Move(9.75f, () => ByRole(role => state.BaitsHammer(role) ? HammerSpot(role, 0) : TowerSpot(role)),
            jitter: 0f, arrivalTime: HammerSnapshotAt(0));
        for (var wave = 1; wave < HammerWaves; wave++)
        {
            var w = wave;
            ai.Move(HammerSnapshotAt(w - 1) + 0.05f,
                () => ByRole(role => state.BaitsHammer(role) ? HammerSpot(role, w) : null),
                jitter: 0f, arrivalTime: HammerSnapshotAt(w));
        }
        ai.Move(HammerAt(HammerWaves - 1) + 0.05f,
            () => ByRole(role => state.BaitsHammer(role) ? GroupSpot(role) : null),
            arrivalTime: PowerfulLightAt);
        ai.Move(19.1f, () => ByRole(role => state.BaitsHammer(role) ? null : TowerExitSpot(role)),
            arrivalTime: 20.4f);
        ai.Move(20.45f, () => ByRole(role => state.BaitsHammer(role) ? null : GroupSpot(role)),
            arrivalTime: 22.3f);
        ai.Move(PowerfulLightAt + 0.05f, () => ByRole(role => OrbSafeSpot(role, first: true)),
            jitter: 0f, arrivalTime: 27.1f);
        ai.Move(27.2f, () => ByRole(role => OrbSafeSpot(role, first: false)),
            jitter: 0f, arrivalTime: 30.1f);
        ai.Move(30.15f, () => ByRole(role => Stacks(role) == 2 ? Vector3.Zero : MiddleWait(role)),
            arrivalTime: 33.8f);
        ai.Move(33.85f, () => ByRole(BanishSpot), arrivalTime: 37.1f);
        ai.Move(37.15f, () => ByRole(ClockSpot), arrivalTime: 45.9f);
    }

    private static float HammerSnapshotAt(int wave) => HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit;

    private int Stacks(PartyRole role) => world.Party.Get(role)?.FindStatus(StatusId.Lightsteeped)?.Stacks ?? 0;

    private static Vector3 FromSim(float north, float east) => new(east * S, 0, -north * S);

    private static Vector3? StartingRing(PartyRole role) => role switch
    {
        PartyRole.RegenHealer => FromSim(7, -17),
        PartyRole.ShieldHealer => FromSim(17, -7),
        PartyRole.MainTank => FromSim(17, 7),
        PartyRole.OffTank => FromSim(7, 17),
        PartyRole.CasterDps => FromSim(-7, 17),
        PartyRole.PhysRangedDps => FromSim(-17, 7),
        PartyRole.MeleeDpsB => FromSim(-17, -7),
        _ => FromSim(-7, -17),
    };

    private Vector3? HammerSpot(PartyRole role, int wave)
    {
        var northBait = state.Index(role) == 6;
        return (northBait, wave) switch
        {
            (true, 0) => FromSim(-7, -7),
            (true, 1) => FromSim(-18, -18),
            (true, 2) => FromSim(-5, -30),
            (true, 3) => FromSim(12, -30),
            (true, _) => FromSim(25, -18),
            (false, 0) => FromSim(7, 7),
            (false, 1) => FromSim(18, 18),
            (false, 2) => FromSim(5, 30),
            (false, 3) => FromSim(-12, 30),
            _ => FromSim(-25, 18),
        };
    }

    private Vector3? TowerSpot(PartyRole role) => Tower(state.Index(role));

    private Vector3? GroupSpot(PartyRole role)
        => NorthGroup(role) ? FromSim(43, 10.8f) : FromSim(-43, -10.8f);

    private Vector3? TowerExitSpot(PartyRole role) => state.Index(role) switch
    {
        2 => FromSim(30, 35),
        3 => FromSim(-30, -35),
        _ => GroupSpot(role),
    };

    private bool NorthGroup(PartyRole role) => state.NorthGroup(role);

    private Vector3? OrbSafeSpot(PartyRole role, bool first)
    {
        var wideNorth = first == state.NorthOrbsFirst;
        return NorthGroup(role)
            ? FromSim(wideNorth ? 36.2f : 42, wideNorth ? 26.6f : 16.7f)
            : FromSim(wideNorth ? -42 : -36.2f, wideNorth ? -16.7f : -26.6f);
    }

    private Vector3 MiddleWait(PartyRole role) => NorthGroup(role)
        ? state.NorthOrbsFirst ? FromSim(16.1f, 0) : FromSim(7, 12)
        : state.NorthOrbsFirst ? FromSim(-7, -12) : FromSim(-16.1f, 0);

    private Vector3? BanishSpot(PartyRole role) => state.Pairs
        ? role switch
        {
            PartyRole.MainTank or PartyRole.PhysRangedDps => ManaClockSpot(PartyRole.MainTank),
            PartyRole.OffTank or PartyRole.MeleeDpsB => ManaClockSpot(PartyRole.OffTank),
            PartyRole.RegenHealer or PartyRole.MeleeDpsA => ManaClockSpot(PartyRole.RegenHealer),
            _ => ManaClockSpot(PartyRole.ShieldHealer),
        }
        : ManaClockSpot(role);

    private static Vector3? ClockSpot(PartyRole role) => ManaClockSpot(role);

    private static Vector3? ManaClockSpot(PartyRole role) => role switch
    {
        PartyRole.MainTank => FromSim(18, 0),
        PartyRole.OffTank => FromSim(-18, 0),
        PartyRole.RegenHealer => FromSim(0, -18),
        PartyRole.ShieldHealer => FromSim(0, 18),
        PartyRole.MeleeDpsA => FromSim(12, -12),
        PartyRole.MeleeDpsB => FromSim(-12, -12),
        PartyRole.PhysRangedDps => FromSim(-12, 12),
        _ => FromSim(12, 12),
    };

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
