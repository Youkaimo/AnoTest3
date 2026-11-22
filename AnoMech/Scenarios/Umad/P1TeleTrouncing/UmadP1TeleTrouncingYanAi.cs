using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P1TeleTrouncing;

public sealed class UmadP1TeleTrouncingYanAi : IScenarioAi<UmadP1TeleTrouncingState>
{
    public string Name => "Yan";
    public string? Group => "JP";

    private static readonly Vector2 ConfettiStackSupportSpot = new(-5.5f, 0f);
    private static readonly Vector2 ConfettiSpreadSupportSpot = new(-3.5f, 0f);
    private static readonly Vector2 ConfettiStackDpsSpot = new(5.5f, 0f);
    private static readonly Vector2 ConfettiSpreadDpsSpot = new(3.5f, 0f);

    private const float LineHalfWidth = 5f;
    private const float LineLength = 40f;
    private static readonly float LineBaseRotation = UmadP1TeleTrouncingScenario.ThunderAnchors[0].Rotation;
    private static readonly Vector2[] LineAnchors = DeriveLineAnchors();

    private static readonly Vector2 Marker1 = new(-6f, -6f);
    private static readonly Vector2 Marker2 = new(6f, -6f);
    private static readonly Vector2 Marker3 = new(6f, 6f);
    private static readonly Vector2 Marker4 = new(-6f, 6f);
    private const float SpreadWallRadius = 18f;
    private static readonly Vector2 NorthSpot = new(0f, -SpreadWallRadius);
    private static readonly Vector2 EastSpot = new(SpreadWallRadius, 0f);
    private static readonly Vector2 SouthSpot = new(0f, SpreadWallRadius);
    private static readonly Vector2 WestSpot = new(-SpreadWallRadius, 0f);

    private static readonly (PartyRole Role, Vector2 Spot)[] RangedSpread =
    [
        (PartyRole.CasterDps, NorthSpot),
        (PartyRole.ShieldHealer, EastSpot),
        (PartyRole.RegenHealer, SouthSpot),
        (PartyRole.PhysRangedDps, WestSpot),
    ];

    private static readonly (PartyRole Role, Vector2 Marker)[] MeleeSpread =
    [
        (PartyRole.MeleeDpsA, Marker1),
        (PartyRole.MeleeDpsB, Marker2),
        (PartyRole.OffTank, Marker3),
        (PartyRole.MainTank, Marker4),
    ];

    private static readonly float[] BothWays = [1f, -1f];
    private static readonly Vector2 NwCorner = Vector2.Normalize(new Vector2(-1f, -1f));
    private static readonly Vector2 SeCorner = Vector2.Normalize(new Vector2(1f, 1f));
    private const float StackCornerRadius = 8.485f;
    private const float SpreadLineMargin = 1.5f;
    private const float SpreadSeparation = 6f;
    private const float MarkerNudgeMax = 2f;

    public void Run(UmadP1TeleTrouncingState state, SimWorld world)
    {
        var ai = new AiManager(world);

        ai.Move(7.5f, () => ArrowSpots(state, first: true), jitter: 0.1f, arrivalTime: 14.3f);
        ai.Move(14.4f, () => ArrowSpots(state, first: false), jitter: 0.1f, arrivalTime: 17.3f);
        ai.Move(17.5f, () => ConfettiSpots(state), jitter: 0.2f, sprint: true);
        ai.Move(23.6f, () => TetherSpots(state), jitter: 0.05f, sprint: true);
        ai.Move(34.6f, () => MysteryFormation(state), jitter: 0.3f, arrivalTime: 39.5f);
        world.Events.Add(40.9f, () => FaceGaze(state, world));
    }

    internal static (Vector2 At7s, Vector2 At10s) ArrowSpotsForRole(UmadP1TeleTrouncingState state, PartyRole role)
    {
        var (at7, at10) = state.ExpiryDirections(role);
        if (at7 == at10) return MatchingQuadrantSpots(at7);
        return (DifferentQuadrantSpot(at7, at10, at7), DifferentQuadrantSpot(at7, at10, at10));
    }

    private static (Vector2 First, Vector2 Second) MatchingQuadrantSpots(TelePortentDirection direction) => direction switch
    {
        TelePortentDirection.Down => (new Vector2(-12f, -12f), new Vector2(-12f, -6f)),
        TelePortentDirection.Left => (new Vector2(12f, -12f), new Vector2(6f, -12f)),
        TelePortentDirection.Up => (new Vector2(12f, 12f), new Vector2(12f, 6f)),
        TelePortentDirection.Right => (new Vector2(-12f, 12f), new Vector2(-6f, 12f)),
        _ => (Vector2.Zero, Vector2.Zero),
    };

    private static Vector2 DifferentQuadrantSpot(TelePortentDirection a, TelePortentDirection b, TelePortentDirection placed)
    {
        var hasUp = a == TelePortentDirection.Up || b == TelePortentDirection.Up;
        var hasRight = a == TelePortentDirection.Right || b == TelePortentDirection.Right;
        var hasDown = a == TelePortentDirection.Down || b == TelePortentDirection.Down;
        var hasLeft = a == TelePortentDirection.Left || b == TelePortentDirection.Left;

        if (hasLeft && hasUp)
            return placed == TelePortentDirection.Up ? new Vector2(-6f, -6f) : new Vector2(-6f, -12f);
        if (hasUp && hasRight)
            return placed == TelePortentDirection.Right ? new Vector2(6f, -6f) : new Vector2(12f, -6f);
        if (hasRight && hasDown)
            return placed == TelePortentDirection.Down ? new Vector2(6f, 6f) : new Vector2(6f, 12f);
        if (hasDown && hasLeft)
            return placed == TelePortentDirection.Left ? new Vector2(-6f, 6f) : new Vector2(-12f, 6f);
        return Vector2.Zero;
    }

    private static IAiMove ArrowSpots(UmadP1TeleTrouncingState state, bool first)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var (at7, at10) = ArrowSpotsForRole(state, role);
            coords[(int)role] = first ? at7 : at10;
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove ConfettiSpots(UmadP1TeleTrouncingState state)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            var isDps = UmadP1TeleTrouncingState.IsDps(role);
            var isStack = role == (isDps ? state.ConfettiStackDps : state.ConfettiStackSupport);
            coords[(int)role] = (isDps, isStack) switch
            {
                (false, true) => ConfettiStackSupportSpot,
                (false, false) => ConfettiSpreadSupportSpot,
                (true, true) => ConfettiStackDpsSpot,
                (true, false) => ConfettiSpreadDpsSpot,
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove TetherSpots(UmadP1TeleTrouncingState state)
    {
        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
        {
            coords[(int)role] = role switch
            {
                PartyRole.MainTank => new Vector2(-4.3f, -4.3f),
                PartyRole.OffTank => new Vector2(4.3f, -4.3f),
                PartyRole.MeleeDpsA => new Vector2(-4.3f, 4.3f),
                PartyRole.MeleeDpsB => new Vector2(4.3f, 4.3f),
                PartyRole.PhysRangedDps => new Vector2(-9f, -9f),
                PartyRole.CasterDps => new Vector2(9f, -9f),
                PartyRole.RegenHealer => new Vector2(-9f, 9f),
                PartyRole.ShieldHealer => new Vector2(9f, 9f),
                _ => Vector2.Zero,
            };
        }
        return AiMove.Create(coords).NaturalOrder();
    }

    private static IAiMove MysteryFormation(UmadP1TeleTrouncingState state)
        => state.FireIsStack ? StackFormation(state) : SpreadFormation(state);

    private static IAiMove StackFormation(UmadP1TeleTrouncingState state)
    {
        var off = state.ThunderRealOffset;
        var o = state.ThunderOrientation;
        var dpsSpot = StackCorner(NwCorner, off, o);
        var supportsSpot = StackCorner(SeCorner, off, o);

        var coords = new Vector2?[8];
        foreach (var role in state.Debuffs.Keys)
            coords[(int)role] = UmadP1TeleTrouncingState.IsDps(role) ? dpsSpot : supportsSpot;
        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2 StackCorner(Vector2 dir, int off, float o)
    {
        var spot = dir * StackCornerRadius;
        if (LineClearance(spot, off, o) >= 2f) return spot;
        var perp = new Vector2(-dir.Y, dir.X);
        foreach (var sign in BothWays)
            for (var n = 1f; n <= 4f; n += 1f)
            {
                var q = spot + perp * (n * sign);
                if (LineClearance(q, off, o) >= 2f) return q;
            }
        for (var r = StackCornerRadius + 1f; r <= 17f; r += 0.5f)
            if (LineClearance(dir * r, off, o) >= 2f) return dir * r;
        return spot;
    }

    private static IAiMove SpreadFormation(UmadP1TeleTrouncingState state)
    {
        var off = state.ThunderRealOffset;
        var o = state.ThunderOrientation;
        var crossNorth = CrossAxisNorth(o);
        var coords = new Vector2?[8];
        var taken = new List<Vector2>();

        foreach (var (role, cardinal) in RangedSpread)
        {
            var spot = SlideAlongWall(cardinal, off, o);
            coords[(int)role] = spot;
            taken.Add(spot);
        }

        var displaced = new List<PartyRole>();
        foreach (var (role, marker) in MeleeSpread)
        {
            if (ClearNearMarker(marker, off, o, crossNorth, taken) is { } spot)
            {
                coords[(int)role] = spot;
                taken.Add(spot);
            }
            else
            {
                displaced.Add(role);
            }
        }
        foreach (var role in displaced)
        {
            var spot = NearestSafeSpread(off, o, taken);
            coords[(int)role] = spot;
            taken.Add(spot);
        }

        return AiMove.Create(coords).NaturalOrder();
    }

    private static Vector2? ClearNearMarker(Vector2 marker, int off, float o, Vector2 crossNorth, List<Vector2> taken)
    {
        for (var step = 0f; step <= MarkerNudgeMax; step += 1f)
            foreach (var sign in BothWays)
            {
                var q = marker + crossNorth * (step * sign);
                if (LineClearance(q, off, o) < SpreadLineMargin) continue;
                if (taken.Exists(t => Vector2.Distance(t, q) < SpreadSeparation)) continue;
                return q;
            }
        return null;
    }

    private static void FaceGaze(UmadP1TeleTrouncingState state, SimWorld world)
    {
        var source3 = state.GazeInverted
            ? UmadP1TeleTrouncingScenario.GazeSourceInverted
            : UmadP1TeleTrouncingScenario.GazeSourceNormal;
        var source = new Vector2(source3.X, source3.Z);

        for (var i = 0; i < 8; i++)
        {
            if (world.Party.Get(i) is not { } member || !member.IsAlive()) continue;
            if (member is SimPlayer && !DebugBotControl.Enabled) continue;
            var p = new Vector2(member.Position.X, member.Position.Z);
            var faceToward = state.GazeInverted ? source : 2f * p - source;
            member.Face(new Vector3(faceToward.X, 0f, faceToward.Y));
        }
    }

    private static Vector2[] DeriveLineAnchors()
    {
        var anchors = UmadP1TeleTrouncingScenario.ThunderAnchors;
        var result = new Vector2[anchors.Length];
        for (var i = 0; i < anchors.Length; i++)
            result[i] = new Vector2(anchors[i].Position.X, anchors[i].Position.Z);
        return result;
    }

    private static float LineClearance(Vector2 q, int realOffset, float orientation)
    {
        var min = float.PositiveInfinity;
        for (var i = 0; i < 4; i++)
        {
            if ((i + realOffset) % 2 != 0) continue;
            var anchor = new Vector2(LineAnchors[i].X * orientation, LineAnchors[i].Y);
            var rot = LineBaseRotation * orientation;
            var fwd = new Vector2(MathF.Sin(rot), MathF.Cos(rot));
            var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
            var rel = q - anchor;
            var along = Vector2.Dot(rel, fwd);
            if (along < 0f || along > LineLength) continue;
            min = MathF.Min(min, MathF.Abs(Vector2.Dot(rel, right)) - LineHalfWidth);
        }
        return min;
    }

    private static Vector2 CrossAxisNorth(float orientation)
    {
        var rot = LineBaseRotation * orientation;
        var right = new Vector2(MathF.Cos(rot), -MathF.Sin(rot));
        return right.Y < 0f ? right : -right;
    }

    private static Vector2 NearestSafeSpread(int realOffset, float orientation, List<Vector2> taken)
    {
        for (var r = 0f; r <= 16f; r += 1f)
            for (var deg = 0; deg < 360; deg += 15)
            {
                var th = deg * MathF.PI / 180f;
                var q = new Vector2(MathF.Sin(th) * r, MathF.Cos(th) * r);
                if (LineClearance(q, realOffset, orientation) < SpreadLineMargin) continue;
                if (taken.Exists(t => Vector2.Distance(t, q) < SpreadSeparation)) continue;
                return q;
            }
        return Vector2.Zero;
    }

    private static Vector2 SlideAlongWall(Vector2 cardinal, int realOffset, float orientation)
    {
        if (LineClearance(cardinal, realOffset, orientation) >= SpreadLineMargin) return cardinal;
        var outward = Vector2.Normalize(cardinal);
        var tangent = new Vector2(-outward.Y, outward.X);
        for (var t = 1.5f; t <= 16f; t += 1.5f)
            foreach (var sign in BothWays)
            {
                var q = cardinal + tangent * (t * sign);
                if (q.Length() <= 19.5f && LineClearance(q, realOffset, orientation) >= SpreadLineMargin)
                    return q;
            }
        for (var pull = 2f; pull <= 12f; pull += 2f)
        {
            var q = cardinal - outward * pull;
            if (LineClearance(q, realOffset, orientation) >= SpreadLineMargin) return q;
        }
        return cardinal;
    }
}
