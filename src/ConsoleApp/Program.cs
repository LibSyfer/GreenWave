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
    new GeneticSolver(new GaOptions(speedStep)),
];

Compare("TestProblem1", Samples.TestProblem1());
Compare("A, Tmax=120", Samples.MovingStart());
Compare("A, Tmax=110", Samples.MovingStart(110));
Compare("B, Tmax=106", Samples.StopIsBetter());
Compare("B, Tmax=90", Samples.StopIsBetter(90));

Console.WriteLine();
ShowTrace("TestProblem1", Samples.TestProblem1());
ShowTrace("B, Tmax=106", Samples.StopIsBetter());
ShowTrace("B, Tmax=90", Samples.StopIsBetter(90));

ShowGaTrace("TestProblem1", Samples.TestProblem1());
ShowGaTrace("B, Tmax=106", Samples.StopIsBetter());

void ShowGaTrace(string title, GreenWaveProblem problem)
{
    var (route, trace) = new GeneticSolver(new GaOptions(speedStep)).SolveTraced(new MotionModel(problem));

    Console.WriteLine($"Трасса ГА: {title} — поколений {trace.Generations.Count}, особей в поколении {trace.Generations[0].Population.Count}");
    Console.WriteLine($"  {"поколение",-10}{"лучшая E, кДж",15}{"средняя E*",12}{"допустимых",12}{"скрещ.",8}{"копий",8}{"мутир.",8}");

    foreach (var generation in trace.Generations.Where(g => g.Index % 10 == 0 || g.Index < 5))
    {
        var population = generation.Population;
        var best = population.Single(x => x.Id == generation.BestId);
        var feasible = population.Where(x => x.Feasible).ToArray();
        string avg = feasible.Length > 0 ? $"{feasible.Average(x => x.Energy) / 1000:F2}" : "—";
        string bestEnergy = best.Feasible ? $"{best.Energy / 1000:F2}" : "недопуст.";

        Console.WriteLine($"  {generation.Index,-10}{bestEnergy,15}{avg,12}{feasible.Length,12}"
            + $"{population.Count(x => x.Origin.Kind == GaOriginKind.Crossover),8}"
            + $"{population.Count(x => x.Origin.Kind == GaOriginKind.Clone),8}"
            + $"{population.Count(x => x.Origin.Mutations.Count > 0),8}");
    }

    Console.WriteLine($"  * средняя — по допустимым особям; лучшая особь id {trace.BestId}{(route is null ? " — решения нет" : "")}");
    Console.WriteLine();
}

void ShowTrace(string title, GreenWaveProblem problem)
{
    var (route, trace) = new DynamicProgrammingSolver(new DpOptions(speedStep)).SolveTraced(new MotionModel(problem));
    var statuses = Enum.GetValues<DpCandidateStatus>();

    Console.WriteLine($"Трасса ДП: {title} — скоростей в сетке {trace.SpeedGrid.Count}, ячейка времени {trace.TimeStep} с");
    Console.WriteLine($"  {"сегмент",-8}{"всего",8}" + string.Concat(statuses.Select(s => $"{s,20}")));

    foreach (var layer in trace.Layers)
    {
        var counts = statuses.Select(s => layer.Candidates.Count(c => c.Status == s));
        Console.WriteLine($"  {layer.SegmentIndex + 1,-8}{layer.Candidates.Count,8}" + string.Concat(counts.Select(n => $"{n,20}")));
    }

    Console.WriteLine($"  лучший путь (id): [{string.Join(", ", trace.BestPath)}]{(route is null ? " — решения нет" : "")}");
    Console.WriteLine();
}

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
