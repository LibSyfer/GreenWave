using GreenWave.Domain;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

var test = new MotionModel(Samples.TestProblem1());
double[] testSpeeds = Enumerable.Range(0, 21).Select(k => (20 + 2 * k) / 3.6).ToArray();

Show("TestProblem1", new DynamicProgrammingSolver(testSpeeds).Solve(test));
Show("A, Tmax=120", new DynamicProgrammingSolver(testSpeeds).Solve(new MotionModel(Samples.MovingStart())));
Show("A, Tmax=110", new DynamicProgrammingSolver(testSpeeds).Solve(new MotionModel(Samples.MovingStart(110))));
Show("B, Tmax=106", new DynamicProgrammingSolver(testSpeeds).Solve(new MotionModel(Samples.StopIsBetter())));
Show("B, Tmax=90", new DynamicProgrammingSolver(testSpeeds).Solve(new MotionModel(Samples.StopIsBetter(90))));

void Show(string title, RouteResult? route)
{
    if (route is null) { Console.WriteLine($"{title}: решения нет"); return; }

    var speeds = string.Join(", ", route.Speeds.Select(v => Math.Round(v * 3.6)));
    Console.WriteLine($"{title}: [{speeds}] км/ч, E = {route.Energy / 1000:F2} кДж, T = {route.Time:F2} с, остановок {route.Stops}");
}