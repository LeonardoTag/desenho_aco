using System;
using System.Collections.Generic;
using System.Linq;

namespace CapitalAco.DrawingMacro.App.Services
{
    public static class NelderMeadSolver
    {
        public delegate double[] ObjectiveFunction(double[] x);

        public static double[] Optimize(
            ObjectiveFunction func,
            double[] x0,
            (double Low, double High)[] bounds,
            double tol = 1e-12,
            int maxIter = 250)
        {
            int n = x0.Length;

            double Objective(double[] x)
            {
                double penalty = 0.0;
                for (int i = 0; i < n; i++)
                {
                    if (x[i] < bounds[i].Low)
                    {
                        penalty += Math.Pow(bounds[i].Low - x[i], 2) + 1e6;
                    }
                    else if (x[i] > bounds[i].High)
                    {
                        penalty += Math.Pow(x[i] - bounds[i].High, 2) + 1e6;
                    }
                }

                if (penalty > 0)
                {
                    return 1e12 + penalty;
                }

                try
                {
                    double[] res = func(x);
                    double sumSq = 0.0;
                    foreach (double r in res)
                    {
                        sumSq += r * r;
                    }
                    return sumSq;
                }
                catch
                {
                    return 1e18;
                }
            }

            double alpha = 1.0;
            double gamma = 2.0;
            double rho = 0.5;
            double sigma = 0.5;

            var simplex = new List<double[]>();
            simplex.Add((double[])x0.Clone());

            for (int i = 0; i < n; i++)
            {
                double[] vertex = (double[])x0.Clone();
                double step = Math.Max(Math.Abs(x0[i]) * 0.05, 0.1);
                vertex[i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, vertex[i] + step));
                simplex.Add(vertex);
            }

            var fVal = simplex.Select(v => Objective(v)).ToList();

            for (int iter = 0; iter < maxIter; iter++)
            {
                var indices = Enumerable.Range(0, n + 1).OrderBy(idx => fVal[idx]).ToList();
                simplex = indices.Select(idx => simplex[idx]).ToList();
                fVal = indices.Select(idx => fVal[idx]).ToList();

                double[] centroid = new double[n];
                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        centroid[i] += simplex[j][i];
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    centroid[i] /= n;
                }

                double size = 0.0;
                for (int j = 0; j < n + 1; j++)
                {
                    double distSq = 0.0;
                    for (int i = 0; i < n; i++)
                    {
                        distSq += Math.Pow(simplex[j][i] - centroid[i], 2);
                    }
                    size += distSq;
                }

                if (size < tol || (fVal[n] - fVal[0]) < tol)
                {
                    break;
                }

                double[] worst = simplex[n];

                // Reflection
                double[] reflected = new double[n];
                for (int i = 0; i < n; i++)
                {
                    reflected[i] = centroid[i] + alpha * (centroid[i] - worst[i]);
                    reflected[i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, reflected[i]));
                }
                double fR = Objective(reflected);

                if (fR < fVal[0])
                {
                    // Expansion
                    double[] expanded = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        expanded[i] = centroid[i] + gamma * (reflected[i] - centroid[i]);
                        expanded[i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, expanded[i]));
                    }
                    double fE = Objective(expanded);

                    if (fE < fR)
                    {
                        simplex[n] = expanded;
                        fVal[n] = fE;
                    }
                    else
                    {
                        simplex[n] = reflected;
                        fVal[n] = fR;
                    }
                }
                else if (fR < fVal[n - 1])
                {
                    simplex[n] = reflected;
                    fVal[n] = fR;
                }
                else
                {
                    // Contraction
                    if (fR < fVal[n])
                    {
                        // Outside contraction
                        double[] contracted = new double[n];
                        for (int i = 0; i < n; i++)
                        {
                            contracted[i] = centroid[i] + rho * (reflected[i] - centroid[i]);
                            contracted[i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, contracted[i]));
                        }
                        double fC = Objective(contracted);

                        if (fC <= fR)
                        {
                            simplex[n] = contracted;
                            fVal[n] = fC;
                            continue;
                        }
                    }
                    else
                    {
                        // Inside contraction
                        double[] contracted = new double[n];
                        for (int i = 0; i < n; i++)
                        {
                            contracted[i] = centroid[i] + rho * (worst[i] - centroid[i]);
                            contracted[i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, contracted[i]));
                        }
                        double fC = Objective(contracted);

                        if (fC < fVal[n])
                        {
                            simplex[n] = contracted;
                            fVal[n] = fC;
                            continue;
                        }
                    }

                    // Shrink
                    double[] best = simplex[0];
                    for (int j = 1; j <= n; j++)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            simplex[j][i] = best[i] + sigma * (simplex[j][i] - best[i]);
                            simplex[j][i] = Math.Max(bounds[i].Low, Math.Min(bounds[i].High, simplex[j][i]));
                        }
                        fVal[j] = Objective(simplex[j]);
                    }
                }
            }

            return simplex[0];
        }
    }
}
