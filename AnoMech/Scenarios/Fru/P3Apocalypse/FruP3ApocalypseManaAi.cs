using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P3Apocalypse.FruP3ApocalypseState;

namespace AnoMech.Scenarios.Fru.P3Apocalypse;

public sealed class FruP3ApocalypseManaAi : IScenarioAi<FruP3ApocalypseState>
{
    public string Name => "Mana · Permaswap";
    public string Group => "Mana";

    private static readonly PartyRole[] ZOrder =
    [
        PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB,
        PartyRole.MainTank,
        PartyRole.OffTank,
        PartyRole.PhysRangedDps,
        PartyRole.RegenHealer,
        PartyRole.CasterDps,
        PartyRole.ShieldHealer,
    ];

    private FruP3ApocalypseState state = null!;
    private SimWorld world = null!;
    private readonly bool[] supportGroup = new bool[8];
    private readonly int[] groupSlot = new int[8];

    public void Run(FruP3ApocalypseState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        AssignPermanentGroups();
        var ai = new AiManager(world);
        ai.Move(2.2f, () => ByRole(InitialBox), arrivalTime: 12.3f);
        ai.Move(16.9f, () => ByRole(role => PermanentBox(role, spread: false)), arrivalTime: 20.2f);
        ai.Move(20.2f, () => ByRole(FirstStack), arrivalTime: WaterAt(1));
        ai.Move(22.85f, () => ByRole(role => PermanentBox(role, spread: true)), arrivalTime: 24.8f);
        ai.Move(25.6f, () => ByRole(Spread), arrivalTime: WaveAt(0));
        ai.Move(36.05f, () => ByRole(PostEruption), jitter: 0f);
        ai.Move(37.05f, () => ByRole(SecondStack), arrivalTime: WaveAt(3));
        ai.Move(41.7f, () => ByRole(role => role == PartyRole.OffTank ? TankBait(near: true) : null), jitter: 0f);
        ai.Move(41.9f, () => ByRole(role => role == PartyRole.OffTank ? TankBait(near: false) : null), jitter: 0f);
        ai.Move(42f, () => ByRole(role => role == PartyRole.OffTank ? null : Vector3.Zero), arrivalTime: 44f);
        ai.Move(45.15f, () => ByRole(KnockbackStack), jitter: 0f);
        ai.Move(48.4f, () => ByRole(ReturnStack), jitter: 0f);
    }

    private void AssignPermanentGroups()
    {
        for (var i = 0; i < 8; i++) supportGroup[i] = i < 4;
        var supportAdjusters = DuplicatePriorityRoles(0);
        var dpsAdjusters = DuplicatePriorityRoles(4);
        foreach (var role in supportAdjusters) supportGroup[role] = false;
        foreach (var role in dpsAdjusters) supportGroup[role] = true;
        AssignZOrderSlots(isSupportGroup: true);
        AssignZOrderSlots(isSupportGroup: false);
    }

    private int[] DuplicatePriorityRoles(int start) => Enumerable.Range(0, 4)
        .Where(duration => Enumerable.Range(start, 4).Count(i => state.WaterDurations[i] == duration) == 2)
        .Select(duration => Enumerable.Range(start, 4).First(i => state.WaterDurations[i] == duration))
        .ToArray();

    private void AssignZOrderSlots(bool isSupportGroup)
    {
        var slot = 0;
        foreach (var role in ZOrder)
            if (supportGroup[(int)role] == isSupportGroup)
                groupSlot[(int)role] = slot++;
    }

    private bool SupportGroup(PartyRole role) => supportGroup[(int)role];
    private int GroupSlot(PartyRole role) => groupSlot[(int)role];

    private int SafeSpoke(bool isSupportGroup)
    {
        var initialSpoke = (state.Rotation < 2 ? state.Rotation : state.Rotation + 4) + (isSupportGroup ? 0 : 4);
        return (initialSpoke + (state.Clockwise ? 7 : 1)) % 8;
    }

    private int SecondSpoke(bool isSupportGroup) => (SafeSpoke(isSupportGroup) + (state.Clockwise ? 7 : 1)) % 8;

    private static Vector3 Direction(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians));
    }

    private static Vector3 ClockwisePerpendicular(Vector3 forward) => new(-forward.Z, 0f, forward.X);

    private static Vector3 BoxPosition(bool isSupportGroup, int slot, bool spread)
    {
        var forward = Direction(isSupportGroup ? -22.5f : 157.5f);
        var clockwise = ClockwisePerpendicular(forward);
        var inner = slot < 2;
        var clockwiseSide = slot % 2 == 0 ? 1f : -1f;
        var forwardDistance = spread ? (inner ? 5f : 13.5f) : (inner ? 5.2f : 8.5f);
        var sideDistance = spread ? (inner ? 3.8f : 4.2f) : 1.8f;
        return forward * forwardDistance + clockwise * (clockwiseSide * sideDistance);
    }

    private static Vector3? InitialBox(PartyRole role)
        => BoxPosition((int)role < 4, (int)role % 4, spread: false);

    private Vector3? PermanentBox(PartyRole role, bool spread)
        => BoxPosition(SupportGroup(role), GroupSlot(role), spread);

    private Vector3? FirstStack(PartyRole role) => Direction(SupportGroup(role) ? -22.5f : 157.5f) * 6.5f;

    private Vector3? Spread(PartyRole role)
    {
        var isSupport = SupportGroup(role);
        var safe = SafeSpoke(isSupport);
        var second = SecondSpoke(isSupport);
        return GroupSlot(role) switch
        {
            0 => Direction((state.Clockwise ? safe : second) * 45f) * 9.7f,
            1 => Direction((state.Clockwise ? second : safe) * 45f) * 9.7f,
            2 => Direction(safe * 45f + 10.5f) * 18.85f,
            _ => Direction(safe * 45f - 10.5f) * 18.85f,
        };
    }

    private Vector3? PostEruption(PartyRole role)
        => Direction(SafeSpoke(SupportGroup(role)) * 45f) * (GroupSlot(role) < 2 ? 4.2f : 13.8f);

    private Vector3? SecondStack(PartyRole role)
        => Direction(SafeSpoke(SupportGroup(role)) * 45f) * 4.2f;

    private Vector3 TankBait(bool near)
        => Direction(SecondSpoke(SupportGroup(PartyRole.OffTank)) * 45f) * (near ? 5f : 17.5f);

    private Vector3 OraclePosition()
        => world.Children.OfType<SimEnemy>().FirstOrDefault(e => e.BNpcBaseId == BNpcBaseId.OracleP3)?.Position ?? Vector3.Zero;

    private Vector3? KnockbackStack(PartyRole role)
    {
        var boss = OraclePosition();
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var angle = (SupportGroup(role) ? 30f : -30f) * MathF.PI / 180f;
        var direction = new Vector3(inward.X * MathF.Cos(angle) - inward.Z * MathF.Sin(angle), 0f,
            inward.X * MathF.Sin(angle) + inward.Z * MathF.Cos(angle));
        return boss + direction * 2f;
    }

    private Vector3? ReturnStack(PartyRole role)
    {
        var boss = OraclePosition();
        var inward = boss.LengthSquared() > 0.01f ? Vector3.Normalize(-boss) : Vector3.UnitZ;
        var right = new Vector3(inward.Z, 0f, -inward.X);
        return boss + inward * 2f + right * (SupportGroup(role) ? -4f : 4f);
    }

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at)
        => AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
