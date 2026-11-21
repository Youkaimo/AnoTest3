using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P4DarklitDragonsong;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P4DarklitDragonsong.FruP4DarklitDragonsongState;

namespace AnoMech.Tests;

public class FruP4DarklitDragonsongManaAiTests
{
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    public void ManaLineupAndBowtieBoxHourglassResolveCleanly(int shape, bool eastWing)
    {
        Dictionary<PartyRole, Vector3>? lineup = null;
        Dictionary<PartyRole, Vector3>? towers = null;
        Dictionary<PartyRole, Vector3>? akhMorn = null;

        var run = ScenarioRun.Execute(typeof(FruP4DarklitDragonsongScenario), 2, 10 + shape * 2 + (eastWing ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = ShieldHealer,
                Overrides = settings =>
                {
                    var o = (FruP4DarklitDragonsongStateOverrides)settings;
                    o.EastWing = eastWing;
                    o.ChainedTank = OffTank;
                    o.ChainedHealer = RegenHealer;
                    o.DpsOrder = [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps];
                    o.Shape = shape;
                    o.TetherWaterIndex = 0;
                    o.FreeWaterIndex = 0;
                    o.SpiritTakerTarget = MeleeDpsA;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(24.1f))
                        lineup = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                    if (probe.Crossed(TowersAt - 0.1f))
                        towers = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                    if (probe.Crossed(57.6f))
                        akhMorn = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(lineup![MeleeDpsA].X, Is.LessThan(lineup[MeleeDpsB].X));
            Assert.That(lineup[MeleeDpsB].X, Is.LessThan(lineup[PhysRangedDps].X));
            Assert.That(lineup[PhysRangedDps].X, Is.LessThan(lineup[CasterDps].X));

            Assert.That(towers![RegenHealer].Z, Is.LessThan(-5f));
            Assert.That(towers[ShieldHealer].Z, Is.GreaterThan(0f));
            Assert.That(towers[PhysRangedDps].Z, Is.LessThan(0f));

            Assert.That(akhMorn![MainTank].X, Is.LessThan(-7f));
            Assert.That(akhMorn[OffTank].Length(), Is.LessThan(1.5f));
        });
    }
}
