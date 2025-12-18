using System;
using System.Globalization;
using System.IO;

namespace CircuitSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            string currentDir = Directory.GetCurrentDirectory();
            string inputFolder = Path.Combine(currentDir, "InputCircuit");
            string plotFolder = Path.Combine(currentDir, "ResultPlots");

            if (!Directory.Exists(inputFolder)) Directory.CreateDirectory(inputFolder);
            if (!Directory.Exists(plotFolder)) Directory.CreateDirectory(plotFolder);

            GenerateExampleFiles(inputFolder);

            CircuitReader reader = new CircuitReader(inputFolder);

            while (true)
            {
                Console.WriteLine("\n==========================================");
                Console.WriteLine("   STATE VARIABLES METHOD");
                Console.WriteLine("==========================================");

                string[] files = Directory.GetFiles(inputFolder, "*.txt");
                if (files.Length == 0) Console.WriteLine(" [Папка InputCircuit пуста]");
                foreach (var f in files) Console.WriteLine($" {Path.GetFileName(f)}");

                Console.Write("\nВведите имя файла (или exit): ");
                string fname = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(fname) || fname.ToLower() == "exit") break;
                if (!fname.EndsWith(".txt")) fname += ".txt";

                try
                {
                    var circuit = reader.ReadCircuit(fname);

                    // АВТО-ПОДБОР ПАРАМЕТРОВ
                    double defaultEnd = 0.01;
                    double defaultDt = 1e-5;
                    bool hasMicro = circuit.Any(c => (c.Type == ComponentType.Capacitor && c.Value < 1e-9) || (c.Type == ComponentType.Inductor && c.Value < 1e-6));

                    if (hasMicro) { defaultDt = 1e-12; defaultEnd = 40e-9; }
                    else if (fname.Contains("LC")) { defaultEnd = 20.0; defaultDt = 0.01; }
                    else if (fname.Contains("step")) { defaultEnd = 0.01; defaultDt = 1e-6; }

                    Console.WriteLine($"\n[Настройка времени] (Enter = авто: T={defaultEnd}, dt={defaultDt})");

                    Console.Write($"Введите время симуляции (T_end): ");
                    string inputTend = Console.ReadLine();
                    double tEnd = string.IsNullOrWhiteSpace(inputTend) ? defaultEnd : double.Parse(inputTend);

                    Console.Write($"Введите шаг (dt): ");
                    string inputDt = Console.ReadLine();
                    double dt = string.IsNullOrWhiteSpace(inputDt) ? defaultDt : double.Parse(inputDt);

                    Console.WriteLine("\n[Начальные условия] Формат: НомерУзла=Вольт (например: 1=5.0 2=0)");
                    Console.WriteLine("Оставьте пустым, чтобы все было 0.");
                    Console.Write("> ");
                    string inputInit = Console.ReadLine();

                    var initialNodes = new Dictionary<int, double>();
                    if (!string.IsNullOrWhiteSpace(inputInit))
                    {
                        var pairs = inputInit.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in pairs)
                        {
                            var parts = p.Split('=');
                            if (parts.Length == 2)
                            {
                                int node = int.Parse(parts[0]);
                                double vol = double.Parse(parts[1]);
                                initialNodes[node] = vol;
                            }
                        }
                    }

                    SimulationEngine.Run(circuit, fname, tEnd, dt, initialNodes, plotFolder);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[ОШИБКА]: {ex.Message}");
                }
            }
        }

        static void GenerateExampleFiles(string dir)
        {
            void Write(string name, string content)
            {
                string p = Path.Combine(dir, name);
                if (!File.Exists(p)) File.WriteAllText(p, content);
            }
            Write("testLC.txt", "C Capacitor 1 1 2\nL Inductor 1 2 1");
            Write("RLC_step.txt", "U_src V 10 1 0\nR1 R 5 1 2\nL1 Inductor 0.001 2 3\nC1 Capacitor 10e-6 3 0");
            Write("inv_off.txt", "// OFF\nV_dd V 5.0 3 0\nV_in V 0.0 4 0\nR_g R 50 4 1\nR_load R 1000 3 2\nJ_mos VCCS 0.02 2 0 1 0\nC_gs C 2e-12 1 0\nC_ds C 1e-12 2 0\nC_load C 5e-12 2 0");
            Write("inv_on.txt", "// ON\nV_dd V 5.0 3 0\nV_in V 3.0 4 0\nR_g R 50 4 1\nR_load R 1000 3 2\nJ_mos VCCS 0.02 2 0 1 0\nC_gs C 2e-12 1 0\nC_gd C 1e-12 1 2\nC_ds C 1e-12 2 0\nC_load C 5e-12 2 0");
            Write("active_inverter.txt", "// Dynamic\nV_dd V 5.0 3 0\nU_in V 3.0 1 0\nR_load R 1000 3 2\nJ_mos VCCS 0.02 2 0 1 0\nC_gs C 2e-12 1 0\nC_gd C 1e-12 1 2\nC_ds C 1e-12 2 0\nC_load C 5e-12 2 0");
            Write("transistor_circuit.txt", "Uin V 5 1 0\nVdd V 12 2 0\nR1 R 10000 2 3\nCgs C 1e-12 1 0\nCgd C 1e-13 1 3\nCds C 1e-12 3 0\nRds R 1000 3 0\nJd VCCS 0.001 3 0 1 0");
            Write("nand_circuit.txt", "V_dd V 5.0 3 0\nV_const V 3.0 6 0\nR_g2 R 50 6 4\nU_in V 3.0 7 0\nR_g1 R 50 7 5\nR_load R 1000 3 2\nC_load C 5e-12 2 0\nJ_vt2 VCCS 0.02 2 1 4 1\nC_gs2 C 2e-12 4 1\nC_gd2 C 1e-12 4 2\nC_ds2 C 1e-12 2 1\nJ_vt1 VCCS 0.02 1 0 5 0\nC_gs1 C 2e-12 5 0\nC_gd1 C 1e-12 5 1\nC_ds1 C 1e-12 1 0");
        }
    }
}