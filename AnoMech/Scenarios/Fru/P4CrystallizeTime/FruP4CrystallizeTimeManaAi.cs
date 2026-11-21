using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Fru.FruConstants;
using static AnoMech.Scenarios.Fru.P4CrystallizeTime.FruP4CrystallizeTimeRoutes;
using static AnoMech.Scenarios.Fru.P4CrystallizeTime.FruP4CrystallizeTimeState;

namespace AnoMech.Scenarios.Fru.P4CrystallizeTime;

public sealed class FruP4CrystallizeTimeManaAi : IScenarioAi<FruP4CrystallizeTimeState>
{
    public string Name => "Mana · Nukemaru (1-4 / ST solo)";
    public string Group => "Mana";

    private const float RunSpeed = 6f;
    private const float ReferenceScale = 10f / 23.74f;
    private const float SpreadClearance = 6f;
    private const float FragmentClearance = 9f;
    private const float AeroKnockbackEndsAt = 27.45f;
    private const float StopCleansingAt = 42.2f;
    private const float ArrivalMargin = 0.25f;

    private static readonly Sign[] WyrmfangSigns = [Sign.Attack1, Sign.Attack2, Sign.Attack3, Sign.Attack4];

    private static readonly CrystallizeTimeAssignment[] MarkerPuddles =
    [
        CrystallizeTimeAssignment.IceEast,
        CrystallizeTimeAssignment.AeroEast,
        CrystallizeTimeAssignment.AeroWest,
        CrystallizeTimeAssignment.IceWest,
    ];

    private static readonly PartyRole[] RedPriority =
    [
        PartyRole.RegenHealer,
        PartyRole.MainTank,
        PartyRole.OffTank,
        PartyRole.MeleeDpsA,
        PartyRole.MeleeDpsB,
        PartyRole.PhysRangedDps,
        PartyRole.CasterDps,
        PartyRole.ShieldHealer,
    ];

    private static readonly Vector3[] SpreadCandidates = CreateSpreadCandidates();

    private FruP4CrystallizeTimeState state = null!;
    private SimWorld world = null!;
    private readonly Dictionary<PartyRole, CrystallizeTimeAssignment> assignments = new();
    private readonly Dictionary<PartyRole, CrystallizeTimeAssignment> puddleTargets = new();
    private readonly Dictionary<PartyRole, Vector3> spreadSpots = new();
    private readonly HashSet<PartyRole> knockedByAero = [];
    private readonly HashSet<PartyRole> holdingClaw = [];
    private readonly HashSet<PartyRole> holdingFang = [];
    private readonly Dictionary<PartyRole, int> moveOrders = new();
    private float elapsed;

    public void Run(FruP4CrystallizeTimeState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        elapsed = 0f;
        puddleTargets.Clear();
        spreadSpots.Clear();
        knockedByAero.Clear();
        holdingClaw.Clear();
        holdingFang.Clear();
        moveOrders.Clear();
        AssignSidesByManaPriority();

        world.Events.Add(DebuffsAt + 0.1f, MarkWyrmfangPlayers);
        world.Events.Add(15.5f, () => FollowRoute(state.SlowNorthwest ? PRE_HG_1_NW : PRE_HG_1_NE, arriveBy: 22.7f));
        world.Events.Add(23.3f, () => FollowRoute(state.SlowNorthwest ? POST_HG_1_NW : POST_HG_1_NE, arriveBy: 25.9f));
        world.Events.Add(25.9f, RememberWhoAeroKnocksBack);
        world.Events.Add(26f, () => FollowRoute(PUDDLE_DODGE));
        world.Events.Add(26.7f, () => FollowRoute(state.SlowNorthwest ? POST_KB_NW : POST_KB_NE, arriveBy: 28.2f));
        world.Events.Add(28.5f, () => FollowRoute(state.SlowNorthwest ? POST_HG_2_NW : POST_HG_2_NE, arriveBy: 28.8f));
        world.Events.Add(29.3f, MoveNorthBeforeLastHourglass);
        world.Events.Add(31.5f, () => FollowRoute(state.East ? POST_EARLY_SOAK_E : POST_EARLY_SOAK_W, arriveBy: 32f));
        world.Events.Add(33.3f, MoveAfterLastHourglass);
        world.Events.Add(34.25f, MoveAfterFirstTidalWaveTwo);
        world.Events.Add(36.25f, MoveAfterFirstTidalWaveThree);
        world.Events.Add(38.55f, MoveAfterSecondTidalWaveOne);
        world.Events.Add(40.65f, MoveAfterSecondTidalWaveTwo);
        world.Events.Add(StopCleansingAt, () => MoveEveryBot(RewindSpot, arriveBy: 44.3f));
        world.Events.Add(45.9f, spreadSpots.Clear);
        world.Events.Add(61.7f, () => MoveEveryBot(AkhMornSpot, arriveBy: 68.9f));
    }

    public void Tick(FruP4CrystallizeTimeState stateParam, SimWorld worldParam, float delta)
    {
        elapsed += delta;
        foreach (var bot in Bots())
        {
            var role = ((ISimPartyMember)bot).Role;
            ReactToClawCaught(bot, role);
            ReactToFangCleansed(bot, role);
        }
        ChasePuddles();
        if (elapsed is >= 45.9f and < 48.5f) SpreadAwayFromPlayerForSpiritTaker();
    }

    private void MarkWyrmfangPlayers()
    {
        for (var i = 0; i < WyrmfangSigns.Length; i++)
            if (world.Party.Get(state.WyrmfangMarkOrder[i]) is { } member)
                world.Party.Mark(WyrmfangSigns[i], member);
    }

    private void AssignSidesByManaPriority()
    {
        assignments.Clear();
        foreach (var role in Enum.GetValues<PartyRole>())
            assignments[role] = state.Debuff(role) switch
            {
                CrystallizeDebuff.RedAero => IsWestOfPair(role) ? CrystallizeTimeAssignment.AeroWest : CrystallizeTimeAssignment.AeroEast,
                CrystallizeDebuff.RedIce => IsWestOfPair(role) ? CrystallizeTimeAssignment.IceWest : CrystallizeTimeAssignment.IceEast,
                CrystallizeDebuff.BlueEruption => CrystallizeTimeAssignment.Eruption,
                CrystallizeDebuff.BlueIce => CrystallizeTimeAssignment.Ice,
                CrystallizeDebuff.BlueDarkness => CrystallizeTimeAssignment.Darkness,
                _ => CrystallizeTimeAssignment.Water,
            };
    }

    private bool IsWestOfPair(PartyRole role) => RedPriority.First(r => state.Debuff(r) == state.Debuff(role)) == role;

    private PartyRole RoleOf(CrystallizeTimeAssignment assignment) => assignments.First(a => a.Value == assignment).Key;

    private float EastSign => state.East ? 1f : -1f;
    private float NorthSign => state.North ? -1f : 1f;

    private PartyRole BlueByDistanceFromTidalStart(int indexFromNear)
        => state.WyrmfangMarkOrder[state.East ? indexFromNear : 3 - indexFromNear];

    private CrystallizeTimeAssignment PuddleForBlue(PartyRole role)
    {
        for (var i = 0; i < state.WyrmfangMarkOrder.Count; i++)
            if (state.WyrmfangMarkOrder[i] == role) return MarkerPuddles[i];
        return CrystallizeTimeAssignment.IceEast;
    }

    private void MoveNorthBeforeLastHourglass()
    {
        var e = EastSign;
        var farIce = BlueByDistanceFromTidalStart(3);
        MoveRoleIfBot(RoleOf(CrystallizeTimeAssignment.IceWest), new Vector3(-2f * e, 0, -16.5f), arriveBy: 33f);
        MoveRoleIfBot(RoleOf(CrystallizeTimeAssignment.IceEast), new Vector3(-2f * e, 0, -16.5f), arriveBy: 33f);
        foreach (var blue in state.WyrmfangMarkOrder)
            MoveRoleIfBot(blue, blue == farIce ? new Vector3(-6.5f * e, 0, -17.5f) : new Vector3(-2f * e, 0, -16.5f), arriveBy: 33f);
    }

    private void MoveAfterLastHourglass()
    {
        var e = EastSign;
        foreach (var red in new[] { CrystallizeTimeAssignment.AeroWest, CrystallizeTimeAssignment.AeroEast, CrystallizeTimeAssignment.IceWest, CrystallizeTimeAssignment.IceEast })
            MoveRoleIfBot(RoleOf(red), new Vector3(-2f * e, 0, 0f));

        MoveRoleIfBot(BlueByDistanceFromTidalStart(0), new Vector3(-2f * e, 0, 0f));
        MoveRoleIfBot(BlueByDistanceFromTidalStart(1), new Vector3(-2f * e, 0, 0f));
        MoveRoleIfBot(BlueByDistanceFromTidalStart(2), new Vector3(-2f * e, 0, 0f));
        StartChasingAssignedPuddle(BlueByDistanceFromTidalStart(3));
    }

    private void MoveAfterFirstTidalWaveTwo()
    {
        var e = EastSign;
        var n = NorthSign;
        foreach (var red in new[] { CrystallizeTimeAssignment.AeroWest, CrystallizeTimeAssignment.AeroEast, CrystallizeTimeAssignment.IceWest, CrystallizeTimeAssignment.IceEast })
            MoveRoleIfBot(RoleOf(red), new Vector3(11f * e, 0, 5f * n));

        StartChasingAssignedPuddle(BlueByDistanceFromTidalStart(0));
        StartChasingAssignedPuddle(BlueByDistanceFromTidalStart(1));
        MoveRoleIfBot(BlueByDistanceFromTidalStart(2), new Vector3(2f * e, 0, 6f));
    }

    private void MoveAfterFirstTidalWaveThree()
    {
        var e = EastSign;
        var n = NorthSign;
        var farIce = BlueByDistanceFromTidalStart(3);
        if (!puddleTargets.ContainsKey(farIce))
            MoveRoleIfBot(farIce, new Vector3(5f * e, 0, 11.5f * n));

        StartChasingAssignedPuddle(BlueByDistanceFromTidalStart(2));
    }

    private void MoveAfterSecondTidalWaveOne()
    {
        var e = EastSign;
        var n = NorthSign;
        var nearAero = BlueByDistanceFromTidalStart(1);
        var farAero = BlueByDistanceFromTidalStart(2);
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (puddleTargets.ContainsKey(role)) continue;
            if (state.North && (role == nearAero || role == farAero)) continue;
            MoveRoleIfBot(role, new Vector3((role == nearAero || role == farAero ? 5f : 11f) * e, 0, 11.5f * n));
        }
    }

    private void MoveAfterSecondTidalWaveTwo()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
        {
            if (puddleTargets.ContainsKey(role)) continue;
            MoveRoleIfBot(role, RewindSpot(role));
        }
    }

    private void StartChasingAssignedPuddle(PartyRole role)
    {
        if (world.Party.Get(role) is not { } bot || !bot.IsAlive() || !world.Party.IsBotDriven(bot)) return;
        if (!bot.HasStatus(StatusId.Wyrmfang)) return;
        CancelPendingMove(role);
        puddleTargets[role] = PuddleForBlue(role);
    }

    private void MoveRoleIfBot(PartyRole role, Vector3 destination, float? arriveBy = null)
    {
        if (world.Party.Get(role) is not { } bot || !bot.IsAlive() || !world.Party.IsBotDriven(bot)) return;
        puddleTargets.Remove(role);
        MoveLeavingAsLateAsPossible(bot, role, destination, arriveBy);
    }

    private IEnumerable<SimCharacter> Bots() => world.Party.ActiveMembers().Where(world.Party.IsBotDriven).ToList();

    private void FollowRoute(IReadOnlyDictionary<CrystallizeTimeAssignment, CrystallizeTimeDestination> route, float? arriveBy = null)
    {
        foreach (var (assignment, destination) in route)
        {
            var role = RoleOf(assignment);
            puddleTargets.Remove(role);
            if (world.Party.Get(role) is not { } bot || !bot.IsAlive() || !world.Party.IsBotDriven(bot)) continue;
            if (destination.Puddle is { } source)
            {
                CancelPendingMove(role);
                puddleTargets[role] = source;
            }
            else
            {
                var leaveNoSoonerThan = knockedByAero.Contains(role) ? AeroKnockbackEndsAt : 0f;
                MoveLeavingAsLateAsPossible(bot, role, destination.Position, arriveBy, leaveNoSoonerThan);
            }
        }
    }

    private void MoveEveryBot(Func<PartyRole, Vector3> spot, float arriveBy)
    {
        puddleTargets.Clear();
        foreach (var bot in Bots())
        {
            var role = ((ISimPartyMember)bot).Role;
            MoveLeavingAsLateAsPossible(bot, role, spot(role), arriveBy);
        }
    }

    private void MoveLeavingAsLateAsPossible(SimCharacter bot, PartyRole role, Vector3 destination, float? arriveBy, float leaveNoSoonerThan = 0f)
    {
        var walk = Vector2.Distance(new Vector2(bot.Position.X, bot.Position.Z), new Vector2(destination.X, destination.Z)) / RunSpeed;
        var leaveAt = MathF.Max(leaveNoSoonerThan, arriveBy is { } deadline ? deadline - ArrivalMargin - walk : 0f);
        var order = CancelPendingMove(role);
        if (leaveAt <= elapsed)
        {
            bot.MoveTo(destination, RunSpeed);
            return;
        }
        world.Events.Add(leaveAt - elapsed, () =>
        {
            if (moveOrders[role] == order && bot.IsAlive()) bot.MoveTo(destination, RunSpeed);
        });
    }

    private void MoveNow(SimCharacter bot, PartyRole role, Vector3 destination)
    {
        CancelPendingMove(role);
        bot.MoveTo(destination, RunSpeed);
    }

    private int CancelPendingMove(PartyRole role) => moveOrders[role] = moveOrders.GetValueOrDefault(role) + 1;

    private void RememberWhoAeroKnocksBack()
    {
        var aeros = state.Holders(CrystallizeDebuff.RedAero).Select(world.Party.Get).OfType<SimCharacter>().ToList();
        foreach (var member in world.Party.ActiveMembers())
            if (member is ISimPartyMember slot && aeros.Any(a => a != member && Vector3.Distance(a.Position, member.Position) <= 15f))
                knockedByAero.Add(slot.Role);
    }

    private void ReactToClawCaught(SimCharacter bot, PartyRole role)
    {
        var holds = bot.HasStatus(StatusId.Wyrmclaw);
        if (holds) { holdingClaw.Add(role); return; }
        if (!holdingClaw.Remove(role) || state.Debuff(role) != CrystallizeDebuff.RedAero) return;
        var escape = state.East ? POST_EARLY_SOAK_E : POST_EARLY_SOAK_W;
        MoveNow(bot, role, escape[assignments[role]].Position);
    }

    private void ReactToFangCleansed(SimCharacter bot, PartyRole role)
    {
        var holds = bot.HasStatus(StatusId.Wyrmfang);
        if (holds) { holdingFang.Add(role); return; }
        if (!holdingFang.Remove(role) || elapsed >= StopCleansingAt) return;
        puddleTargets.Remove(role);
        MoveNow(bot, role, SafeSpotAfterCleanse(role));
    }

    private Vector3 SafeSpotAfterCleanse(PartyRole role)
    {
        var e = EastSign;
        var n = NorthSign;
        if (role == BlueByDistanceFromTidalStart(3))
            return new Vector3(5f * e, 0, 11.5f * n);
        if (role == BlueByDistanceFromTidalStart(0))
            return elapsed < 38.5f ? new Vector3(11f * e, 0, 5f * n) : new Vector3(11f * e, 0, 11.5f * n);
        if (state.North)
            return elapsed < 40.6f ? new Vector3(5f * e, 0, 1.5f) : RewindSpot(role);
        return elapsed < 38.5f
            ? new Vector3(5f * e, 0, 7.5f)
            : elapsed < 40.6f ? new Vector3(5f * e, 0, 12.5f) : RewindSpot(role);
    }

    private void ChasePuddles()
    {
        foreach (var (role, source) in puddleTargets)
            if (world.Party.Get(role) is { } bot && bot.IsAlive()
                && state.DragonPuddles.TryGetValue(RoleOf(source), out var puddle))
                bot.MoveTo(puddle, RunSpeed);
    }

    private Vector3 RewindSpot(PartyRole role)
    {
        var groupOne = role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;
        var tank = role.IsTank();
        var leadEast = state.Corner is CrystallizeCorner.NW or CrystallizeCorner.SE ? groupOne : !groupOne;
        var front = tank ? 22.7f : 20.5f;
        var back = tank ? 18.2f : 15.4f;
        var north = (state.North ? 1 : -1) * (leadEast ? back : front);
        var east = (state.East ? 1 : -1) * (leadEast ? front : back);
        return new Vector3(east * ReferenceScale, 0, -north * ReferenceScale);
    }

    private static Vector3 AkhMornSpot(PartyRole role) => role == PartyRole.OffTank ? new Vector3(9.3f, 0, 0) : Vector3.Zero;

    private static Vector3[] CreateSpreadCandidates()
    {
        var spots = new List<Vector3>();
        for (var x = -18; x <= 18; x += 6)
            for (var z = -18; z <= 18; z += 6)
            {
                var spot = new Vector3(x, 0, z);
                if (spot.Length() <= 18.5f && Vector3.Distance(spot, FragmentPosition) >= FragmentClearance) spots.Add(spot);
            }
        return spots.ToArray();
    }

    private void SpreadAwayFromPlayerForSpiritTaker()
    {
        var player = world.Party.Player;
        foreach (var bot in Bots().OrderBy(b => ((ISimPartyMember)b).Role))
        {
            var role = ((ISimPartyMember)bot).Role;
            bool Clear(Vector3 spot) =>
                (!player.IsAlive() || world.Party.IsBotDriven(player!) || Vector3.Distance(spot, player!.Position) >= SpreadClearance)
                && spreadSpots.All(s => s.Key == role || Vector3.Distance(spot, s.Value) >= SpreadClearance);
            if (spreadSpots.TryGetValue(role, out var current) && Clear(current)) continue;
            var spot = SpreadCandidates.Where(Clear).OrderBy(c => Vector3.DistanceSquared(bot.Position, c)).Cast<Vector3?>().FirstOrDefault();
            if (spot is not { } chosen) continue;
            spreadSpots[role] = chosen;
            bot.MoveTo(chosen, RunSpeed);
        }
    }
}
