using System.Collections.Generic;

namespace PowerGridSim.Models
{
    public class Record
    {
        public class NodeState
        {
            public double ActivePowerMW { get; set; }        // +/-
            public double ReactivePowerMvar { get; set; }
            public double VoltagePu { get; set; }
            public double VoltageKV { get; set; }            // VoltagePu * node's EffectiveKV
            public double VoltagePhaseAngle { get; set; }    // degrees
            public BusType BusType { get; set; }
            public bool IsBlackedOut { get; set; }
            public bool IsVoltageViolation { get; set; }     // outside Min/MaxVoltagePu
            public double DemandUnmetMW { get; set; }           
        }

        public class CableState
        {
            public double CurrentFlowMW { get; set; }        
            public double ReactiveFlowMvar { get; set; }
            public double ApparentMVA { get; set; }          // sqrt(P^2 + Q^2)
            public double LoadPercentage { get; set; }       // ApparentMVA / ThermalLimitMVA * 100
            public double LossMW { get; set; }
            public bool IsBroken { get; set; }
        }

        public class SimFrame
        {
            public int TotalHours { get; set; }
            public string TimeLabel { get; set; } = string.Empty;

            public double TotalGenerationMW { get; set; }
            public double TotalDemandMW { get; set; }
            public double TotalReactiveMvar { get; set; }
            public double TotalLineLossesMW { get; set; }
            public double DemandUnmetMW { get; set; }
            public int BlackedOutNodesCount { get; set; }
            public int VoltageViolationCount { get; set; }
            public int ConnectionCount { get; set; }
            public Dictionary<string, NodeState> NodeStates { get; set; } = new();
            public Dictionary<string, CableState> CableStates { get; set; } = new();
        }

        public class SimulationTimeline
        {
            public string TownName { get; set; } = "Town";
            public List<SimFrame> Frames { get; set; } = new();
        }
    }
}