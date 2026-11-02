using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed record MonteCarloOptions(int Experiments = 1000, double PhaseShift = 3, int Seed = 1);

public sealed record MonteCarloStatistics(
    int Experiments, double PhaseShift, int Seed, int ValidExperiments, int InvalidExperiments,
    int WithStops, int OverTime, double? StopProbability, double? OverTimeProbability,
    double? MeanStops, double? MeanTime, double? Percentile95);

/// <summary>Проверка фиксированного маршрута при независимом сдвиге фаз, равномерном в [-PhaseShift, PhaseShift].</summary>
public static class MonteCarloSimulation
{
    private static GreenWaveProblem NextScenario(GreenWaveProblem problem, Random random, double shift)
        => problem with { Road = new Road(problem.Road.Segments.Select(s => s.Light is null ? s : s with
        {
            Light = s.Light with { Offset = s.Light.Offset + (2 * random.NextDouble() - 1) * shift },
        }).ToArray()) };

    /// <summary>Восстанавливает выбранный прогон из той же последовательности сдвигов, что и статистика.</summary>
    public static MonteCarloExperiment Replay(GreenWaveProblem problem, IReadOnlyList<double> speeds,
        MonteCarloOptions options, int experimentIndex, CancellationToken cancellationToken = default)
    {
        Validate(problem, speeds, options);
        if (experimentIndex < 0 || experimentIndex >= options.Experiments)
            throw new ArgumentException("Номер эксперимента вне диапазона.");
        var random = new Random(options.Seed);
        int lights = problem.Road.Segments.Count(s => s.Light is not null);
        for (int i = 0; i < experimentIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int j = 0; j < lights; j++) random.NextDouble();
        }
        var scenario = NextScenario(problem, random, options.PhaseShift);
        var model = new MotionModel(scenario);
        var state = new CarState(0, problem.InitialSpeed);
        var segments = new List<SegmentResult>();
        for (int i = 0; i < scenario.Road.Segments.Length; i++)
        {
            var result = model.Drive(scenario.Road.Segments[i], state, speeds[i]);
            if (result.LengthShortage > 0) return new(scenario, null, i + 1);
            segments.Add(result);
            state = result.ResultState;
        }
        return new(scenario, new RouteResult(segments.ToArray()), null);
    }

    private static void Validate(GreenWaveProblem problem, IReadOnlyList<double> speeds, MonteCarloOptions options)
    {
        problem.Validate();
        if (options.Experiments < 1 || options.Experiments > 100000)
            throw new ArgumentException("Количество экспериментов должно быть целым числом от 1 до 100000.");
        if (!double.IsFinite(options.PhaseShift) || options.PhaseShift < 0 || options.PhaseShift > 60)
            throw new ArgumentException("Сдвиг фаз должен быть от 0 до 60 секунд.");
        if (speeds.Count != problem.Road.Segments.Length || speeds.Any(v =>
            !double.IsFinite(v) || v < problem.Limits.MinSpeed || v > problem.Limits.MaxSpeed))
            throw new ArgumentException("Некорректные скорости исходного маршрута.");
    }

    public static MonteCarloStatistics Run(GreenWaveProblem problem, IReadOnlyList<double> speeds,
        MonteCarloOptions options, CancellationToken cancellationToken = default, List<MonteCarloSummary>? summaries = null)
    {
        Validate(problem, speeds, options);

        var random = new Random(options.Seed);
        var baseline = new bool[speeds.Count];
        var nominalModel = new MotionModel(problem);
        var nominalState = new CarState(0, problem.InitialSpeed);
        for (int i = 0; i < speeds.Count; i++)
        {
            var segment = nominalModel.Drive(problem.Road.Segments[i], nominalState, speeds[i]);
            baseline[i] = segment.Stopped;
            nominalState = segment.ResultState;
        }
        var times = new List<double>(options.Experiments);
        int invalid = 0, withStops = 0, overTime = 0, totalStops = 0;
        for (int experiment = 0; experiment < options.Experiments; experiment++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Сдвиг фиксируется на весь прогон, исходная задача не изменяется.
            var scenario = NextScenario(problem, random, options.PhaseShift);
            var segments = scenario.Road.Segments;
            var model = new MotionModel(scenario);
            var state = new CarState(0, problem.InitialSpeed);
            int stops = 0;
            bool valid = true;
            bool newStops = false;
            int? invalidSegment = null;
            for (int i = 0; i < segments.Length; i++)
            {
                var result = model.Drive(segments[i], state, speeds[i]);
                if (result.LengthShortage > 0) { valid = false; invalidSegment = i + 1; break; }
                state = result.ResultState;
                if (result.Stopped) stops++;
                if (result.Stopped && !baseline[i]) newStops = true;
            }
            summaries?.Add(new(experiment + 1, valid ? stops : null, valid ? state.Time : null,
                valid && newStops, valid && state.Time > problem.Limits.MaxTime, invalidSegment));
            if (!valid) { invalid++; continue; }
            times.Add(state.Time);
            totalStops += stops;
            if (stops > 0) withStops++;
            if (state.Time > problem.Limits.MaxTime) overTime++;
        }
        times.Sort();
        int count = times.Count;
        return new(options.Experiments, options.PhaseShift, options.Seed, count, invalid, withStops, overTime,
            count == 0 ? null : (double)withStops / count,
            count == 0 ? null : (double)overTime / count,
            count == 0 ? null : (double)totalStops / count,
            count == 0 ? null : times.Average(),
            count == 0 ? null : times[(int)Math.Ceiling(0.95 * count) - 1]);
    }
}

public sealed record MonteCarloExperiment(GreenWaveProblem Problem, RouteResult? Route, int? InvalidSegment);

public sealed record MonteCarloSummary(int Number, int? Stops, double? Time, bool NewStops, bool OverTime, int? InvalidSegment);
