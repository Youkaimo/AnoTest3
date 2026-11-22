using System;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P4KefkaSays;

public sealed class UmadP4KefkaSaysYanAi : IScenarioAi<UmadP4KefkaSaysState>
{
    public string Name => "Yan";
    public string? Group => "JP";

    private const float ThunderEdgeInset = 1.25f;
    private const float ThunderEdgeProbe = 2f;
    private const float GazeHolderAlongEdge = 2.2f;
    private static readonly float[] NonGazeAlongEdge = [7.5f, 8.7f, 9.9f];
    private static readonly Vector2[] NorthNonGazeAroundBoss = [new(-1.3f, -7.5f), new(1.3f, -7.5f), new(0f, -9f)];
    private const float GazePairOffset = 2f;
    private const float FacingStep = 0.6f;

    private const float AntilightLocalX = 8f;
    private const float AntilightLocalZ = 7f;
    private const float BlizzardNudge = 2f;

    private const float ConeHalfAngle = MathF.PI / 4f;
    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private const float LineBaseRotation = -MathF.PI / 4f;
    private static readonly Vector2[] LineAnchors =
    [
        new(24.75f, -3.54f),
        new(17.68f, -10.61f),
        new(10.61f, -17.68f),
        new(3.54f, -24.75f),
    ];

    private static readonly Vector2?[] Wave1NeoRelative =
    [
        new(0f, -9f), new(0f, -9f), new(0f, -9f), new(12f, -9f),
        new(0f, 9f), new(0f, 9f), new(0f, 9f), new(12f, 9f),
    ];

    private static readonly Vector2?[] Wave2Cardinals =
    [
        new(0f, -9.5f), new(0f, -9.5f), new(0f, -9.5f), new(-13f, 0f),
        new(0f, 9.5f), new(0f, 9.5f), new(0f, 9.5f), new(13f, 0f),
    ];

    public void Run(UmadP4KefkaSaysState state, SimWorld world)
    {
        var ai = new AiManager(world);

        ai.Move(2f, () => Stack(Vector2.Zero), jitter: 2f, arrivalTime: 9f);
        ai.Move(11.6f, () => Stack(SafeSpot(state.Mystery[0])), jitter: 0.8f, arrivalTime: 15.4f);
        ai.Move(26.4f, () => Stack(SafeSpot(state.Mystery[1])), jitter: 0.8f, arrivalTime: 30.3f);
        ai.Move(41.5f, () => Stack(SafeSpot(state.Mystery[2])), jitter: 0.8f, arrivalTime: 45.4f);

        ai.Move(57.6f, () => FloodOfNaught(state), jitter: 1.5f, arrivalTime: 62.2f);
        ai.Move(63f, () => ResolveWave1ElementsNeoRelative(state), arrivalTime: 70.5f);
        ai.Move(72f, () => SupportsNorthAlongThunderEdge(state.Wave1, state.Mystery[3], 0f), jitter: 0f, arrivalTime: 76f);
        ai.Move(78.5f, () => SupportsNorthAlongThunderEdge(state.Wave1, state.Mystery[3], GazeFacingStep(state.Wave1True)), jitter: 0f);

        ai.Move(81f, () => Stack(Vector2.Zero), jitter: 1f, arrivalTime: 86.5f);
        ai.Move(88f, () => StrayFlames(state.InfernoMystery), jitter: 0.5f, arrivalTime: 92f);

        ai.Move(92.5f, () => ResolveWave2ElementsUnderBlizzard(state), arrivalTime: 96.0f);
        ai.Move(98f, () => SupportsNorthAroundBoss(state.Wave2, 0f), jitter: 0f, arrivalTime: 101.5f);
        ai.Move(102.5f, () => SupportsNorthAroundBoss(state.Wave2, GazeFacingStep(state.Wave2True)), jitter: 0f);

        ai.Move(106f, () => Stack(Vector2.Zero), jitter: 0.8f, arrivalTime: 109.5f);
        ai.Move(111f, () => StraySprayAndMystery(state.TsunamiMystery, state.Mystery[4]), jitter: 0.3f, arrivalTime: 114.5f);

        ScheduleAccelerationBombDodge(state, world);
    }

    private static IAiMove FloodOfNaught(UmadP4KefkaSaysState state)
    {
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var role = state.Wave3[i];
            var rightSide = (i % 2 == 0) ^ state.Wounds[i] ^ (state.Antilights[0].Antilight == Antilight.White);
            var localX = rightSide ? -AntilightLocalX : AntilightLocalX;
            var localZ = role.IsDps() ? AntilightLocalZ : -AntilightLocalZ;
            var pos = state.NeoExdeathDirection.Apply(new Vector3(localX, 0f, localZ));
            coords[(int)role] = new Vector2(pos.X, pos.Z);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove ResolveWave1ElementsNeoRelative(UmadP4KefkaSaysState state)
        => ElementFormation(state.ElemRoles[0], state.ElemTrue[0], Wave1NeoRelative, state.NeoExdeathDirection.Apply);

    private static IAiMove ResolveWave2ElementsUnderBlizzard(UmadP4KefkaSaysState state)
    {
        var s = state.Mystery[3].BlizzardOffset == 0 ? BlizzardNudge : -BlizzardNudge;
        return ElementFormation(state.ElemRoles[1], state.ElemTrue[1], Wave2Cardinals, p =>
        {
            p.AddX(0, s); p.AddX(1, s); p.AddX(2, s);
            p.AddY(3, s);
            p.AddX(4, -s); p.AddX(5, -s); p.AddX(6, -s);
            p.AddY(7, -s);
        });
    }

    private static IAiMove ElementFormation(RoleList roleList, bool isTrue, Vector2?[] coords,
                                            Action<IAiPositions>? adjust = null)
    {
        var move = AiMove.Create(coords)
            .Assignments([
                roleList[0],
                roleList[1],
                isTrue ? roleList[3] : roleList[2],
                isTrue ? roleList[2] : roleList[3],
                roleList[4],
                roleList[5],
                isTrue ? roleList[7] : roleList[6],
                isTrue ? roleList[6] : roleList[7],
            ]);
        return adjust is null ? move : move.ApplyPositions(adjust);
    }

    private static float GazeFacingStep(bool lookAway) => lookAway ? FacingStep : -FacingStep;

    private static IAiMove SupportsNorthAlongThunderEdge(RoleList wave, MysteryCast thunder, float outwardStep)
    {
        var rot = LineBaseRotation * thunder.LightningOrientation;
        var lane = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
        var north = lane.Y > 0f ? -lane : lane;
        var across = new Vector2(north.Y, -north.X);
        var safeSide = LineClearance(across * ThunderEdgeProbe, thunder) > LineClearance(-across * ThunderEdgeProbe, thunder) ? across : -across;
        var edge = safeSide * ThunderEdgeInset;

        var coords = new Vector2?[8];
        coords[(int)wave[0]] = edge + north * (GazeHolderAlongEdge + outwardStep);
        coords[(int)wave[4]] = edge - north * (GazeHolderAlongEdge + outwardStep);
        for (var k = 0; k < NonGazeAlongEdge.Length; k++)
        {
            coords[(int)wave[1 + k]] = edge + north * (NonGazeAlongEdge[k] + outwardStep);
            coords[(int)wave[5 + k]] = edge - north * (NonGazeAlongEdge[k] + outwardStep);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove SupportsNorthAroundBoss(RoleList wave, float outwardStep)
    {
        var coords = new Vector2?[8];
        coords[(int)wave[0]] = StepFromBoss(new Vector2(0f, -GazePairOffset), outwardStep);
        coords[(int)wave[4]] = StepFromBoss(new Vector2(0f, GazePairOffset), outwardStep);
        for (var k = 0; k < NorthNonGazeAroundBoss.Length; k++)
        {
            coords[(int)wave[1 + k]] = StepFromBoss(NorthNonGazeAroundBoss[k], outwardStep);
            coords[(int)wave[5 + k]] = StepFromBoss(-NorthNonGazeAroundBoss[k], outwardStep);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 StepFromBoss(Vector2 p, float outwardStep) => p + Vector2.Normalize(p) * outwardStep;

    private static IAiMove Stack(Vector2 spot) => AiMove.All(spot);

    private static IAiMove StrayFlames(ChaosMystery inferno)
    {
        if (!inferno.SolutionIsChariot) return AiMove.All(Vector2.Zero);

        const float radius = 11f;
        var coords = new Vector2?[8];
        for (var i = 0; i < 8; i++)
        {
            var th = i * MathF.PI / 4f;
            coords[i] = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove StraySprayAndMystery(ChaosMystery water, MysteryCast mystery) =>
        AiMove.All(water.SolutionIsChariot ? FarSafeSpot(mystery) : SafeSpot(mystery));

    private static void ScheduleAccelerationBombDodge(UmadP4KefkaSaysState state, SimWorld world)
    {
        var role = world.Party.PlayerRole;
        var w1 = Array.IndexOf(state.Wave1.List, role) % 4;
        var w2 = Array.IndexOf(state.Wave2.List, role) % 4;

        (float resolveTime, bool real)? bomb =
            w2 == 0 ? (96.28f, state.Wave2True) :
            w2 == 1 ? (71.28f, state.Wave2True) :
            w1 == 0 ? (71.36f, state.Wave1True) :
            w1 == 1 ? (96.36f, state.Wave1True) :
            null;

        if (bomb is not ({ } resolveTime, false)) return;

        world.Events.Add(resolveTime - 0.6f, () =>
        {
            var member = world.Party.Get(role);
            if (member == null || !member.IsAlive()) return;
            var toDestination = (member.MoveDestination ?? member.Position) - member.Position;
            var step = toDestination.Length() > 0.05f ? Vector3.Normalize(toDestination) : Vector3.UnitX;
            member.MoveTo(member.Position + step * 0.3f, speed: 0.4f);
        });
    }

    private static Vector2 SafeSpot(MysteryCast mystery)
    {
        const float wantClearance = 1.5f;
        Vector2 best = Vector2.Zero;
        var bestClear = float.NegativeInfinity;
        for (var radius = 2f; radius <= 6f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
            if (bestClear >= wantClearance) break;
        }
        return best;
    }

    private static Vector2 FarSafeSpot(MysteryCast mystery)
    {
        Vector2 best = new(0f, 10f);
        var bestClear = float.NegativeInfinity;
        for (var radius = 8f; radius <= 12f; radius += 0.5f)
        {
            for (var deg = 0; deg < 360; deg += 2)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * radius, MathF.Cos(th) * radius);
                var clear = Clearance(q, mystery);
                if (clear > bestClear) { bestClear = clear; best = q; }
            }
        }
        return best;
    }

    private static float Clearance(Vector2 q, MysteryCast mystery)
    {
        var min = LineClearance(q, mystery);
        var r = q.Length();
        var qBearing = MathF.Atan2(q.X, q.Y);

        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.BlizzardOffset) % 2 != 0) continue;
            var coneBearing = MathF.PI / 2f * i + MathF.PI / 4f;
            var d = AngleDiff(qBearing, coneBearing);
            min = MathF.Min(min, r * MathF.Sin(d - ConeHalfAngle));
        }
        return min;
    }

    private static float LineClearance(Vector2 q, MysteryCast mystery)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + mystery.LightningOffset) % 2 != 0) continue;
            var anchor = new Vector2(LineAnchors[i].X * mystery.LightningOrientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * mystery.LightningOrientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue;
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth);
        }
        return min;
    }

    private static float AngleDiff(float a, float b)
    {
        var d = MathF.Abs(a - b) % (2f * MathF.PI);
        return d > MathF.PI ? 2f * MathF.PI - d : d;
    }
}
