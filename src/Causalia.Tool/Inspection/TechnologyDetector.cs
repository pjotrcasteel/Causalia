namespace Causalia.Tool.Inspection;

internal sealed class TechnologyDetector
{
    public IReadOnlyList<string> Detect(
        IReadOnlyList<ProjectSummary> projects,
        IReadOnlyList<BoundaryCandidate> boundaryCandidates)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(boundaryCandidates);

        var technologies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            DetectFromProject(project, technologies);
        }

        foreach (var boundary in boundaryCandidates)
        {
            DetectFromBoundary(boundary, technologies);
        }

        return technologies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static void DetectFromProject(ProjectSummary project, HashSet<string> technologies)
    {
        if (project.Sdk.Contains("Web", StringComparison.OrdinalIgnoreCase)
            || project.FrameworkReferences.Contains("Microsoft.AspNetCore.App", StringComparer.OrdinalIgnoreCase))
        {
            technologies.Add("ASP.NET Core");
        }

        foreach (var package in project.PackageReferences)
        {
            if (package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("Entity Framework Core");
            }
            else if (package.StartsWith("Dapr.", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("Dapr");
            }
            else if (package.Contains("Confluent.Kafka", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("Kafka");
            }
            else if (package.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("RabbitMQ");
            }
            else if (package.StartsWith("Grpc.", StringComparison.OrdinalIgnoreCase)
                     || package.StartsWith("Google.Protobuf", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("gRPC");
            }
            else if (package.StartsWith("Reqnroll", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("Reqnroll");
            }
            else if (package.StartsWith("MSTest.", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("MSTest");
            }
            else if (package.StartsWith("xunit", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("xUnit");
            }
            else if (package.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("NUnit");
            }

            if (package.StartsWith("Causalia", StringComparison.OrdinalIgnoreCase))
            {
                technologies.Add("Causalia");
            }
        }
    }

    private static void DetectFromBoundary(BoundaryCandidate boundary, HashSet<string> technologies)
    {
        switch (boundary.Category)
        {
            case "Dapr":
                technologies.Add("Dapr");
                break;
            case "Kafka":
                technologies.Add("Kafka");
                break;
            case "RabbitMQ":
                technologies.Add("RabbitMQ");
                break;
            case "gRPC":
                technologies.Add("gRPC");
                break;
            case "HTTP":
                technologies.Add("HTTP clients");
                break;
            case "Storage" when boundary.Reason.Contains("DbContext", StringComparison.Ordinal):
                technologies.Add("Entity Framework Core");
                break;
        }
    }
}
