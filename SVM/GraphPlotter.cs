using ScottPlot;
public static class GraphPlotter
    {
        public static void PlotSeparate(string simName, double[] time, Dictionary<string, double[]> data, string baseFolder)
        {
            string folderName = $"{Path.GetFileNameWithoutExtension(simName)}_{DateTime.Now:HH-mm-ss}";
            string fullFolderPath = Path.Combine(baseFolder, folderName);
            
            if (!Directory.Exists(fullFolderPath)) Directory.CreateDirectory(fullFolderPath);

            Console.WriteLine($"\n[Графики] Сохраняются в папку: {folderName}");

            foreach (var kvp in data)
            {
                string varName = kvp.Key;
                double[] values = kvp.Value;

                double min = values.Min();
                double max = values.Max();
                if (Math.Abs(max - min) < 1e-9 && Math.Abs(max) < 0.01) continue;

                var plt = new Plot();
                
                plt.Title($"{varName}  [{simName}]");
                plt.XLabel("Time (s)");
                
                if (varName.StartsWith("U")) plt.YLabel("Voltage (V)");
                else if (varName.StartsWith("I")) plt.YLabel("Current (A)");
                else plt.YLabel("Value");

                var sig = plt.Add.Scatter(time, values);
                sig.LineWidth = 3;
                sig.MarkerSize = 0;
                
                sig.LegendText = varName;
                plt.ShowLegend();

                string safeName = varName.Replace("(", "_").Replace(")", "").Replace(" ", "");
                string filename = $"{safeName}.png"; 
                string fullPath = Path.Combine(fullFolderPath, filename);

                plt.SavePng(fullPath, 800, 600);
                Console.WriteLine($"   -> Сохранен: {filename}");
            }
        }
    }