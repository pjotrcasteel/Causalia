using System.Text.RegularExpressions;

namespace Causalia.Tool.Inspection;

internal sealed partial class SourceInspector
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".causalia",
        ".git",
        ".vs",
        "artifacts",
        "bin",
        "obj",
        "TestResults"
    };

    private static readonly IReadOnlyList<FindingDefinition> MarkerFindings = new List<FindingDefinition>
    {
        new()
        {
            Marker = "Thread.Sleep(",
            Code = "INSPECT003",
            Description = "Thread.Sleep blocks real wall-clock time.",
            Recommendation = "Replace blocking sleep with an injected TimeProvider-aware delay.",
            AnalyzerRuleId = "CAU1001"
        },
        new()
        {
            Marker = "Random.Shared",
            Code = "INSPECT004",
            Description = "Random.Shared introduces nondeterministic values.",
            Recommendation = "Use simulation-scoped deterministic randomness for behavior that affects the scenario.",
            AnalyzerRuleId = "CAU1002"
        },
        new()
        {
            Marker = "new Random(",
            Code = "INSPECT004",
            Description = "A locally created Random instance may make replay nondeterministic.",
            Recommendation = "Inject or use simulation-scoped deterministic randomness where the value affects behavior.",
            AnalyzerRuleId = "CAU1002"
        },
        new()
        {
            Marker = "Guid.NewGuid(",
            Code = "INSPECT005",
            Description = "Guid.NewGuid creates nondeterministic identifiers.",
            Recommendation = "Inject identifier generation when the identifier influences simulation behavior or assertions.",
            AnalyzerRuleId = "CAU1002"
        },
        new()
        {
            Marker = "Task.Run(",
            Code = "INSPECT006",
            Description = "Task.Run may move work outside deterministic scheduling.",
            Recommendation = "Review whether the work should run through a Causalia-controlled operation instead.",
            AnalyzerRuleId = "CAU1003"
        },
        new()
        {
            Marker = "CancellationToken.None",
            Code = "INSPECT007",
            Description = "CancellationToken.None breaks cancellation propagation at this call site.",
            Recommendation = "Pass the operation or simulation cancellation token through the dependency boundary.",
            AnalyzerRuleId = "CAU1007"
        }
    }.AsReadOnly();

    private static readonly IReadOnlyList<BoundaryDefinition> TokenBoundaries = new List<BoundaryDefinition>
    {
        new()
        {
            Category = "Dapr",
            Score = 100,
            Reason = "DaprClient is an external messaging/service invocation boundary.",
            SuggestedScenario = "Simulate redelivery or a lost acknowledgement and verify idempotent handling.",
            Tokens = new List<string> { "DaprClient" }.AsReadOnly()
        },
        new()
        {
            Category = "Kafka",
            Score = 100,
            Reason = "Kafka producer/consumer usage is a distributed messaging boundary.",
            SuggestedScenario = "Simulate duplicate delivery or acknowledgement loss around offset processing.",
            Tokens = new List<string> { "Confluent.Kafka" }.AsReadOnly()
        },
        new()
        {
            Category = "RabbitMQ",
            Score = 100,
            Reason = "RabbitMQ usage is a distributed messaging boundary.",
            SuggestedScenario = "Simulate redelivery or publisher confirmation loss and verify side effects remain correct.",
            Tokens = new List<string> { "RabbitMQ.Client" }.AsReadOnly()
        },
        new()
        {
            Category = "gRPC",
            Score = 90,
            Reason = "gRPC usage is a remote-call boundary.",
            SuggestedScenario = "Simulate a timeout after the remote operation may already have completed.",
            Tokens = new List<string> { "GrpcChannel", "AsyncUnaryCall<", "ServerCallContext" }.AsReadOnly()
        },
        new()
        {
            Category = "Storage",
            Score = 95,
            Reason = "DbContext marks a durable storage transaction boundary.",
            SuggestedScenario = "Simulate commit success followed by acknowledgement loss and verify retry does not duplicate effects.",
            Tokens = new List<string> { "DbContext" }.AsReadOnly()
        },
        new()
        {
            Category = "HTTP",
            Score = 85,
            Reason = "HttpClient marks an outbound remote-call boundary.",
            SuggestedScenario = "Simulate a timeout after the remote side effect and verify retry safety.",
            Tokens = new List<string> { "HttpClient" }.AsReadOnly()
        },
        new()
        {
            Category = "Background processing",
            Score = 80,
            Reason = "BackgroundService is a long-running lifecycle boundary.",
            SuggestedScenario = "Simulate cancellation or restart while work is in progress and verify safe recovery.",
            Tokens = new List<string> { "BackgroundService" }.AsReadOnly()
        }
    }.AsReadOnly();

    public async Task<SourceInspectionResult> InspectAsync(
        string rootDirectory,
        IReadOnlyList<ProjectSummary> projects,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(projects);

        var projectsToScan = SelectProjectsToScan(projects);
        var sourceFiles = CollectSourceFiles(projectsToScan, cancellationToken);
        var findings = new List<SourceFinding>();
        var boundaries = new List<BoundaryCandidate>();
        var usesTimeProvider = false;
        var usesCancellationToken = false;
        var usesRetryMechanisms = false;

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(sourceFile, cancellationToken);
            var lines = SplitLines(content);
            var relativePath = ToRelativePath(rootDirectory, sourceFile);
            usesTimeProvider |= content.Contains("TimeProvider", StringComparison.Ordinal);
            usesCancellationToken |= content.Contains("CancellationToken", StringComparison.Ordinal);
            usesRetryMechanisms |= ContainsRetryMechanism(content);
            AddFindings(lines, relativePath, findings);
            AddBoundaryCandidates(lines, content, relativePath, boundaries);
        }

        var orderedFindings = findings
            .OrderBy(finding => finding.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Line)
            .ThenBy(finding => finding.Code, StringComparer.Ordinal)
            .ToList();

        var orderedBoundaries = boundaries
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Line)
            .ToList();

        return new SourceInspectionResult
        {
            Findings = orderedFindings.AsReadOnly(),
            BoundaryCandidates = orderedBoundaries.AsReadOnly(),
            AdoptionCapabilities = new AdoptionCapabilities
            {
                UsesTimeProvider = usesTimeProvider,
                UsesCancellationToken = usesCancellationToken,
                UsesRetryMechanisms = usesRetryMechanisms
            },
            SourceFilesScanned = sourceFiles.Count
        };
    }

    private static IReadOnlyList<ProjectSummary> SelectProjectsToScan(IReadOnlyList<ProjectSummary> projects)
    {
        var productionProjects = projects.Where(project => !project.IsTestProject).ToList();
        return productionProjects.Count > 0 ? productionProjects.AsReadOnly() : projects;
    }

    private static IReadOnlyList<string> CollectSourceFiles(
        IReadOnlyList<ProjectSummary> projects,
        CancellationToken cancellationToken)
    {
        var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            foreach (var sourceFile in Directory.EnumerateFiles(project.ProjectDirectory, "*.cs", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsExcluded(sourceFile))
                {
                    sourceFiles.Add(Path.GetFullPath(sourceFile));
                }
            }
        }

        return sourceFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static void AddFindings(
        IReadOnlyList<string> lines,
        string relativePath,
        ICollection<SourceFinding> findings)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            AddClockFinding(line, index, relativePath, findings);

            foreach (var definition in MarkerFindings)
            {
                AddMarkerFinding(line, index, relativePath, definition, findings);
            }

            if (line.Contains("Task.Delay(", StringComparison.Ordinal) && !HasTimeProviderNearby(lines, index))
            {
                findings.Add(
                    CreateFinding(
                        "INSPECT002",
                        "Task.Delay may be using wall-clock time.",
                        relativePath,
                        index,
                        line,
                        "Use the TimeProvider-aware Task.Delay overload and pass the simulation cancellation token.",
                        "CAU1001"));
            }
        }
    }

    private static void AddClockFinding(
        string line,
        int index,
        string relativePath,
        ICollection<SourceFinding> findings)
    {
        var marker = FindFirstMarker(
            line,
            new List<string>
            {
                "DateTime.UtcNow",
                "DateTime.Now",
                "DateTimeOffset.UtcNow",
                "DateTimeOffset.Now"
            });

        if (marker is null)
        {
            return;
        }

        findings.Add(
            CreateFinding(
                "INSPECT001",
                $"{marker} reads the system clock directly.",
                relativePath,
                index,
                line,
                "Inject TimeProvider and use GetUtcNow() or GetLocalNow() so virtual time can control the scenario.",
                "CAU1001"));
    }

    private static void AddMarkerFinding(
        string line,
        int index,
        string relativePath,
        FindingDefinition definition,
        ICollection<SourceFinding> findings)
    {
        if (!line.Contains(definition.Marker, StringComparison.Ordinal))
        {
            return;
        }

        findings.Add(
            CreateFinding(
                definition.Code,
                definition.Description,
                relativePath,
                index,
                line,
                definition.Recommendation,
                definition.AnalyzerRuleId));
    }

    private static SourceFinding CreateFinding(
        string code,
        string description,
        string relativePath,
        int zeroBasedLine,
        string line,
        string recommendation,
        string? analyzerRuleId)
    {
        return new SourceFinding
        {
            Code = code,
            Description = description,
            FilePath = relativePath,
            Line = zeroBasedLine + 1,
            Snippet = line.Trim(),
            Recommendation = recommendation,
            AnalyzerRuleId = analyzerRuleId
        };
    }

    private static void AddBoundaryCandidates(
        IReadOnlyList<string> lines,
        string content,
        string relativePath,
        ICollection<BoundaryCandidate> boundaries)
    {
        foreach (var definition in TokenBoundaries)
        {
            AddTokenBoundary(lines, relativePath, definition, boundaries);
        }

        AddTypeNameBoundaries(content, lines, relativePath, boundaries);
    }

    private static void AddTypeNameBoundaries(
        string content,
        IReadOnlyList<string> lines,
        string relativePath,
        ICollection<BoundaryCandidate> boundaries)
    {
        foreach (Match match in TypeNameRegex().Matches(content))
        {
            var typeName = match.Groups["name"].Value;
            var definition = GetTypeBoundaryDefinition(typeName);

            if (definition is not null)
            {
                AddNamedBoundary(content, lines, relativePath, typeName, definition, boundaries);
            }
        }
    }

    private static BoundaryDefinition? GetTypeBoundaryDefinition(string typeName)
    {
        if (typeName.EndsWith("Handler", StringComparison.Ordinal))
        {
            return new BoundaryDefinition
            {
                Category = "Handler",
                Score = 75,
                Reason = "Handler-shaped application code is a useful entry point for retry, duplicate, and concurrency scenarios.",
                SuggestedScenario = "Invoke the handler twice or concurrently and verify the domain outcome is idempotent.",
                Tokens = new List<string>().AsReadOnly()
            };
        }

        if (typeName.EndsWith("Repository", StringComparison.Ordinal)
            || typeName.EndsWith("Store", StringComparison.Ordinal))
        {
            return new BoundaryDefinition
            {
                Category = "Storage",
                Score = 88,
                Reason = "Repository/store code is a natural seam between application behavior and durable state.",
                SuggestedScenario = "Inject an ambiguous storage outcome and verify persisted state before retrying.",
                Tokens = new List<string>().AsReadOnly()
            };
        }

        if (typeName.EndsWith("Controller", StringComparison.Ordinal))
        {
            return new BoundaryDefinition
            {
                Category = "HTTP entry point",
                Score = 60,
                Reason = "Controller code is an externally observable application entry point.",
                SuggestedScenario = "Drive one request through the real application service while simulating one dependency failure.",
                Tokens = new List<string>().AsReadOnly()
            };
        }

        return null;
    }

    private static void AddTokenBoundary(
        IReadOnlyList<string> lines,
        string relativePath,
        BoundaryDefinition definition,
        ICollection<BoundaryCandidate> boundaries)
    {
        var lineIndex = definition.Tokens
            .Select(token => FindLine(lines, token))
            .Where(value => value >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        if (lineIndex < 0)
        {
            return;
        }

        boundaries.Add(
            CreateBoundary(
                relativePath,
                Path.GetFileNameWithoutExtension(relativePath),
                lineIndex,
                definition));
    }

    private static void AddNamedBoundary(
        string content,
        IReadOnlyList<string> lines,
        string relativePath,
        string name,
        BoundaryDefinition definition,
        ICollection<BoundaryCandidate> boundaries)
    {
        var characterIndex = content.IndexOf(name, StringComparison.Ordinal);
        var lineIndex = characterIndex < 0 ? 0 : content[..characterIndex].Count(character => character == '\n');

        if (lineIndex >= lines.Count)
        {
            lineIndex = Math.Max(0, lines.Count - 1);
        }

        boundaries.Add(CreateBoundary(relativePath, name, lineIndex, definition));
    }

    private static BoundaryCandidate CreateBoundary(
        string relativePath,
        string name,
        int zeroBasedLine,
        BoundaryDefinition definition)
    {
        return new BoundaryCandidate
        {
            Category = definition.Category,
            Name = name,
            FilePath = relativePath,
            Line = zeroBasedLine + 1,
            Reason = definition.Reason,
            Score = definition.Score,
            SuggestedScenario = definition.SuggestedScenario
        };
    }

    private static bool ContainsRetryMechanism(string content)
    {
        return content.Contains("Retry", StringComparison.Ordinal)
            || content.Contains("retry", StringComparison.Ordinal)
            || content.Contains("WaitAndRetry", StringComparison.Ordinal)
            || content.Contains("ResiliencePipeline", StringComparison.Ordinal)
            || content.Contains("AddStandardResilienceHandler", StringComparison.Ordinal);
    }

    private static int FindLine(IReadOnlyList<string> lines, string token)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Contains(token, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasTimeProviderNearby(IReadOnlyList<string> lines, int index)
    {
        var end = Math.Min(lines.Count - 1, index + 3);

        for (var current = index; current <= end; current++)
        {
            if (lines[current].Contains("TimeProvider", StringComparison.Ordinal)
                || lines[current].Contains("timeProvider", StringComparison.Ordinal)
                || lines[current].Contains(".TimeProvider", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? FindFirstMarker(string line, IReadOnlyList<string> markers)
    {
        return markers.FirstOrDefault(marker => line.Contains(marker, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }

    private static bool IsExcluded(string path)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(ExcludedDirectories.Contains);
    }

    private static string ToRelativePath(string rootDirectory, string path)
    {
        return Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');
    }

    [GeneratedRegex(@"\b(?:class|record|struct|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeNameRegex();
}
