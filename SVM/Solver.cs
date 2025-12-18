using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
public class Solver
    {
        private List<Component> _circuit;
        private int _nodeCount;
        public List<Component> States { get; private set; }
        public List<Component> Inputs { get; private set; }
        public Matrix<double> A { get; private set; }
        public Matrix<double> B { get; private set; }

        public Solver(List<Component> circuit)
        {
            _circuit = circuit;
            _nodeCount = circuit.Max(c => Math.Max(c.Node1, c.Node2)) + 1;
            States = circuit.Where(x => x.Type == ComponentType.Capacitor || x.Type == ComponentType.Inductor).ToList();
            Inputs = circuit.Where(x => x.Type == ComponentType.VoltageSource || x.Type == ComponentType.CurrentSource).ToList();
        }

        public void BuildMatrices()
        {
            int nx = States.Count;
            int nu = Inputs.Count;
            A = Matrix<double>.Build.Dense(nx, nx);
            B = Matrix<double>.Build.Dense(nx, Math.Max(1, nu));

            for (int j = 0; j < nx; j++) 
                A.SetColumn(j, SolveStep(j, -1));

            if (nu > 0)
                for (int k = 0; k < nu; k++) 
                    B.SetColumn(k, SolveStep(-1, k));
        }

        private Vector<double> SolveStep(int activeStateIdx, int activeInputIdx)
        {
            var vList = new List<(int n1, int n2, double val)>();
            for (int i = 0; i < Inputs.Count; i++)
                if (Inputs[i].Type == ComponentType.VoltageSource)
                    vList.Add((Inputs[i].Node1, Inputs[i].Node2, (i == activeInputIdx) ? 1.0 : 0.0));
            for (int i = 0; i < States.Count; i++)
                if (States[i].Type == ComponentType.Capacitor)
                    vList.Add((States[i].Node1, States[i].Node2, (i == activeStateIdx) ? 1.0 : 0.0));

            int mnaSize = (_nodeCount - 1) + vList.Count;
            var Y = Matrix<double>.Build.Dense(mnaSize, mnaSize);
            var J = Vector<double>.Build.Dense(mnaSize);

            for (int i = 0; i < _nodeCount - 1; i++) Y[i, i] += 1e-12; // Gmin

            foreach (var c in _circuit)
            {
                if (c.Type == ComponentType.Resistor) StampY(Y, c.Node1, c.Node2, 1.0 / c.Value);
                else if (c.Type == ComponentType.VCCS) StampVCCS(Y, c.Node1, c.Node2, c.ControlNode1, c.ControlNode2, c.Value);
            }

            for (int i = 0; i < Inputs.Count; i++)
                if (Inputs[i].Type == ComponentType.CurrentSource) StampJ(J, Inputs[i].Node1, Inputs[i].Node2, (i == activeInputIdx) ? 1.0 : 0.0);
            for (int i = 0; i < States.Count; i++)
                if (States[i].Type == ComponentType.Inductor) StampJ(J, States[i].Node1, States[i].Node2, (i == activeStateIdx) ? 1.0 : 0.0);

            int vOffset = _nodeCount - 1;
            for (int k = 0; k < vList.Count; k++)
            {
                int row = vOffset + k;
                if (vList[k].n1 != 0) { Y[row, vList[k].n1 - 1] = 1; Y[vList[k].n1 - 1, row] = 1; }
                if (vList[k].n2 != 0) { Y[row, vList[k].n2 - 1] = -1; Y[vList[k].n2 - 1, row] = -1; }
                Y[row, row] = -1e-12; // Anti-loop fix
                J[row] = vList[k].val;
            }

            Vector<double> X;
            try { X = Y.Solve(J); } catch { return Vector<double>.Build.Dense(States.Count); }

            var dX = Vector<double>.Build.Dense(States.Count);
            int vIdx = vOffset + Inputs.Count(x => x.Type == ComponentType.VoltageSource);

            for (int i = 0; i < States.Count; i++)
            {
                if (States[i].Type == ComponentType.Capacitor) dX[i] = X[vIdx++] / States[i].Value;
                else if (States[i].Type == ComponentType.Inductor)
                {
                    double v1 = (States[i].Node1 == 0) ? 0 : X[States[i].Node1 - 1];
                    double v2 = (States[i].Node2 == 0) ? 0 : X[States[i].Node2 - 1];
                    dX[i] = (v1 - v2) / States[i].Value;
                }
            }
            return dX;
        }

        void StampY(Matrix<double> Y, int n1, int n2, double g) {
            if(n1!=0)
            {
                Y[n1-1,n1-1]+=g; 
                if(n2!=0)
                    Y[n1-1,n2-1]-=g;
            }
            if(n2!=0)
            {
                Y[n2-1,n2-1]+=g; 
                if(n1!=0)
                    Y[n2-1,n1-1]-=g;}
        }
        void StampVCCS(Matrix<double> Y, int n1, int n2, int g, int s, double gm) {
            if(n1!=0)
            {
                if(g!=0)
                    Y[n1-1,g-1]+=gm; 
                if(s!=0)
                    Y[n1-1,s-1]-=gm;}

            if(n2!=0)
            {
                if(g!=0)
                    Y[n2-1,g-1]-=gm; 
                if(s!=0)
                    Y[n2-1,s-1]+=gm;
            }
        }
        void StampJ(Vector<double> J, int n1, int n2, double val) {
            if(n1!=0)
                J[n1-1]-=val;

            if(n2!=0)
                J[n2-1]+=val;
        }
    }