using System;

namespace LivingWorld.Simulation.Economy
{
    /// <summary>
    /// Condition (0-100) of the four shared structures behind the infrastructure stat
    /// (P5-01, TOWN.md): the cracking mill wheel, the palisade, wells and roads, and the
    /// granary building. Defaults are the design's starting values.
    /// </summary>
    public sealed class TownInfrastructureState
    {
        public TownInfrastructureState(int wheelCondition = 30, int palisadeCondition = 80,
            int wellAndRoadsCondition = 70, int granaryBuildingCondition = 75)
        {
            WheelCondition = Validate(wheelCondition, nameof(wheelCondition));
            PalisadeCondition = Validate(palisadeCondition, nameof(palisadeCondition));
            WellAndRoadsCondition = Validate(wellAndRoadsCondition, nameof(wellAndRoadsCondition));
            GranaryBuildingCondition = Validate(granaryBuildingCondition, nameof(granaryBuildingCondition));
        }

        public int WheelCondition { get; private set; }
        public int PalisadeCondition { get; private set; }
        public int WellAndRoadsCondition { get; private set; }
        public int GranaryBuildingCondition { get; private set; }

        public void SetWheelCondition(int condition) => WheelCondition = Validate(condition, nameof(condition));
        public void SetPalisadeCondition(int condition) => PalisadeCondition = Validate(condition, nameof(condition));
        public void SetWellAndRoadsCondition(int condition) => WellAndRoadsCondition = Validate(condition, nameof(condition));
        public void SetGranaryBuildingCondition(int condition) => GranaryBuildingCondition = Validate(condition, nameof(condition));

        private static int Validate(int condition, string name)
        {
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(name, "Condition is 0-100.");
            return condition;
        }
    }
}
