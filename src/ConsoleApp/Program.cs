using System.Diagnostics;
using GreenWave.Domain;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

double speedStep = 2 / 3.6;

ISolver[] solvers =
[
    new DynamicProgrammingSolver(new DpOptions(speedStep)),
    new BruteForceSolver(speedStep),
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
