using System.Collections.Immutable;

namespace OptiSync.Shared.Domain;

public sealed record TspInstance(string Name, string Comment, ImmutableArray<CityPoint> Cities)
{
    public int Dimension => Cities.Length;

    public double[] DistanceMatrix { get; } = BuildDistanceMatrix(Cities);

    public double Distance(int from, int to) => DistanceMatrix[from * Cities.Length + to];

    public double TourCost(ReadOnlySpan<int> tour)
    {
        if (tour.Length == 0)
        {
            return 0;
        }

        int n = Cities.Length;
        double cost = 0;
        for (int i = 0; i < tour.Length - 1; i++)
        {
            cost += DistanceMatrix[tour[i] * n + tour[i + 1]];
        }

        return cost + DistanceMatrix[tour[^1] * n + tour[0]];
    }

    public static double EuclideanDistance(CityPoint a, CityPoint b) =>
        Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static double[] BuildDistanceMatrix(ImmutableArray<CityPoint> cities)
    {
        int n = cities.Length;
        var matrix = new double[n * n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double distance = EuclideanDistance(cities[i], cities[j]);
                matrix[i * n + j] = distance;
                matrix[j * n + i] = distance;
            }
        }

        return matrix;
    }
}
