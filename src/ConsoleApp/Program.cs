using System.Diagnostics;
using GreenWave.Domain;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

double[] testSpeeds = Enumerable.Range(0, 21).Select(k => (20 + 2 * k) / 3.6).ToArray();

ISolver[] solvers =
[
    new DynamicProgrammingSolver(testSpeeds),
    new BruteForceSolver(testSpeeds),
];

Compare("TestProblem1", Samples.TestProblem1());
Compare("A, Tmax=120", Samples.MovingStart());
Compare("A, Tmax=110", Samples.MovingStart(110));
Compare("B, Tmax=106", Samples.StopIsBetter());
Compare("B, Tmax=90", Samples.StopIsBetter(90));

void Compare(string title, GreenWaveProblem problem)
{
    Console.WriteLine(title);
    var model = new MotionModel(problem);

    foreach (var solver in solvers)
    {
        var sw = Stopwatch.StartNew();
        var route = solver.Solve(model);
        sw.Stop();
        Show($"  {solver.Name,-25} {sw.ElapsedMilliseconds,6} мс", route);
    }
}

void Show(string title, RouteResult? route)
{
    if (route is null) { Console.WriteLine($"{title}: решения нет"); return; }

    var speeds = string.Join(", ", route.Speeds.Select(v => Math.Round(v * 3.6)));
    Console.WriteLine($"{title}: [{speeds}] км/ч, E = {route.Energy / 1000:F2} кДж, T = {route.Time:F2} с, остановок {route.Stops}");
}
