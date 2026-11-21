using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P4CrystallizeTime;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class FruP4CrystallizeTimeManaAiTests
{
    [TestCase(CrystallizeCorner.NW, true)]
    [TestCase(CrystallizeCorner.NE, true)]
    [TestCase(CrystallizeCorner.SE, true)]
    [TestCase(CrystallizeCorner.SW, true)]
    [TestCase(CrystallizeCorner.NW, false)]
    [TestCase(CrystallizeCorner.NE, false)]
    [TestCase(CrystallizeCorner.SE, false)]
    [TestCase(CrystallizeCorner.SW, false)]
    public void ManaPriorityMarkerCleansesAndStSoloAkhMornPassAllCorners(CrystallizeCorner corner, bool slowNorthwest)
    {
        Dictionary<PartyRole, Vector3>? opening = null;
        Dictionary<PartyRole, Vector3>? akhMorn = null;

        var run = ScenarioRun.Execute(typeof(FruP4CrystallizeTimeScenario), 1, 200 + (int)corner * 2 + (slowNorthwest ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (FruP4CrystallizeTimeStateOverrides)settings;
                    o.Debuffs = new Dictionary<PartyRole, CrystallizeDebuff>
                    {
                        [RegenHealer] = CrystallizeDebuff.RedIce,
                        [ShieldHealer] = CrystallizeDebuff.RedIce,
                        [MainTank] = CrystallizeDebuff.RedAero,
                        [OffTank] = CrystallizeDebuff.RedAero,
                        [MeleeDpsA] = CrystallizeDebuff.BlueEruption,
                        [MeleeDpsB] = CrystallizeDebuff.BlueIce,
                        [PhysRangedDps] = CrystallizeDebuff.BlueDarkness,
                        [CasterDps] = CrystallizeDebuff.BlueWater,
                    };
                    o.Corner = corner;
                    o.SlowNorthwest = slowNorthwest;
                    o.SpiritTakerTarget = MeleeDpsA;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(22.6f))
                        opening = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                    if (probe.Crossed(68.8f))
                        akhMorn = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(opening![RegenHealer].X, Is.LessThan(-5f));
            Assert.That(opening[ShieldHealer].X, Is.GreaterThan(5f));
            Assert.That(opening[MainTank].X, Is.LessThan(-5f));
            Assert.That(opening[OffTank].X, Is.GreaterThan(5f));

            Assert.That(akhMorn![OffTank].X, Is.GreaterThan(7f));
            Assert.That(akhMorn[MainTank].Length(), Is.LessThan(1.5f));
        });
    }
}
