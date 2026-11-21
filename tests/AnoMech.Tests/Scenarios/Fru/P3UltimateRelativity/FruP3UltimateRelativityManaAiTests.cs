using System.Globalization;
using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios.Fru.P3UltimateRelativity;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P3UltimateRelativity.FruP3UltimateRelativityState;

namespace AnoMech.Tests;

public class FruP3UltimateRelativityManaAiTests
{
    private static Dictionary<PartyRole, RelativityDebuff> PinnedDebuffs() => new()
    {
        [MeleeDpsA] = RelativityDebuff.ShortDps,
        [MeleeDpsB] = RelativityDebuff.ShortDps,
        [PhysRangedDps] = RelativityDebuff.MediumDps,
        [CasterDps] = RelativityDebuff.LongDps,
        [MainTank] = RelativityDebuff.ShortSupport,
        [OffTank] = RelativityDebuff.MediumSupport,
        [RegenHealer] = RelativityDebuff.LongSupport,
        [ShieldHealer] = RelativityDebuff.LongSupport,
    };

    [Test]
    public void MarksAllRolesAndUsesDaiOrientationClockSpots()
    {
        SimTimeline? timeline = null;
        var entityIds = new Dictionary<PartyRole, uint>();
        var sampleTimes = new[] { FireAt(0), FireAt(1), FireAt(2) };
        var samples = new Dictionary<float, Dictionary<PartyRole, Vector3>>();

        var run = ScenarioRun.Execute(typeof(FruP3UltimateRelativityScenario), 1, 42, new ScenarioRunOptions
        {
            PlayerRole = ShieldHealer,
            Overrides = settings =>
            {
                var o = (FruP3UltimateRelativityStateOverrides)settings;
                o.Debuffs = PinnedDebuffs();
                o.DpsIce = true;
                o.North = 0;
                o.ClockwiseMask = 0;
                o.Darkness = [RegenHealer, MainTank, OffTank];
                o.ShellCrusherTarget = MainTank;
            },
            Probe = probe =>
            {
                timeline ??= ((FakeBattleCharas)Natives.BattleCharas).Timeline;
                foreach (var role in Enum.GetValues<PartyRole>())
                    entityIds.TryAdd(role, probe.Member(role)!.EntityId);
                foreach (var time in sampleTimes)
                {
                    if (!probe.Crossed(time)) continue;
                    samples[time] = Enum.GetValues<PartyRole>()
                        .ToDictionary(role => role, role => probe.Member(role)!.Position);
                }
            },
        });

        Assert.That(run.Passed, Is.True, run.ToString());

        var marksByRole = timeline!.Rows
            .Where(row => row["kind"] == "ac_target" && row["id"] == "0x1F6" && row["target"].Length > 0)
            .ToDictionary(
                row => entityIds.First(pair => pair.Value == uint.Parse(row["target"], NumberStyles.HexNumber)).Key,
                row => (Sign)int.Parse(row["p"].Split(',')[0].Replace("0x", ""), NumberStyles.HexNumber));

        Assert.Multiple(() =>
        {
            Assert.That(marksByRole, Has.Count.EqualTo(8));
            Assert.That(marksByRole[MainTank], Is.EqualTo(Sign.Attack3));
            Assert.That(marksByRole[OffTank], Is.EqualTo(Sign.Ignore1));
            Assert.That(marksByRole[PhysRangedDps], Is.EqualTo(Sign.Ignore2));
            Assert.That(marksByRole[CasterDps], Is.EqualTo(Sign.Bind3));
            Assert.That(new[] { marksByRole[MeleeDpsA], marksByRole[MeleeDpsB] }, Is.EquivalentTo([Sign.Attack1, Sign.Attack2]));
            Assert.That(new[] { marksByRole[RegenHealer], marksByRole[ShieldHealer] }, Is.EquivalentTo([Sign.Bind1, Sign.Bind2]));
        });

        var attack1 = marksByRole.First(pair => pair.Value == Sign.Attack1).Key;
        var attack2 = marksByRole.First(pair => pair.Value == Sign.Attack2).Key;
        var bind1 = marksByRole.First(pair => pair.Value == Sign.Bind1).Key;
        var bind2 = marksByRole.First(pair => pair.Value == Sign.Bind2).Key;

        var wave0 = samples[FireAt(0)];
        var wave1 = samples[FireAt(1)];
        var wave2 = samples[FireAt(2)];

        Assert.Multiple(() =>
        {
            AssertAtClock(wave0[MainTank], 0, 13.78f);
            AssertAtClock(wave0[attack1], 3, 13.78f);
            AssertAtClock(wave0[attack2], 5, 13.78f);

            AssertAtClock(wave1[OffTank], 2, 13.78f);
            AssertAtClock(wave1[PhysRangedDps], 6, 13.78f);

            AssertAtClock(wave2[bind1], 1, 13.78f);
            AssertAtClock(wave2[bind2], 7, 13.78f);
            Assert.That(wave2[CasterDps].Length(), Is.LessThan(2f));
        });
    }

    [Test]
    public void SupportIceStacksInMiddleDuringFirstFireAndLongDpsGoesOutForThirdFire()
    {
        var samples = new Dictionary<float, Dictionary<PartyRole, Vector3>>();
        var run = ScenarioRun.Execute(typeof(FruP3UltimateRelativityScenario), 1, 99, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = settings =>
            {
                var o = (FruP3UltimateRelativityStateOverrides)settings;
                o.Debuffs = PinnedDebuffs();
                o.DpsIce = false;
                o.North = 0;
                o.ClockwiseMask = 0xFF;
                o.Darkness = [RegenHealer, MainTank, OffTank];
                o.ShellCrusherTarget = MainTank;
            },
            Probe = probe =>
            {
                foreach (var time in new[] { FireAt(0), FireAt(2) })
                    if (probe.Crossed(time))
                        samples[time] = Enum.GetValues<PartyRole>()
                            .ToDictionary(role => role, role => probe.Member(role)!.Position);
            },
        });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(samples[FireAt(0)][MainTank].Length(), Is.LessThan(2f));
            AssertAtClock(samples[FireAt(2)][CasterDps], 4, 13.78f);
        });
    }

    private static void AssertAtClock(Vector3 actual, int clock, float radius)
    {
        var angle = clock * MathF.PI / 4f;
        var expected = new Vector3(MathF.Sin(angle), 0f, -MathF.Cos(angle)) * radius;
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.6f), $"Expected clock {clock} ({expected}), got {actual}");
    }
}
