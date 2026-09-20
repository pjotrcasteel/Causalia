namespace Causalia.Tool.Inspection;

internal sealed class PackageRecommender
{
    public IReadOnlyList<string> Recommend(
        IReadOnlyList<string> technologies,
        IReadOnlyList<BoundaryCandidate> boundaryCandidates)
    {
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(boundaryCandidates);

        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Causalia",
            "Causalia.Analyzers"
        };

        AddTechnologyPackages(technologies, packages);

        if (boundaryCandidates.Any(candidate => candidate.Category == "Storage")
            && !technologies.Contains("Entity Framework Core", StringComparer.OrdinalIgnoreCase))
        {
            packages.Add("Causalia.Storage");
        }

        return packages.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static void AddTechnologyPackages(IReadOnlyList<string> technologies, HashSet<string> packages)
    {
        foreach (var technology in technologies)
        {
            switch (technology)
            {
                case "ASP.NET Core":
                    packages.Add("Causalia.AspNetCore");
                    break;
                case "Entity Framework Core":
                    packages.Add("Causalia.EntityFrameworkCore");
                    break;
                case "Dapr":
                    packages.Add("Causalia.Dapr");
                    break;
                case "Kafka":
                    packages.Add("Causalia.Kafka");
                    break;
                case "RabbitMQ":
                    packages.Add("Causalia.RabbitMQ");
                    break;
                case "gRPC":
                    packages.Add("Causalia.Grpc");
                    break;
                case "Reqnroll":
                    packages.Add("Causalia.Reqnroll");
                    break;
            }
        }
    }
}
