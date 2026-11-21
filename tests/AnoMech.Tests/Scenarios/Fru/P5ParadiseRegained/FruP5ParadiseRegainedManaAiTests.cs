using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P5ParadiseRegained;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P5ParadiseRegained.FruP5ParadiseRegainedState;

namespace AnoMech.Tests;

public class FruP5ParadiseRegainedManaAiTests
{
    [TestCase(0, true, true)]
    [TestCase(0, true, false)]
    [TestCase(0, false, true)]
    [TestCase(0, false, false)]
    [TestCase(1, true, true)]
    [TestCase(1, false, false)]
    [TestCase(2, true, false)]
    [TestCase(2, false, true)]
    public void ManaTowersUseNorthMtAndMeleeFarRangedNearAcrossAllPatterns(int rotation, bool eastSecond, bool darkFirst)
    {
        Dictionary<PartyRole, Vector3>? wing1 = null;
        Dictionary<PartyRole, Vector3>? wing2 = null;

        var run = ScenarioRun.Execute(typeof(FruP5ParadiseRegainedScenario), 1, 300 + rotation * 4 + (eastSecond ? 2 : 0) + (darkFirst ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = ShieldHealer,
                Overrides = settings =>
                {
                    var o = (FruP5ParadiseRegainedStateOverrides)settings;
                    o.Rotation = rotation;
                    o.EastSecond = eastSecond;
                    o.DarkFirst = darkFirst;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(TowerHitAt(0) - 0.1f))
                        wing1 = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                    if (probe.Crossed(TowerHitAt(1) - 0.1f))
                        wing2 = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        if (rotation == 0)
        {
            var farSide = darkFirst ? 1f : -1f;
            Assert.Multiple(() =>
            {
                Assert.That(wing1![MainTank].X, Is.InRange(-0.5f, 0.5f));
                Assert.That(wing1[MainTank].Z, Is.LessThan(-5f));

                Assert.That(wing2![MeleeDpsA].X * farSide, Is.GreaterThan(3f));
                Assert.That(wing2[MeleeDpsB].X * farSide, Is.GreaterThan(3f));
                Assert.That(wing2[PhysRangedDps].X * farSide, Is.LessThan(-3f));
                Assert.That(wing2[CasterDps].X * farSide, Is.LessThan(-3f));
            });
        }
    }
}
