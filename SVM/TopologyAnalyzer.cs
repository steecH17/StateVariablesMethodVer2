using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CircuitSimulator
{
    public class TopologyAnalyzer
    {
        private List<Component> _circuit;
        private int _nodeCount;
        
        public List<Component> Tree { get; private set; } = new List<Component>();
        public List<Component> Links { get; private set; } = new List<Component>();
        public Matrix<double> MMatrix { get; private set; }

        public TopologyAnalyzer(List<Component> circuit)
        {
            _circuit = circuit;
            _nodeCount = circuit.Max(c => Math.Max(c.Node1, c.Node2)) + 1;
        }

        public void Analyze()
        {
            Console.WriteLine("\n=== ЭТАП 1: Топологический анализ ===");
            
            // 1. Построение дерева (Приоритет: E -> C -> R -> L -> J)
            var sorted = _circuit.OrderBy(c => (int)c.Type).ToList();
            
            int[] parent = Enumerable.Range(0, _nodeCount).ToArray();
            int Find(int i) { while (i != parent[i]) i = parent[i]; return i; }
            void Union(int i, int j) { int r1 = Find(i); int r2 = Find(j); if (r1 != r2) parent[r1] = r2; }

            Tree.Clear(); 
            Links.Clear();

            foreach (var c in sorted)
            {
                if (Find(c.Node1) != Find(c.Node2)) 
                {
                    Tree.Add(c); 
                    c.IsTree = true; 
                    Union(c.Node1, c.Node2);
                } 
                else 
                {
                    Links.Add(c); 
                    c.IsTree = false;
                }
            }

            Console.WriteLine($"[Граф] Ветви дерева ({Tree.Count}): {string.Join(", ", Tree.Select(c => c.Name))}");
            Console.WriteLine($"[Граф] Хорды ({Links.Count}): {string.Join(", ", Links.Select(c => c.Name))}");

            // 2. М-Матрица
            if (Links.Count > 0 && Tree.Count > 0)
            {
                Console.WriteLine("\n=== ЭТАП 2: Матрица главных контуров (M-Matrix) ===");
                MMatrix = Matrix<double>.Build.Dense(Links.Count, Tree.Count);
                
                for (int i = 0; i < Links.Count; i++)
                {
                    var path = FindPath(Links[i].Node2, Links[i].Node1);
                    foreach (var p in path)
                    {
                        int idx = Tree.IndexOf(p.Comp);
                        if (idx >= 0) MMatrix[i, idx] = p.Direction;
                    }
                }
                PrintMMatrix();
            }
        }

        private void PrintMMatrix()
        {
            Console.Write("      ");
            foreach (var t in Tree) Console.Write($"{t.Name,-7}");
            Console.WriteLine();
            for (int i = 0; i < Links.Count; i++)
            {
                Console.Write($"{Links[i].Name,-6}");
                for (int j = 0; j < Tree.Count; j++)
                    Console.Write($"{(MMatrix[i, j] == 0 ? "." : (MMatrix[i, j] > 0 ? "1" : "-1")),-7}");
                Console.WriteLine();
            }
        }

        private List<PathNode> FindPath(int start, int end)
        {
            return RecSearch(start, end, new HashSet<int> { start }) ?? new List<PathNode>();
        }

        private List<PathNode> RecSearch(int curr, int target, HashSet<int> visited)
        {
            if (curr == target) return new List<PathNode>();
            
            foreach (var br in Tree)
            {
                int next = (br.Node1 == curr) ? br.Node2 : (br.Node2 == curr ? br.Node1 : -1);
                
                if (next != -1 && !visited.Contains(next))
                {
                    visited.Add(next);
                    var res = RecSearch(next, target, visited);
                    if (res != null) 
                    {
                        int dir = (br.Node1 == curr) ? 1 : -1;
                        res.Add(new PathNode { Comp = br, Direction = dir });
                        return res;
                    }
                }
            }
            return null;
        }
    }
}