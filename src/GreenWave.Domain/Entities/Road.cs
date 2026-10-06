namespace GreenWave.Domain.Entities;

public sealed record Segment(double Length, TrafficLight? Light);

public sealed record Road(Segment[] Segments);
