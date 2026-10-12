using System.Diagnostics;
using GreenWave.Domain;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Все величины в API — в СИ: метры, секунды, м/с. Перевод в км/ч — на клиенте.

app.MapGet("/api/samples", () => new SampleDto[]
{
    new("test1", "Тестовая задача 1", Samples.TestProblem1()),
    new("a120", "Старт в движении, Tmax = 120 с", Samples.MovingStart()),
    new("a110", "Старт в движении, Tmax = 110 с", Samples.MovingStart(110)),
    new("b106", "Остановка выгоднее, Tmax = 106 с", Samples.StopIsBetter()),
    new("b90", "Нет решения, Tmax = 90 с", Samples.StopIsBetter(90)),
});

app.MapPost("/api/solve", (SolveRequest request) =>
{
    try
    {
        var model = new MotionModel(request.Problem);
        double speedStep = request.SpeedStepKmh / 3.6;
        ISolver solver = request.Solver switch
        {
            "dp" => new DynamicProgrammingSolver(new DpOptions(speedStep)),
            "ga" => new GeneticSolver(new GaOptions(speedStep)),
            "bruteforce" => CheckBruteForceSize(request.Problem, speedStep),
            _ => throw new ArgumentException($"Неизвестный решатель: {request.Solver}"),
        };

        var sw = Stopwatch.StartNew();
        var route = solver.Solve(model);
        sw.Stop();

        var trajectory = route is null ? null : Trajectory.Build(request.Problem, route.Segments);
        return Results.Ok(new SolveResponse(solver.Name, route, trajectory, sw.Elapsed.TotalMilliseconds));
    }
    catch (ArgumentException e)
    {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.Run();

// Полный перебор растёт как (число скоростей)^(число сегментов) — ограничиваем, чтобы не повесить сервер.
static BruteForceSolver CheckBruteForceSize(GreenWaveProblem problem, double speedStep)
{
    const double maxRoutes = 5e6;
    double routes = Math.Pow(problem.Limits.SpeedGrid(speedStep).Length, problem.Road.Segments.Length);

    if (routes > maxRoutes)
        throw new ArgumentException($"Полный перебор: {routes:E1} маршрутов, допустимо не больше {maxRoutes:E0}. Увеличьте шаг скорости или используйте ДП.");

    return new BruteForceSolver(speedStep);
}

record SampleDto(string Id, string Title, GreenWaveProblem Problem);

/// <param name="Solver">"dp", "ga" или "bruteforce".</param>
/// <param name="SpeedStepKmh">Шаг сетки скоростей, км/ч.</param>
record SolveRequest(GreenWaveProblem Problem, string Solver, double SpeedStepKmh);

record SolveResponse(string Solver, RouteResult? Route, IReadOnlyList<TrajectoryPoint>? Trajectory, double ElapsedMs);
