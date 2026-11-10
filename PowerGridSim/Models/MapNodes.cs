using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PowerGridSim.Models
{
    public enum GeneratorType { PowerPlant, AlternativeGeneration, HVDC_Link }
    public enum SubstationType { TransmissionSubstation, GridSupplyPoint, DistributionSubstation }
    public enum ConsumerType { Industrial, Commercial, Residential }
    public enum NodeCableType { TransmissionLine, DistributionLine }

    public enum BusType { PQ, PV, Slack }

    internal static class LoadProfiles
    {
        public static readonly double[] Solar = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.3, 0.6, 0.8, 1.0, 1.0, 0.9, 0.7, 0.4, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
        public static readonly double[] Wind = { 0.6, 0.6, 0.7, 0.7, 0.8, 0.8, 0.7, 0.6, 0.5, 0.5, 0.6, 0.7, 0.8, 0.8, 0.7, 0.6, 0.6, 0.7, 0.8, 0.9, 0.8, 0.7, 0.6, 0.6 };
        public static readonly double[] Industrial = { 0.3, 0.3, 0.3, 0.4, 0.5, 0.7, 0.9, 1.0, 1.0, 1.0, 1.0, 1.0, 0.9, 0.9, 0.9, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.3, 0.3 };
        public static readonly double[] Commercial = { 0.1, 0.1, 0.1, 0.1, 0.2, 0.4, 0.7, 0.9, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.9, 0.8, 0.7, 0.6, 0.4, 0.3, 0.2, 0.1, 0.1, 0.1 };
        public static readonly double[] Residential = { 0.2, 0.15, 0.15, 0.15, 0.2, 0.3, 0.5, 0.6, 0.5, 0.4, 0.4, 0.4, 0.4, 0.4, 0.4, 0.5, 0.7, 0.9, 1.0, 0.9, 0.7, 0.5, 0.3, 0.2 };

        public static double At(double[] profile, int hour) => profile[((hour % 24) + 24) % 24];
    }

    public class PowerRating
    {
        [XmlAttribute("NominalMW")] public double NominalMVA { get; set; }
        [XmlAttribute("ThermalLimitMW")] public double ThermalLimitMVA { get; set; }
        [XmlAttribute("EmergencyLimitMW")] public double EmergencyLimitMVA { get; set; }

        public PowerRating() { }
        public PowerRating(double nominal)
        {
            NominalMVA = nominal;
            ThermalLimitMVA = nominal;
            EmergencyLimitMVA = nominal * 1.15;
        }

        public static double Apparent(double pMW, double qMvar) => Math.Sqrt(pMW * pMW + qMvar * qMvar);
        public bool IsOverThermalLimit(double pMW, double qMvar) => Apparent(pMW, qMvar) > ThermalLimitMVA;
        public bool IsOverEmergencyLimit(double pMW, double qMvar) => Apparent(pMW, qMvar) > EmergencyLimitMVA;
    }

    [XmlInclude(typeof(GeneratorNode))]
    [XmlInclude(typeof(SubstationNode))]
    [XmlInclude(typeof(ConsumerNode))]
    public abstract class MapNode
    {
        [XmlAttribute] public string Id { get; set; } = string.Empty;
        [XmlAttribute] public string Name { get; set; } = string.Empty;
        [XmlAttribute] public double X { get; set; }
        [XmlAttribute] public double Y { get; set; }

        [XmlAttribute] public double NominalKV { get; set; }
        public PowerRating Rating { get; set; } = new();

        [XmlIgnore] public double EffectiveKV => NominalKV > 0 ? NominalKV : DefaultKV;
        [XmlIgnore] protected abstract double DefaultKV { get; }
    }

    public class GeneratorNode : MapNode
    {
        [XmlAttribute] public GeneratorType GeneratorType { get; set; }
        [XmlAttribute] public double VoltageSetpointPu { get; set; } = 1.02;
        [XmlAttribute] public double MinMvar { get; set; }
        [XmlAttribute] public double MaxMvar { get; set; }

        [XmlIgnore] protected override double DefaultKV => 400;
        [XmlIgnore] public double EffectiveMaxMvar => MaxMvar != 0 ? MaxMvar : 0.4 * Rating.NominalMVA;
        [XmlIgnore] public double EffectiveMinMvar => MinMvar != 0 ? MinMvar : -0.4 * Rating.NominalMVA;
        [XmlIgnore] public bool ControlsVoltage => GeneratorType != GeneratorType.AlternativeGeneration;

        public double CapacityFactorAt(SimTime t) => GeneratorType switch
        {
            GeneratorType.AlternativeGeneration => LoadProfiles.At(LoadProfiles.Solar, t.HourOfDay),
            GeneratorType.HVDC_Link => LoadProfiles.At(LoadProfiles.Wind, t.HourOfDay),
            _ => 1.0
        };
        public double AvailableMWAt(SimTime t) => Rating.NominalMVA * CapacityFactorAt(t);
    }

    public class SubstationNode : MapNode
    {
        [XmlAttribute] public SubstationType SubstationType { get; set; }
        [XmlAttribute] public double PrimaryKV { get; set; }
        [XmlAttribute] public double SecondaryKV { get; set; }

    [XmlIgnore]
        protected override double DefaultKV => SubstationType switch
        {
            SubstationType.TransmissionSubstation => 400,
            SubstationType.GridSupplyPoint => 132,
            _ => 11
        };
        [XmlIgnore]
        public double EffectivePrimaryKV => PrimaryKV > 0 ? PrimaryKV : DefaultKV;

        [XmlIgnore]
        public double EffectiveSecondaryKV => SecondaryKV > 0 ? SecondaryKV : (DefaultKV > 100 ? 132 : 11);

        [XmlIgnore]
        public bool IsStepDownTransformer => EffectivePrimaryKV != EffectiveSecondaryKV;
    }

    public class ConsumerNode : MapNode
    {
        [XmlAttribute] public ConsumerType ConsumerType { get; set; }
        [XmlAttribute] public double PowerFactor { get; set; }

        [XmlIgnore]
        public double EffectivePowerFactor => PowerFactor > 0 ? PowerFactor : ConsumerType switch
        {
            ConsumerType.Industrial => 0.90,
            ConsumerType.Commercial => 0.95,
            _ => 0.97
        };

        [XmlIgnore] protected override double DefaultKV => ConsumerType == ConsumerType.Industrial ? 132 : 11;

        public double DemandFactorAt(SimTime t) => ConsumerType switch
        {
            ConsumerType.Industrial => LoadProfiles.At(LoadProfiles.Industrial, t.HourOfDay),
            ConsumerType.Commercial => LoadProfiles.At(LoadProfiles.Commercial, t.HourOfDay),
            _ => LoadProfiles.At(LoadProfiles.Residential, t.HourOfDay)
        };

        public double DemandMWAt(SimTime t)
        {
            double weekend = t.IsWeekend
                ? ConsumerType switch { ConsumerType.Industrial => 0.4, ConsumerType.Commercial => 0.6, _ => 1.0 }
                : 1.0;
            return Rating.NominalMVA * EffectivePowerFactor * DemandFactorAt(t) * weekend;
        }

        // Q = P * tan(acos(pf)), constant-power load.
        public double DemandMvarAt(SimTime t) =>
            DemandMWAt(t) * Math.Tan(Math.Acos(EffectivePowerFactor));
    }

    public class GridCable
    {
        [XmlAttribute] public string Id { get; set; } = string.Empty;
        [XmlAttribute] public string Name { get; set; } = string.Empty;
        [XmlAttribute] public NodeCableType Type { get; set; }
        [XmlAttribute] public string FromNodeId { get; set; } = string.Empty;
        [XmlAttribute] public string ToNodeId { get; set; } = string.Empty;
        [XmlAttribute] public double LengthKm { get; set; }
        [XmlAttribute] public double RepairHours { get; set; }

        [XmlIgnore] public double ResistancePu { get; set; }
        [XmlIgnore] public double ReactancePu { get; set; }
        [XmlIgnore] public double SusceptanceMWPerRad { get; set; }

        public PowerRating Rating { get; set; } = new();

    }

    [XmlRoot("PowerGridBlueprint")]
    public class PowerGridBlueprint
    {
        [XmlAttribute] public string TownName { get; set; } = "Default Town";
        [XmlAttribute] public double BaseMVA { get; set; } = 100;
        [XmlAttribute] public double MinVoltagePu { get; set; } = 0.95;
        [XmlAttribute] public double MaxVoltagePu { get; set; } = 1.05;

        [XmlArray("Nodes")]
        [XmlArrayItem("MapNode")]
        public List<MapNode> Nodes { get; set; } = new();

        [XmlArray("Cables")]
        [XmlArrayItem("GridCable")]
        public List<GridCable> Cables { get; set; } = new();

        public Dictionary<string, int> BuildNodeIndex()
        {
            var map = new Dictionary<string, int>(Nodes.Count);
            for (int i = 0; i < Nodes.Count; i++) map[Nodes[i].Id] = i;
            return map;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>();
            foreach (var n in Nodes)
                if (!seen.Add(n.Id)) errors.Add($"Duplicate node id '{n.Id}'");

            var cableIds = new HashSet<string>();
            foreach (var c in Cables)
            {
                if (!cableIds.Add(c.Id)) errors.Add($"Duplicate cable id '{c.Id}'");
                if (!seen.Contains(c.FromNodeId)) errors.Add($"{c.Id}: unknown From node '{c.FromNodeId}'");
                if (!seen.Contains(c.ToNodeId)) errors.Add($"{c.Id}: unknown To node '{c.ToNodeId}'");
                if (c.FromNodeId == c.ToNodeId) errors.Add($"{c.Id}: connects a node to itself");
            }
            if (BaseMVA <= 0) errors.Add("BaseMVA must be positive");
            return errors;
        }
    }
}