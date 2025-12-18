using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
public class CircuitReader
    {
        private string _inputDirectory;

        public CircuitReader(string inputDirectory)
        {
            _inputDirectory = inputDirectory;
            if (!Directory.Exists(_inputDirectory)) Directory.CreateDirectory(_inputDirectory);
        }

        public List<Component> ReadCircuit(string fileName)
        {
            string filePath = Path.Combine(_inputDirectory, fileName);
            if (!File.Exists(filePath)) 
                filePath = fileName; 
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл схемы не найден: {filePath}");

            var components = new List<Component>();
            int lineNumber = 0;

            try
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    lineNumber++;
                    var trimmed = line.Split(new[] { "//", "#" }, StringSplitOptions.None)[0].Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var comp = ParseComponentLine(trimmed, lineNumber);
                    if (comp != null) components.Add(comp);
                }
                return components;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка в файле {fileName} (строка {lineNumber}): {ex.Message}");
            }
        }

        private Component ParseComponentLine(string line, int lineNumber)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;

            string name = parts[0];
            ComponentType type = ParseType(parts[1]);
            double value = ParseValue(parts[2]);

            if (type == ComponentType.VCCS)
            {
                if (parts.Length < 7) throw new Exception("VCCS требует 7 параметров: Name Type Val N1 N2 CN1 CN2");
                return new Component(name, type, value, int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5]), int.Parse(parts[6]));
            }
            else
            {
                if (parts.Length < 5) throw new Exception($"Элемент {name} требует минимум 5 параметров.");
                return new Component(name, type, value, int.Parse(parts[3]), int.Parse(parts[4]));
            }
        }

        private ComponentType ParseType(string t)
        {
            t = t.ToLowerInvariant();
            if (t.StartsWith("r")) return ComponentType.Resistor;
            if (t.StartsWith("l") || t.Contains("ind")) return ComponentType.Inductor;
            if (t.StartsWith("c") && !t.Contains("cur")) return ComponentType.Capacitor;
            if (t.StartsWith("v") && t != "vccs") return ComponentType.VoltageSource;
            if (t.StartsWith("i") || t.StartsWith("j") || t.Contains("curr")) return ComponentType.CurrentSource;
            if (t == "vccs" || t == "g") return ComponentType.VCCS;
            throw new ArgumentException($"Неизвестный тип: {t}");
        }

        private double ParseValue(string s) => 
            double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
    }