using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;

namespace AnoMech.Scenarios.Umad.P3BlackHole;

public sealed class UmadP3BlackHoleYanAi : IScenarioAi<UmadP3BlackHoleState>
{
    public string Name => "Yan";
    public string? Group => "JP";

    private static readonly int[] SupportsBeforeDps = [4, 5, 6, 3, 0, 1, 2, 7];

    private static readonly (int Seat, Sign Mark)[] MarkerSeats =
    [
        (0, Sign.Attack1),
        (4, Sign.Attack2),
        (3, Sign.Attack3),
        (1, Sign.Bind1),
        (5, Sign.Bind2),
        (7, Sign.Bind3),
        (2, Sign.Ignore1),
        (6, Sign.Ignore2),
    ];

    private static int TetherSeat(int seat) => SupportsBeforeDps[seat];

    private UmadP3BlackHoleState state = null!;
    private SimWorld world = null!;
    private readonly Dictionary<int, SimCharacter?> assignedHole = new();
    private readonly Dictionary<SimCharacter, float> setHoles = new();
    private (Vector2 Centre, float Rotation)? implosionChaos;
    private (int Diagonal, float Distance)[]? implosionSpots;

    private const float ThunderClearDistance = 10f;
    private const float ThunderClearanceInterval = 0.4f;
    private const float SideTieTolerance = 1e-3f;
    private const float ArenaRadius = 20f;
    private const float ImplosionBorderInset = 1f;
    private const float ImplosionMinChaosDistance = 4f;
    private const float ImplosionConeLean = 0.18f;
    private const float SlapRadius = 13f;
    private const float SlapClearance = 1.5f;
    private const float NewSetPollSeconds = 0.05f;
    private const float NewSetMaxWaitSeconds = 3f;
    private const int PullTetherMaxRetries = 4;
    private const float TetherRegrabInterval = 0.4f;
    private const float TetherPullRadius = 10.5f;
    private static readonly int[] SupportGroup = [0, 1, 2, 3];
    private static readonly int[] DpsGroup = [4, 5, 6, 7];
    private const float StompTowerX = 11.2f;
    private const float StompTowerPairGap = 1.5f;

    public void Run(UmadP3BlackHoleState stateParam, SimWorld worldParam)
    {
        state = stateParam;
        world = worldParam;
        assignedHole.Clear();
        setHoles.Clear();
        implosionChaos = null;
        implosionSpots = null;
        var ai = new AiManager(world);

        world.Events.Add(8.5f, ApplyLineMarkers);
        world.Events.Add(25.17f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[0]);
        world.Events.Add(55.70f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[1]);
        world.Events.Add(89.95f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[2]);
        world.Events.Add(123.83f, () => state.ScenarioObjects.TetherSortFrom = state.KefkaPosition[3]);

        ai.Move(7f, StackCentreTanksHoldBossesCentred);
        ai.Move(11f, StackCentre);
        ai.Move(18f, () => SlapSpots(slapIndex: 0, kefkaIndex: 0));
        ai.Move(25f, StackCentre);
        world.Events.Add(26.2f, () => GrabNewTether(tetherIndex: 0, playerIndex: 4));
        ScheduleTetherRegrab(26.6f, 28.2f, tetherIndex: 0, playerIndex: 4);
        world.Events.Add(28.2f, () => PullTether(playerIndex: 4, counterClockwise: true));
        world.Events.Add(32.4f, () => GrabWave1SecondShotPair(nearPlayerIndex: 4, farPlayerIndex: 0));
        world.Events.Add(34.4f, () => PullTether(playerIndex: 4, counterClockwise: false));
        world.Events.Add(34.4f, () => PullTether(playerIndex: 0, counterClockwise: false));
        world.Events.Add(39.4f, ResolveFirstThunder);
        ai.Move(43.19f, SwapFirstThunderTanks);
        {
            var (set1First, set1Second) = ThunderIIIPlanning.Roles(state.ThunderSet1);
            ScheduleThunderClearance(39.5f, 43.19f, 43.5f, 46.2f, set1First, set1Second);
        }
        ai.Move(46.5f, DodgeEdict);
        ai.Move(50.6f, () => SlapSpots(slapIndex: 1, kefkaIndex: 1), sprint: true);
        ai.Move(56f, StackCentre);
        world.Events.Add(58.1f, () => GrabNewTether(tetherIndex: 0, playerIndex: 4));
        world.Events.Add(58.1f, () => GrabNewTether(tetherIndex: 1, playerIndex: 0));
        world.Events.Add(58.1f, () => GrabNewTether(tetherIndex: 2, playerIndex: 3));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 4, counterClockwise: false));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 0, counterClockwise: false));
        world.Events.Add(60.1f, () => PullTether(playerIndex: 3, counterClockwise: false));
        world.Events.Add(64f, () => GrabTether(tetherIndex: 0, playerIndex: 5, intercept: 1f));
        world.Events.Add(66f, () => ReturnToMiddle(playerIndex: 4));
        world.Events.Add(69f, () => GrabTether(tetherIndex: 1, playerIndex: 1, intercept: 1f));
        world.Events.Add(71f, () => ReturnToMiddle(playerIndex: 0));
        ai.Move(73.1f, DodgeEdictAndLookUpon);
        ai.Move(80f, StackCentre);
        world.Events.Add(82f, ResolveSecondThunder);
        ai.Move(84.5f, SwapSecondThunderTanks);
        {
            var (set2First, set2Second) = ThunderIIIPlanning.Roles(state.ThunderSet2);
            ScheduleThunderClearance(80.5f, 84.5f, 84.9f, 87.5f, set2First, set2Second);
        }
        ai.Move(88f, StackCentre);
        world.Events.Add(92.3f, () => GrabNewTether(tetherIndex: 0, playerIndex: 5));
        world.Events.Add(92.3f, () => GrabNewTether(tetherIndex: 1, playerIndex: 1));
        world.Events.Add(92.3f, () => GrabNewTether(tetherIndex: 2, playerIndex: 7));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 5, counterClockwise: false));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 1, counterClockwise: false));
        world.Events.Add(94.3f, () => PullTether(playerIndex: 7, counterClockwise: false));
        world.Events.Add(98f, () => GrabTether(tetherIndex: 0, playerIndex: 6, intercept: 1f));
        world.Events.Add(100f, () => ReturnToMiddle(playerIndex: 5));
        world.Events.Add(103f, () => GrabTether(tetherIndex: 1, playerIndex: 2, intercept: 1f));
        world.Events.Add(105f, () => ReturnToMiddle(playerIndex: 1));
        world.Events.Add(109.5f, () => ReturnToMiddle(playerIndex: 7));
        world.Events.Add(110.5f, () => ReturnToMiddle(playerIndex: 6));
        world.Events.Add(111.5f, () => ReturnToMiddle(playerIndex: 2));
        world.Events.Add(110f, () => AnchorMtForImplosion(kefkaIndex: 3));
        ai.Move(117f, () => DodgeImplosion(shockwaveIndex: 0, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: 119.09f);
        ai.Move(119.2f, () => DodgeImplosion(shockwaveIndex: 1, slapIndex: 2, slapKefkaIndex: 3), jitter: 0f, arrivalTime: 121.11f);
        ai.Move(121.3f, () => SlapSpots(slapIndex: 2, kefkaIndex: 3), sprint: true);
        ai.Move(124f, StackCentre);
        world.Events.Add(125.9f, () => GrabNewTether(tetherIndex: 0, playerIndex: 6));
        world.Events.Add(125.9f, () => GrabNewTether(tetherIndex: 1, playerIndex: 2));
        world.Events.Add(127.9f, () => PullTether(playerIndex: 6, counterClockwise: false));
        world.Events.Add(127.9f, () => PullTether(playerIndex: 2, counterClockwise: false));
        world.Events.Add(130.8f, () => GrabTether(tetherIndex: 0, playerIndex: 2));
        world.Events.Add(130.8f, () => ReturnToMiddle(playerIndex: 6));
        ScheduleTetherRegrab(131.2f, 134f, tetherIndex: 0, playerIndex: 2);
        ai.Move(134f, () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: false), sprint: true);
        ai.Move(134f, () => DodgeLookUponSplitOthers(tetherPlayerIndex: 2, lookKefkaIndex: 4), sprint: true);
        ai.Move(134.5f, () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: false), sprint: true);
        ai.Move(135f, () => DodgeLookUponSplitHolder(tetherPlayerIndex: 2, lookKefkaIndex: 4, leaveWithoutTether: true), sprint: true);
        ai.Move(139f, PrepositionForStomp);
        ai.Move(147f, StompBlizzardCorners);
        ai.Move(149.8f, StompStackAndTowers);
        ai.Move(152.4f, StompSwapWest);
        ai.Move(153.7f, StompSwapEast);
    }

    private void ApplyLineMarkers()
    {
        foreach (var (seat, mark) in MarkerSeats)
            if (state.Roles.Get(seat) is { } member)
                world.Party.Mark(mark, member);
    }

    private void GrabWave1SecondShotPair(int nearPlayerIndex, int farPlayerIndex)
        => GrabWave1SecondShotPair(nearPlayerIndex, farPlayerIndex, world.Events.Elapsed, 0f);

    private void GrabWave1SecondShotPair(int nearPlayerIndex, int farPlayerIndex, float dueAt, float waited)
    {
        var tethers = state.ScenarioObjects.Tethers;
        var shown = tethers.Count >= 2
                    && !tethers.Any(t => t.A is { } hole && setHoles.TryGetValue(hole, out var at) && at < dueAt);
        if (!shown && waited < NewSetMaxWaitSeconds)
        {
            world.Events.Add(NewSetPollSeconds, () => GrabWave1SecondShotPair(nearPlayerIndex, farPlayerIndex, dueAt, waited + NewSetPollSeconds));
            return;
        }
        if (tethers.Count < 2) return;

        var nearPlayer = state.Roles.Get(TetherSeat(nearPlayerIndex));
        var nearIdx = 0;
        if (nearPlayer is not null && tethers[0].A is { } a0 && tethers[1].A is { } a1)
        {
            var d0 = Vector3.DistanceSquared(nearPlayer.Position, a0.Position);
            var d1 = Vector3.DistanceSquared(nearPlayer.Position, a1.Position);
            nearIdx = d1 < d0 ? 1 : 0;
        }
        var farIdx = 1 - nearIdx;

        if (GrabTether(nearIdx, nearPlayerIndex)?.A is { } nearHole)
        {
            setHoles.TryAdd(nearHole, dueAt);
            world.Events.Add(TetherRegrabInterval, () => HoldOnTo(nearHole, nearPlayerIndex, dueAt + NewSetMaxWaitSeconds));
        }
        if (GrabTether(farIdx, farPlayerIndex)?.A is { } farHole)
        {
            setHoles.TryAdd(farHole, dueAt);
            world.Events.Add(TetherRegrabInterval, () => HoldOnTo(farHole, farPlayerIndex, dueAt + NewSetMaxWaitSeconds));
        }
    }

    private static IAiMove StackCentreTanksHoldBossesCentred() =>
        AiMove.Create(
            new(6.0f, 0f),
            new(-3.8f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f),
            new(0f, 0f)).NaturalOrder();

    private static IAiMove StackCentre() => AiMove.All(Vector2.Zero);

    private IAiMove SlapSpots(int slapIndex, int kefkaIndex)
    {
        var direction = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Right
            ? state.KefkaPosition[kefkaIndex].Flip()
            : state.KefkaPosition[kefkaIndex];

        return state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left
            ? AiMove.All(new(9f, 0f)).ApplyPositions(direction.Apply).ApplyPositions(p => p.Multiply(1, 9f / 7f))
            : AiMove.Create(
                    new(7f, 7f), new(7f, 7f),
                    new(9f, 0f), new(9f, 0f),
                    new(7f, -7f), new(7f, -7f), new(7f, -7f), new(7f, -7f))
                .NaturalOrder()
                .ApplyPositions(direction.Apply);
    }

    private void ResolveFirstThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet1);
        if (world.Party.Get((int)first) is not { } firstMember || !firstMember.IsAlive()) return;
        (firstMember as ISimPartyMember)?.UseSprint(3.5f);
        firstMember.Follow(exdeath, speed: AiManager.SprintSpeed);
    }

    private void ResolveSecondThunder()
    {
        if (state.ScenarioObjects.Exdeath is not { } exdeath) return;
        var (first, _) = ThunderIIIPlanning.Roles(state.ThunderSet2);
        if (world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive())
            firstMember.Follow(exdeath);
    }

    private IAiMove SwapThunderTanks(ThunderIIIAssignment plan)
    {
        var (first, second) = ThunderIIIPlanning.Roles(plan);
        if (second is not { } secondRole) return AiMove.Create().NaturalOrder();
        var a = world.Party.Get(first);
        var b = world.Party.Get(secondRole);
        if (a is null || b is null) return AiMove.Create().NaturalOrder();

        var coords = new Vector2?[8];
        coords[(int)secondRole] = new Vector2(a.Position.X, a.Position.Z);
        coords[(int)first] = new Vector2(b.Position.X, b.Position.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove SwapFirstThunderTanks() => SwapThunderTanks(state.ThunderSet1);
    private IAiMove SwapSecondThunderTanks() => SwapThunderTanks(state.ThunderSet2);

    private void EnforceThunderClearanceOnce(PartyRole first, PartyRole? second)
    {
        if (second is { } secondRole
            && world.Party.Get((int)first) is { } firstMember && firstMember.IsAlive()
            && world.Party.Get((int)secondRole) is { } secondMember && secondMember.IsAlive())
        {
            var firstPos = new Vector2(firstMember.Position.X, firstMember.Position.Z);
            var secondPos = new Vector2(secondMember.Position.X, secondMember.Position.Z);
            if ((secondPos - firstPos).LengthSquared() < ThunderClearDistance * ThunderClearDistance)
            {
                var away = firstPos.LengthSquared() > 1e-4f ? Vector2.Normalize(-firstPos) : new Vector2(0f, -1f);
                var seat = firstPos + RotateVec(away, MathF.PI / 2f) * ThunderClearDistance;
                var seatClearRadius = ArenaRadius - 1f;
                if (seat.LengthSquared() > seatClearRadius * seatClearRadius)
                    seat = seat.LengthSquared() > 1e-4f ? Vector2.Normalize(seat) * seatClearRadius : seat;
                secondMember.MoveTo(new Vector3(seat.X, 0f, seat.Y));
            }
        }

        if (state.ScenarioObjects.Exdeath is { } exdeath && exdeath.IsAlive())
        {
            var bossPos = new Vector2(exdeath.Position.X, exdeath.Position.Z);
            for (int i = 0; i < 8; i++)
            {
                var role = (PartyRole)i;
                if (role == first || (second is { } sec && role == sec)) continue;
                if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
                var pos = new Vector2(member.Position.X, member.Position.Z);
                var offset = pos - bossPos;
                if (offset.LengthSquared() >= ThunderClearDistance * ThunderClearDistance) continue;
                var dir = offset.LengthSquared() > 1e-4f ? Vector2.Normalize(offset) : new Vector2(1f, 0f);
                var target = bossPos + dir * ThunderClearDistance;
                var clearRadius = ArenaRadius - 1f;
                if (target.LengthSquared() > clearRadius * clearRadius)
                    target = target.LengthSquared() > 1e-4f ? Vector2.Normalize(target) * clearRadius : target;
                member.MoveTo(new Vector3(target.X, 0f, target.Y));
            }
        }
    }

    private void ScheduleThunderClearance(float fromTime, float swapStart, float swapLand, float toTime, PartyRole first, PartyRole? second)
    {
        for (var t = fromTime; t < swapStart; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(first, second));
        var postSwapExcluded = second ?? first;
        for (var t = swapLand; t <= toTime; t += ThunderClearanceInterval)
            world.Events.Add(t, () => EnforceThunderClearanceOnce(postSwapExcluded, null));
    }

    private IAiMove DodgeEdict()
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        const float margin = 3f;
        var s = margin - Vector2.Dot(bossPos, fwd);
        var target = s > 0f ? -fwd * s : Vector2.Zero;
        return AiMove.All(target);
    }

    private IAiMove DodgeEdictAndLookUpon()
    {
        var theta = state.KefkaPosition[2].RadiansFromNorth;
        var axis = new Vector2(-MathF.Sin(theta), MathF.Cos(theta));
        var lookRight = new Vector2(MathF.Cos(theta), MathF.Sin(theta));
        const float lookSafe = 11f;
        const float behind = 3f;

        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return AiMove.All(lookRight * lookSafe);

        var bossPos = new Vector2(boss.Position.X, boss.Position.Z);
        var fwd = new Vector2(MathF.Sin(boss.Rotation), MathF.Cos(boss.Rotation));
        var denom = Vector2.Dot(axis, fwd);

        float Fwd(Vector2 p) => Vector2.Dot(p - bossPos, fwd);

        Vector2 Seat(float sign)
        {
            var edge = lookRight * (sign * lookSafe);
            var f = Fwd(edge);
            if (f <= -behind || MathF.Abs(denom) < 0.1f) return edge;
            var maxSlide = MathF.Sqrt((ArenaRadius - 1f) * (ArenaRadius - 1f) - lookSafe * lookSafe);
            var t = Math.Clamp((-behind - f) / denom, -maxSlide, maxSlide);
            return edge + axis * t;
        }

        var a = Seat(1f);
        var b = Seat(-1f);
        bool aOk = Fwd(a) < -1f, bOk = Fwd(b) < -1f;
        if (aOk == bOk) return AiMove.All(a.LengthSquared() <= b.LengthSquared() ? a : b);
        return AiMove.All(aOk ? a : b);
    }

    private IAiMove DodgeImplosion(int shockwaveIndex, int slapIndex, int slapKefkaIndex)
    {
        var boss = state.ScenarioObjects.Chaos;
        if (boss is null) return StackCentre();

        if (shockwaveIndex == 0 || implosionChaos is null)
            implosionChaos = (new Vector2(boss.Position.X, boss.Position.Z), boss.Rotation);
        var (c, rotation) = implosionChaos.Value;
        var baseAxis = rotation + (state.ImplosionAttack == ActionId.LongitudinalImplosion ? 0f : MathF.PI / 2f);

        if (shockwaveIndex == 0 || implosionSpots is null)
            implosionSpots = PickImplosionSpots(c, baseAxis, slapIndex, slapKefkaIndex);

        var coneAxisAngle = baseAxis + shockwaveIndex * (MathF.PI / 2f);
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var (diagonal, distance) = implosionSpots[i];
            coords[i] = ImplosionSpot(c, baseAxis, coneAxisAngle, diagonal, distance);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private (int Diagonal, float Distance)[] PickImplosionSpots(Vector2 c, float baseAxis, int slapIndex, int slapKefkaIndex)
    {
        var slapSpots = SlapSpots(slapIndex, slapKefkaIndex);
        var slapCentres = SlapCentres(slapIndex, slapKefkaIndex);
        var spots = new (int, float)[8];
        for (var i = 0; i < 8; i++)
        {
            var goal = slapSpots[i] ?? Vector2.Zero;
            var bestCost = float.MaxValue;
            for (var k = 0; k < 4; k++)
            {
                var d = Diagonal(baseAxis, k);
                var maxDistance = MathF.Min(RayToArenaBorder(c, RotateVec(d, ImplosionConeLean)),
                                            RayToArenaBorder(c, RotateVec(d, -ImplosionConeLean))) - ImplosionBorderInset;
                for (var t = ImplosionMinChaosDistance; t <= maxDistance; t += 0.25f)
                {
                    var first = ImplosionSpot(c, baseAxis, baseAxis, k, t);
                    var second = ImplosionSpot(c, baseAxis, baseAxis + MathF.PI / 2f, k, t);
                    var clearance = MathF.Min(SlapClearanceAt(first, slapCentres), SlapClearanceAt(second, slapCentres));
                    var cost = Vector2.Distance(second, goal) + MathF.Max(0f, -clearance) * 100f;
                    if (cost < bestCost) { bestCost = cost; spots[i] = (k, t); }
                }
            }
        }
        return spots;
    }

    private static Vector2 Diagonal(float baseAxis, int k)
    {
        var theta = baseAxis + MathF.PI / 4f + k * (MathF.PI / 2f);
        return new Vector2(MathF.Sin(theta), MathF.Cos(theta));
    }

    private static Vector2 ImplosionSpot(Vector2 c, float baseAxis, float coneAxisAngle, int diagonal, float distance)
    {
        var dir = Diagonal(baseAxis, diagonal);
        var coneAxis = new Vector2(MathF.Sin(coneAxisAngle), MathF.Cos(coneAxisAngle));
        var perp = RotateVec(coneAxis, MathF.PI / 2f);
        if (Vector2.Dot(perp, dir) < 0f) perp = -perp;
        var lean = MathF.Sign(dir.X * perp.Y - dir.Y * perp.X) * ImplosionConeLean;
        return c + RotateVec(dir, lean) * distance;
    }

    private Vector2[] SlapCentres(int slapIndex, int kefkaIndex)
    {
        var mul = state.SlapAttacks[slapIndex] == ActionId.SlapHappy_Left ? -1f : 1f;
        return Enumerable.Range(0, 3)
            .Select(row => state.KefkaPosition[kefkaIndex].Apply(new Vector3(10f * mul, 0f, -10f + row * 10f)))
            .Select(p => new Vector2(p.X, p.Z))
            .ToArray();
    }

    private static float SlapClearanceAt(Vector2 p, Vector2[] slapCentres) =>
        slapCentres.Min(centre => Vector2.Distance(p, centre)) - SlapRadius - SlapClearance;

    private static float RayToArenaBorder(Vector2 from, Vector2 dir)
    {
        var proj = Vector2.Dot(from, dir);
        return -proj + MathF.Sqrt(proj * proj + ArenaRadius * ArenaRadius - from.LengthSquared());
    }

    private static Vector2 RotateVec(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private void AnchorMtForImplosion(int kefkaIndex)
    {
        var mt = world.Party.Get(PartyRole.MainTank);
        if (mt is null) return;
        var spot = state.KefkaPosition[kefkaIndex].Rotate(1).Apply(new Vector3(0f, 0f, -5f));
        mt.MoveTo(spot);
    }

    private void ReturnToMiddle(int playerIndex) =>
        state.Roles.Get(TetherSeat(playerIndex))?.MoveTo(Vector3.Zero);

    private Vector3 LookUponHolderSpot(int holderSeat, int lookKefkaIndex)
    {
        var theta = state.KefkaPosition[lookKefkaIndex].RadiansFromNorth;
        var holder = state.Roles.Get(holderSeat);
        var blackHole = (TetherHeldBy(holder) ?? state.ScenarioObjects.Tethers.FirstOrDefault())?.A;
        var alpha = blackHole is { } bh
            ? MathF.Atan2(bh.Position.X, -bh.Position.Z)
            : theta + MathF.PI / 2f;

        var plus = alpha + MathF.PI / 4f;
        var minus = alpha - MathF.PI / 4f;
        var beta = MathF.Abs(MathF.Sin(minus - theta)) > MathF.Abs(MathF.Sin(plus - theta)) + SideTieTolerance ? minus : plus;

        const float edge = 18f;
        return new Vector3(edge * MathF.Sin(beta), 0f, -edge * MathF.Cos(beta));
    }

    private IAiMove DodgeLookUponSplitHolder(int tetherPlayerIndex, int lookKefkaIndex, bool leaveWithoutTether)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var coords = new Vector2?[8];
        if (!leaveWithoutTether && TetherHeldBy(state.Roles.Get(holderSeat)) is null)
            return AiMove.Create(coords).NaturalOrder();
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = state.Roles[holderSeat];
        coords[(int)holderRole] = new Vector2(holderSpot.X, holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private IAiMove DodgeLookUponSplitOthers(int tetherPlayerIndex, int lookKefkaIndex)
    {
        var holderSeat = TetherSeat(tetherPlayerIndex);
        var holderSpot = LookUponHolderSpot(holderSeat, lookKefkaIndex);
        var holderRole = state.Roles[holderSeat];
        var coords = new Vector2?[8];
        for (int i = 0; i < 8; i++)
            if ((PartyRole)i != holderRole)
                coords[i] = new Vector2(-holderSpot.X, -holderSpot.Z);
        return AiMove.Create(coords).NaturalOrder();
    }

    private void GrabNewTether(int tetherIndex, int playerIndex) => GrabNewTether(tetherIndex, playerIndex, world.Events.Elapsed, 0f);

    private void GrabNewTether(int tetherIndex, int playerIndex, float dueAt, float waited)
    {
        var tethers = state.ScenarioObjects.Tethers;
        var shown = tetherIndex < tethers.Count
                    && !tethers.Any(t => t.A is { } hole && setHoles.TryGetValue(hole, out var at) && at < dueAt);
        if (!shown && waited < NewSetMaxWaitSeconds)
        {
            world.Events.Add(NewSetPollSeconds, () => GrabNewTether(tetherIndex, playerIndex, dueAt, waited + NewSetPollSeconds));
            return;
        }
        if (GrabTether(tetherIndex, playerIndex)?.A is not { } hole) return;
        setHoles.TryAdd(hole, dueAt);
        world.Events.Add(TetherRegrabInterval, () => HoldOnTo(hole, playerIndex, dueAt + NewSetMaxWaitSeconds));
    }

    private void HoldOnTo(SimCharacter hole, int playerIndex, float until)
    {
        var player = state.Roles.Get(TetherSeat(playerIndex));
        var tether = state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.A, hole));
        if (player is null || tether is null || ReferenceEquals(tether.B, player) || world.Events.Elapsed >= until) return;
        if (!player.IsIntercepting) player.Intercept(tether, 3f);
        world.Events.Add(TetherRegrabInterval, () => HoldOnTo(hole, playerIndex, until));
    }

    private SimTether? GrabTether(int tetherIndex, int playerIndex, float intercept = 3f)
    {
        var seat = TetherSeat(playerIndex);
        var player = state.Roles.Get(seat);
        var tether = state.ScenarioObjects.Tethers.ElementAtOrDefault(tetherIndex);
        assignedHole[seat] = tether?.A;
        if (tether is null) return null;
        player?.Intercept(tether, intercept);
        return tether;
    }

    private void PullTether(int playerIndex, bool counterClockwise, int retriesLeft = PullTetherMaxRetries)
    {
        var seat = TetherSeat(playerIndex);
        var player = state.Roles.Get(seat);
        var hole = assignedHole.GetValueOrDefault(seat);
        var assigned = hole is null ? null : state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.A, hole));
        if (assigned is not { A: { } blackHole, B: { } held } || !ReferenceEquals(held, player))
        {
            if (retriesLeft > 0)
                world.Events.Add(1f, () => PullTether(playerIndex, counterClockwise, retriesLeft - 1));
            return;
        }
        var bhPos = new Vector2(blackHole.Position.X, blackHole.Position.Z);
        var rawSpot = PullSpotFromHole(bhPos, counterClockwise);
        var spot = world.Obstacles.ClampOutside(rawSpot, margin: 2f);
        player?.MoveTo(new Vector3(spot.X, 0f, spot.Y));
    }

    private void ScheduleTetherRegrab(float fromTime, float toTime, int tetherIndex, int playerIndex)
    {
        for (var t = fromTime; t < toTime; t += TetherRegrabInterval)
            world.Events.Add(t, () => RegrabTetherIfLost(tetherIndex, playerIndex));
    }

    private void RegrabTetherIfLost(int tetherIndex, int playerIndex)
    {
        var player = state.Roles.Get(TetherSeat(playerIndex));
        if (player is null || player.IsIntercepting || TetherHeldBy(player) is not null) return;
        GrabTether(tetherIndex, playerIndex);
    }

    private SimTether? TetherHeldBy(SimCharacter? player) =>
        player is null
            ? null
            : state.ScenarioObjects.Tethers.FirstOrDefault(t => ReferenceEquals(t.B, player));

    private static Vector2 PullSpotFromHole(Vector2 cardinal, bool counterClockwise)
    {
        var dir = Vector2.Normalize(cardinal);
        var angle = counterClockwise ? -MathF.PI / 3f : MathF.PI / 3f;
        var c = MathF.Cos(angle);
        var s = MathF.Sin(angle);
        return new Vector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c) * TetherPullRadius;
    }

    private bool SupportsStackFirst() => !state.StackTargets[0].IsDps();

    private IAiMove PrepositionForStomp() => AiMove.All(Vector2.Zero);

    private IAiMove StompBlizzardCorners() =>
        AiMove.Create(
                new(-8f, -10f), new(8f, -10f), new(-10f, -9f), new(10f, -9f),
                new(-8f, 10f), new(8f, 10f), new(-10f, 9f), new(10f, 9f))
            .NaturalOrder()
            .ApplyPositions(state.KefkaPosition[4].Apply);

    private IAiMove StompStackAndTowers()
    {
        var coords = new Vector2?[8];
        foreach (var r in SupportsStackFirst() ? SupportGroup : DpsGroup) coords[r] = Vector2.Zero;
        PlaceOnTowers(coords, SupportsStackFirst() ? DpsGroup : SupportGroup);
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    private IAiMove StompSwapWest() => StompSwapHalf(0, 2, -StompTowerX);
    private IAiMove StompSwapEast() => StompSwapHalf(1, 3, StompTowerX);

    private IAiMove StompSwapHalf(int pairA, int pairB, float towerX)
    {
        var newTower = SupportsStackFirst() ? SupportGroup : DpsGroup;
        var newStack = SupportsStackFirst() ? DpsGroup : SupportGroup;
        var coords = new Vector2?[8];
        coords[newTower[pairA]] = new Vector2(towerX, -StompTowerPairGap);
        coords[newTower[pairB]] = new Vector2(towerX, StompTowerPairGap);
        coords[newStack[pairA]] = Vector2.Zero;
        coords[newStack[pairB]] = Vector2.Zero;
        return AiMove.Create(coords).NaturalOrder().ApplyPositions(state.KefkaPosition[4].Apply);
    }

    private static void PlaceOnTowers(Vector2?[] coords, int[] group)
    {
        coords[group[0]] = new Vector2(-StompTowerX, -StompTowerPairGap);
        coords[group[2]] = new Vector2(-StompTowerX, StompTowerPairGap);
        coords[group[1]] = new Vector2(StompTowerX, -StompTowerPairGap);
        coords[group[3]] = new Vector2(StompTowerX, StompTowerPairGap);
    }
}
