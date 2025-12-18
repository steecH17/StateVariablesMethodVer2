using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CircuitSimulator
{
    public static class SimulationEngine
    {
        public static void Run(List<Component> circuit, string fname, double tEnd, double dt, Dictionary<int, double> initialNodeVoltages, string outputFolder)
        {
            try
            {
                // 1. Проверка земли
                if (!circuit.Any(c => c.Node1 == 0 || c.Node2 == 0))
                {
                    int max = circuit.Max(c => Math.Max(c.Node1, c.Node2));
                    Console.WriteLine($"[Warning] Узел {max} принят за GND (0).");
                    foreach(var c in circuit) {
                        if(c.Node1==max) c.Node1=0; if(c.Node2==max) c.Node2=0;
                        if(c.ControlNode1==max) c.ControlNode1=0; if(c.ControlNode2==max) c.ControlNode2=0;
                    }
                }

                // 2. Топология
                var topo = new TopologyAnalyzer(circuit);
                topo.Analyze();

                // 3. Матрицы
                Console.WriteLine("\n=== ЭТАП 3: Построение уравнений ===");
                var solver = new Solver(circuit);
                solver.BuildMatrices();

                Console.WriteLine("--- Matrix A ---");
                PrintMatrix(solver.A);

                // Вывод уравнений
                Console.WriteLine("\n[Уравнения состояния]:");
                for (int i = 0; i < solver.States.Count; i++)
                {
                    string eq = $"d[{GetLabel(solver.States[i])}]/dt = ";
                    for (int j = 0; j < solver.States.Count; j++)
                        if (Math.Abs(solver.A[i, j]) > 1e-12)
                            eq += $"{solver.A[i, j]:0.0E+0} * {GetLabel(solver.States[j])} + ";
                    Console.WriteLine(eq.TrimEnd(' ', '+'));
                }

                // 4. Подготовка к решению
                int steps = (int)(tEnd / dt);
                if (steps > 100000) 
                {
                    Console.WriteLine($"Слишком много шагов ({steps}). Ограничиваем до 100000.");
                    steps = 100000;
                    tEnd = steps * dt;
                }

                int nx = solver.States.Count;
                Console.WriteLine($"\n=== ЭТАП 4: Решение ===");
                Console.WriteLine($"T_end={tEnd:G3}s, dt={dt:G3}s, Steps={steps}");

                var I = Matrix<double>.Build.DenseIdentity(nx);
                var M_left = (I - solver.A * (dt / 2.0)).Inverse(); 
                var M_right = (I + solver.A * (dt / 2.0));
                
                var X = Vector<double>.Build.Dense(nx);

                if (initialNodeVoltages != null && initialNodeVoltages.Count > 0)
                {
                    Console.WriteLine("Применение начальных условий...");
                    for (int i = 0; i < nx; i++)
                    {
                        var comp = solver.States[i];
                        if (comp.Type == ComponentType.Capacitor)
                        {
                            // U_c = Phi_1 - Phi_2
                            double v1 = initialNodeVoltages.ContainsKey(comp.Node1) ? initialNodeVoltages[comp.Node1] : 0.0;
                            double v2 = initialNodeVoltages.ContainsKey(comp.Node2) ? initialNodeVoltages[comp.Node2] : 0.0;
                            X[i] = v1 - v2;
                            if (Math.Abs(X[i]) > 1e-9)
                                Console.WriteLine($"   -> {comp.Name}: {X[i]} V");
                        }
                    }
                }
                else if (fname.Contains("LC") && solver.Inputs.Count == 0)
                {
                    for(int i=0; i<nx; i++) if (solver.States[i].Type == ComponentType.Capacitor) X[i] = 10.0;
                    Console.WriteLine("Авто-запуск LC: Uc = 10В");
                }

                var history = new Dictionary<string, double[]>();
                foreach (var c in solver.States) history[GetLabel(c)] = new double[steps];
                var tAxis = new double[steps];
                
                var U_curr = CalculateInputs(0, solver, tEnd);
                
                int printRate = Math.Max(1, steps / 15);
                Console.WriteLine($"\n{"Time",-10} | " + string.Join(" | ", solver.States.Select(s => $"{GetLabel(s),-10}")));

                // Цикл
                for (int i = 0; i < steps; i++)
                {
                    double t = i * dt;
                    tAxis[i] = t;

                    var U_next = CalculateInputs(t + dt, solver, tEnd);
                    var U_avg = (U_curr + U_next) * 0.5;

                    var TermInput = solver.B * U_avg * dt;
                    X = M_left * (M_right * X + TermInput);
                    
                    U_curr = U_next;

                    for (int k = 0; k < nx; k++) 
                        history[GetLabel(solver.States[k])][i] = X[k];

                    if (i % printRate == 0 || i == steps - 1)
                         Console.WriteLine($"{t,-10:G3} | " + string.Join(" | ", X.Select(v => $"{v,-10:F4}")));
                }

                GraphPlotter.PlotSeparate(fname, tAxis, history, outputFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ОШИБКА ДВИЖКА]: {ex.Message}");
            }
        }

        private static string GetLabel(Component c) => 
            c.Type == ComponentType.Capacitor ? $"U({c.Name})" : $"I({c.Name})";

        private static Vector<double> CalculateInputs(double t, Solver solver, double tEnd)
        {
            var U = Vector<double>.Build.Dense(Math.Max(1, solver.Inputs.Count));
            for (int k = 0; k < solver.Inputs.Count; k++)
            {
                var inp = solver.Inputs[k];
                double val = inp.Value;
                if (inp.Name.StartsWith("U") && !inp.Name.Contains("dd")) 
                {
                    if (t < tEnd * 0.2) val = 0;
                    else if (t < tEnd * 0.25) val = inp.Value * (t - tEnd * 0.2) / (tEnd * 0.05);
                    else val = inp.Value;
                }
                U[k] = val;
            }
            return U;
        }

        private static void PrintMatrix(Matrix<double> M)
        {
            for (int i = 0; i < M.RowCount; i++) {
                for (int j = 0; j < M.ColumnCount; j++) 
                    Console.Write($"{M[i, j],-12:0.00E+00} ");
                Console.WriteLine();
            }
        }
    }
}