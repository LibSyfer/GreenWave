using System.Diagnostics;
using GreenWave.Api;
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
    new("dp-ga", "ДП и ГА — разные маршруты и устойчивость", Samples.DpGaComparison(), new(2, 1000, 10)),
    new("dp-ga-visible", "ДП и ГА — заметное расхождение на графике", Samples.DpGaVisibleDifference(), new(2, 1000, 10)),
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

app.MapPost("/api/solve/dp/trace", (DpTraceRequest request) =>
{
    try
    {
        var model = new MotionModel(request.Problem);
        var solver = new DynamicProgrammingSolver(new DpOptions(request.SpeedStepKmh / 3.6, request.TimeStep));

        var sw = Stopwatch.StartNew();
        var (route, trace) = solver.SolveTraced(model);
        sw.Stop();

        var trajectory = route is null ? null : Trajectory.Build(request.Problem, route.Segments);
        return Results.Ok(new DpTraceResponse(route, trajectory, sw.Elapsed.TotalMilliseconds, DpTraceDto.From(trace)));
    }
    catch (ArgumentException e)
    {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/solve/ga/trace", (GaTraceRequest request) =>
{
    try
    {
        var model = new MotionModel(request.Problem);
        var solver = new GeneticSolver(new GaOptions(request.SpeedStepKmh / 3.6));
        var sw = Stopwatch.StartNew();
        var (route, trace) = solver.SolveTraced(model);
        sw.Stop();
        var trajectory = route is null ? null : Trajectory.Build(request.Problem, route.Segments);
        // Для разбора наследования нужны гены и происхождение, без повторения сегментов каждой особи.
        return Results.Ok(new
        {
            route, trajectory, elapsedMs = sw.Elapsed.TotalMilliseconds,
            gaTrace = new
            {
                trace.SpeedGrid, trace.BestId,
                Generations = trace.Generations.Select(g => new
                {
                    g.Index, g.BestId,
                    Population = g.Population.Select(x => new
                    {
                        x.Id, x.Genes, x.Energy, x.Time, x.Shortage, x.Overrun, x.Feasible,
                        Origin = new
                        {
                            Kind = x.Origin.Kind.ToString(), x.Origin.Parent1Id, x.Origin.Parent2Id,
                            x.Origin.Selection1, x.Origin.Selection2, x.Origin.CrossoverPoint, x.Origin.Mutations,
                        },
                    }),
                }),
            },
        });
    }
    catch (ArgumentException e)
    {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

// Пересчитываем только выбранную хромосому, без повторного запуска ГА.
app.MapPost("/api/trajectory", (TrajectoryRequest request) =>
{
    try
    {
        var model = new MotionModel(request.Problem);
        if (request.Speeds is null || request.Speeds.Length != request.Problem.Road.Segments.Length)
            throw new ArgumentException("Число скоростей должно совпадать с числом участков.");
        var state = new CarState(0, request.Problem.InitialSpeed);
        var segments = new List<SegmentResult>();
        for (int i = 0; i < request.Speeds.Length; i++)
        {
            double speed = request.Speeds[i];
            if (!double.IsFinite(speed) || speed < request.Problem.Limits.MinSpeed || speed > request.Problem.Limits.MaxSpeed)
                throw new ArgumentException("Скорость вне допустимого диапазона.");
            var result = model.Drive(request.Problem.Road.Segments[i], state, speed);
            // Нельзя строить физическую траекторию при нехватке пути на разгон/торможение.
            if (result.LengthShortage > 0)
                return Results.Ok(new { trajectory = (IReadOnlyList<TrajectoryPoint>?)null });
            segments.Add(result);
            state = result.ResultState;
        }
        return Results.Ok(new { trajectory = Trajectory.Build(request.Problem, segments) });
    }
    catch (ArgumentException e)
    {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/solve/monte-carlo", (MonteCarloRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        var model = new MotionModel(request.Problem);
        if (request.Experiments < 1 || request.Experiments > 100000)
            throw new ArgumentException("Количество экспериментов должно быть от 1 до 100000.");
        if (!double.IsFinite(request.PhaseShift) || request.PhaseShift < 0 || request.PhaseShift > 60)
            throw new ArgumentException("Сдвиг фаз должен быть от 0 до 60 секунд.");
        double step = request.SpeedStepKmh / 3.6;
        if (!double.IsFinite(step) || step <= 0 ||
            (request.Problem.Limits.MaxSpeed - request.Problem.Limits.MinSpeed) / step > 100 ||
            request.Problem.Road.Segments.Length > 30)
            throw new ArgumentException("Для Монте-Карло допустимо до 30 участков и примерно 100 шагов сетки скоростей.");
        var sw = Stopwatch.StartNew();
        var options = new MonteCarloOptions(request.Experiments, request.PhaseShift);
        var routes = new (string Id, string Title, RouteResult? Route)[]
        {
            ("dp", "Динамическое программирование", new DynamicProgrammingSolver(new DpOptions(step)).Solve(model)),
            ("ga", "Генетический алгоритм", new GeneticSolver(new GaOptions(step)).Solve(model)),
        };
        var comparisons = routes.Select(x =>
        {
            var experiments = new List<MonteCarloSummary>();
            var statistics = x.Route is null ? null : MonteCarloSimulation.Run(
                request.Problem, x.Route.Speeds, options, cancellationToken, experiments);
            return new {
            solver = x.Id, title = x.Title, route = x.Route,
            trajectory = x.Route is null ? null : Trajectory.Build(request.Problem, x.Route.Segments),
            // Один seed создаёт одинаковые наборы сдвигов для честного сравнения маршрутов.
            statistics, experiments,
            };
        }).ToArray();
        var displayed = routes.FirstOrDefault(x => x.Route is not null);
        var route = displayed.Route;
        sw.Stop();
        return Results.Ok(new
        {
            solver = "MonteCarlo", route,
            trajectory = route is null ? null : Trajectory.Build(request.Problem, route.Segments),
            elapsedMs = sw.Elapsed.TotalMilliseconds,
            monteCarlo = new { experiments = request.Experiments, phaseShift = request.PhaseShift, seed = options.Seed,
                displayedSolver = route is null ? null : displayed.Id, comparisons },
        });
    }
    catch (ArgumentException e)
    {
        return Results.Problem(e.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/solve/monte-carlo/experiment", (MonteCarloExperimentRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        if (request.Speeds is null || request.Problem.Road.Segments.Length > 30)
            throw new ArgumentException("Некорректные параметры эксперимента.");
        var replay = MonteCarloSimulation.Replay(request.Problem, request.Speeds,
            new MonteCarloOptions(request.Experiments, request.PhaseShift, request.Seed), request.Experiment - 1, cancellationToken);
        return Results.Ok(new { replay.Problem, replay.Route, replay.InvalidSegment,
            trajectory = replay.Route is null ? null : Trajectory.Build(replay.Problem, replay.Route.Segments) });
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

record SampleDto(string Id, string Title, GreenWaveProblem Problem, SampleSettings? Settings = null);
record SampleSettings(double SpeedStepKmh, int Experiments, double PhaseShift);

/// <param name="Solver">"dp", "ga" или "bruteforce".</param>
/// <param name="SpeedStepKmh">Шаг сетки скоростей, км/ч.</param>
record SolveRequest(GreenWaveProblem Problem, string Solver, double SpeedStepKmh);

record SolveResponse(string Solver, RouteResult? Route, IReadOnlyList<TrajectoryPoint>? Trajectory, double ElapsedMs);

/// <param name="TimeStep">Ширина ячейки времени ДП, с.</param>
record DpTraceRequest(GreenWaveProblem Problem, double SpeedStepKmh, double TimeStep = 0.1);

record DpTraceResponse(RouteResult? Route, IReadOnlyList<TrajectoryPoint>? Trajectory, double ElapsedMs, DpTraceDto Trace);

record GaTraceRequest(GreenWaveProblem Problem, double SpeedStepKmh);

record TrajectoryRequest(GreenWaveProblem Problem, double[] Speeds);

record MonteCarloRequest(GreenWaveProblem Problem, double SpeedStepKmh, int Experiments = 1000, double PhaseShift = 3);

record MonteCarloExperimentRequest(GreenWaveProblem Problem, double[] Speeds, int Experiments, double PhaseShift, int Seed, int Experiment);
