using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace PowerGridSim.Models
{
    public sealed record AcResult(
    double[] VoltagePu, double[] AngleDeg,   // per node
    double[] P, double[] Q,                  // per node inc slack and Q from PV buses
    double[] FlowMW, double[] FlowMvar,      // per cable, From -> To
    double[] LossMW,                         // per cable
    BusType[] BusTypes,                      // per node
    int ConnectionCount);

    public class PowerGridEngine
    {
        public AcResult Solve(PowerGridBlueprint bp, double[] pReq, double[] qReq, ISet<string> openCableIds)
        {
            int n = bp.Nodes.Count, m = bp.Cables.Count;
            double baseMva = bp.BaseMVA > 0 ? bp.BaseMVA : 100.0;

            var volt = new double[n]; var ang = new double[n];
            var P = new double[n]; var Q = new double[n];
            var flowP = new double[m]; var flowQ = new double[m]; var loss = new double[m];
            var bus = new BusType[n];

            var imp = LineParameters.Calculate(bp);
            var index = bp.BuildNodeIndex();
            var connections = Connectivity.FindConnections(bp, openCableIds);

            foreach (var connection in connections)
            {
                var gens = connection.Where(i => bp.Nodes[i] is GeneratorNode).ToList();
                if (gens.Count == 0) continue;

                int slack = gens.OrderByDescending(i => ((GeneratorNode)bp.Nodes[i]).GeneratorType == GeneratorType.PowerPlant)
                    .ThenByDescending(i => bp.Nodes[i].Rating.NominalMVA).First();
                bus[slack] = BusType.Slack;

                foreach (int i in connection) { P[i] = pReq[i]; Q[i] = qReq[i]; }
                P[slack] -= connection.Sum(i => P[i]);

                var set = new HashSet<int>(connection);
                var lines = new List<(int cable, int a, int b, double x)>();
                for (int c = 0; c < m; c++)
                {
                    if (openCableIds.Contains(bp.Cables[c].Id)) continue;
                    int a = index[bp.Cables[c].FromNodeId], b = index[bp.Cables[c].ToNodeId];
                    if (set.Contains(a) && imp[c].XPu > 0) lines.Add((c, a, b, imp[c].XPu));
                }
                var w = lines.Select(l => (l.a, l.b, 1.0 / l.x)).ToList();
                var pPu = new double[n];
                foreach (int i in connection) pPu[i] = P[i] / baseMva;
                var theta = SolveLaplacian(n, connection, w, new() { [slack] = 0.0 }, pPu);

                var fixedV = new Dictionary<int, double>();
                foreach (int g in gens)
                {
                    var gn = (GeneratorNode)bp.Nodes[g];
                    if (g == slack || gn.ControlsVoltage) fixedV[g] = gn.VoltageSetpointPu;
                    if (g != slack && gn.ControlsVoltage) bus[g] = BusType.PV;
                }
                var qPu = new double[n];
                foreach (int i in connection) qPu[i] = Q[i] / baseMva;
                var v = SolveLaplacian(n, connection, w, fixedV, qPu);

                var netQ = new double[n];
                double connectionLoss = 0;
                foreach (var (c, a, b, x) in lines)
                {
                    double pij = (theta[a] - theta[b]) / x;      // pu
                    double qij = (v[a] - v[b]) / x;              // pu
                    double l = imp[c].RPu * (pij * pij + qij * qij) / (v[a] * v[a]);
                    flowP[c] = pij * baseMva; flowQ[c] = qij * baseMva; loss[c] = l * baseMva;
                    connectionLoss += loss[c];
                    netQ[a] += flowQ[c]; netQ[b] -= flowQ[c];
                }

                P[slack] += connectionLoss;                          
                foreach (int i in connection)
                {
                    volt[i] = v[i]; ang[i] = theta[i] * 180 / Math.PI;
                    if (bp.Nodes[i] is GeneratorNode) Q[i] = netQ[i];   
                }
            }

            return new AcResult(volt, ang, P, Q, flowP, flowQ, loss, bus, connections.Count);
        }

        private static double[] SolveLaplacian(int n, List<int> connection, List<(int a, int b, double w)> lines,
                                               Dictionary<int, double> fix, double[] rhs)
        {
            var result = new double[n];
            foreach (var kv in fix) result[kv.Key] = kv.Value;

            var unknown = connection.Where(i => !fix.ContainsKey(i)).ToList();
            if (unknown.Count == 0) return result;
            var pos = unknown.Select((node, k) => (node, k)).ToDictionary(t => t.node, t => t.k);

            var L = Matrix<double>.Build.Dense(unknown.Count, unknown.Count);
            var r = Vector<double>.Build.Dense(unknown.Count);
            foreach (var node in unknown) r[pos[node]] = rhs[node];

            foreach (var (a, b, w) in lines)
            {
                bool ua = pos.TryGetValue(a, out int ia), ub = pos.TryGetValue(b, out int ib);
                if (ua) { L[ia, ia] += w; if (ub) L[ia, ib] -= w; else r[ia] += w * fix[b]; }
                if (ub) { L[ib, ib] += w; if (ua) L[ib, ia] -= w; else r[ib] += w * fix[a]; }
            }

            var sol = L.Solve(r);
            foreach (var node in unknown) result[node] = sol[pos[node]];
            return result;
        }
    }
}