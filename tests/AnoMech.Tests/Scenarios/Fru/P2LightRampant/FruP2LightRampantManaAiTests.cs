using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru;
using AnoMech.Scenarios.Fru.P2LightRampant;

namespace AnoMech.Tests;

public class FruP2LightRampantManaAiTests
{
    [Test]
    public void H1AndMainTankBaitsUseManaPuddlePathAndTetherTowerOrder()
    {
        var sampleTimes = Enumerable.Range(0, FruP2LightRampantState.HammerWaves)
            .Select(wave => FruP2LightRampantState.HammerAt(wave) - FruHolyPuddles.SnapshotBeforeHit)
            .Append(18.9f)
            .ToArray();
        var samples = new Dictionary<float, Dictionary<PartyRole, Vector2>>();
        var run = ScenarioRun.Execute(typeof(FruP2LightRampantScenario), 1, 123,
            new ScenarioRunOptions
            {
                PlayerRole = PartyRole.RegenHealer,
                Overrides = settings =>
                {
                    var overrides = (FruP2LightRampantStateOverrides)settings;
                    foreach (var role in Enum.GetValues<PartyRole>())
                        overrides.Duty[role] = role is PartyRole.RegenHealer or PartyRole.MainTank
                            ? LightRampantDuty.Bait
                            : LightRampantDuty.Tether;
                },
                Probe = probe =>
                {
                    foreach (var time in sampleTimes)
                    {
                        if (!probe.Crossed(time)) continue;
                        samples[time] = Enum.GetValues<PartyRole>()
                            .ToDictionary(role => role, role =>
                            {
                                var member = probe.Member(role)!;
                                return new Vector2(member.Position.X, member.Position.Z);
                            });
                    }
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());

        var puddleSpots = new[]
        {
            (PartyRole.RegenHealer, -3f, 3f),
            (PartyRole.RegenHealer, -7.634f, 7.634f),
            (PartyRole.RegenHealer, -12.723f, 2.120f),
            (PartyRole.RegenHealer, -12.723f, -5.089f),
            (PartyRole.RegenHealer, -7.634f, -10.602f),
            (PartyRole.MainTank, 3f, -3f),
            (PartyRole.MainTank, 7.634f, -7.634f),
            (PartyRole.MainTank, 12.723f, -2.120f),
            (PartyRole.MainTank, 12.723f, 5.089f),
            (PartyRole.MainTank, 7.634f, 10.602f),
        };
        for (var wave = 0; wave < FruP2LightRampantState.HammerWaves; wave++)
        {
            var sample = samples[sampleTimes[wave]];
            AssertNear(sample[PartyRole.RegenHealer], puddleSpots[wave].Item2, puddleSpots[wave].Item3);
            AssertNear(sample[PartyRole.MainTank], puddleSpots[wave + FruP2LightRampantState.HammerWaves].Item2,
                puddleSpots[wave + FruP2LightRampantState.HammerWaves].Item3);
        }

        var towers = samples[sampleTimes[^1]];
        AssertTower(towers, PartyRole.ShieldHealer, 0);
        AssertTower(towers, PartyRole.OffTank, 1);
        AssertTower(towers, PartyRole.CasterDps, 2);
        AssertTower(towers, PartyRole.PhysRangedDps, 3);
        AssertTower(towers, PartyRole.MeleeDpsB, 4);
        AssertTower(towers, PartyRole.MeleeDpsA, 5);
    }

    private static void AssertTower(IReadOnlyDictionary<PartyRole, Vector2> positions, PartyRole role, int tower)
    {
        var expected = FruP2LightRampantState.Tower(tower);
        AssertNear(positions[role], expected.X, expected.Z);
    }

    private static void AssertNear(Vector2 actual, float x, float z)
        => Assert.That(Vector2.Distance(actual, new Vector2(x, z)), Is.LessThan(0.15f));
}
