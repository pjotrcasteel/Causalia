namespace Causalia.Visualization;

internal static class TraceEventClassifier
{
    public static TraceClassification Classify(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var parts = message.Split(':');
        var category = ClassifyCategory(parts);
        var severity = ClassifySeverity(category, parts);
        var lane = ClassifyLane(category, parts);
        var correlationId = ClassifyCorrelationId(category, parts);
        return new TraceClassification(category, severity, lane, correlationId);
    }

    private static TraceEventCategory ClassifyCategory(IReadOnlyList<string> parts)
    {
        return parts[0] switch
        {
            "scheduler" => TraceEventCategory.Scheduler,
            "node" => TraceEventCategory.Node,
            "process" => TraceEventCategory.Process,
            "messaging" => TraceEventCategory.Messaging,
            "aspnetcore" => TraceEventCategory.AspNetCore,
            "http-network" => TraceEventCategory.Network,
            "storage" => TraceEventCategory.Storage,
            "fault" => TraceEventCategory.Fault,
            "invariant" => TraceEventCategory.Invariant,
            "linearizability" => TraceEventCategory.Linearizability,
            "consistency" => TraceEventCategory.Consistency,
            "model" => TraceEventCategory.ModelBased,
            "load" => TraceEventCategory.Load,
            "dapr" => TraceEventCategory.Dapr,
            "kafka" => TraceEventCategory.Kafka,
            "rabbitmq" => TraceEventCategory.RabbitMQ,
            "grpc" => TraceEventCategory.Grpc,
            "reality" => TraceEventCategory.ProductionReality,
            _ => TraceEventCategory.User
        };
    }

    private static TraceEventSeverity ClassifySeverity(TraceEventCategory category, IReadOnlyList<string> parts)
    {
        if (category == TraceEventCategory.User)
        {
            return TraceEventSeverity.Information;
        }

        var statusParts = GetStatusParts(category, parts);

        if (ContainsAnyStatus(
                statusParts,
                "failed",
                "violated",
                "interrupted",
                "dropped",
                "partitioned",
                "unavailable",
                "crashed"))
        {
            return TraceEventSeverity.Error;
        }

        if (category == TraceEventCategory.Fault ||
            ContainsAnyStatus(statusParts, "fault", "delayed", "duplicated", "deadline", "cancelled"))
        {
            return TraceEventSeverity.Warning;
        }

        if (ContainsAnyStatus(statusParts, "completed", "satisfied", "restarted", "healed"))
        {
            return TraceEventSeverity.Success;
        }

        return TraceEventSeverity.Information;
    }

    private static IReadOnlyList<string> GetStatusParts(TraceEventCategory category, IReadOnlyList<string> parts)
    {
        return category switch
        {
            TraceEventCategory.Node => PartAt(parts, 1),
            TraceEventCategory.Process when ElementAtOrDefault(parts, 1) == "generation" => PartAt(parts, 2),
            TraceEventCategory.Process => PartAt(parts, 1),
            TraceEventCategory.Messaging => PartAt(parts, 1),
            TraceEventCategory.AspNetCore => PartAt(parts, 2),
            TraceEventCategory.Network => PartAt(parts, 2),
            TraceEventCategory.Storage when ElementAtOrDefault(parts, 2) == "operation" => PartAt(parts, 4),
            TraceEventCategory.Storage => PartAt(parts, 2),
            TraceEventCategory.Invariant => PartAt(parts, 1),
            TraceEventCategory.Consistency when ElementAtOrDefault(parts, 2) == "convergence" => PartsAt(parts, 2, 3),
            TraceEventCategory.Consistency => PartAt(parts, 2),
            TraceEventCategory.ModelBased => PartAt(parts, 4),
            TraceEventCategory.Load => PartAt(parts, 2),
            TraceEventCategory.Dapr => PartAt(parts, 2),
            TraceEventCategory.Kafka => PartAt(parts, 2),
            TraceEventCategory.RabbitMQ when ElementAtOrDefault(parts, 1) == "publish" => PartAt(parts, 2),
            TraceEventCategory.RabbitMQ => PartAt(parts, 1),
            TraceEventCategory.Grpc => PartAt(parts, 2),
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyList<string> PartAt(IReadOnlyList<string> parts, int index)
    {
        return index < parts.Count ? [parts[index]] : Array.Empty<string>();
    }

    private static IReadOnlyList<string> PartsAt(IReadOnlyList<string> parts, int firstIndex, int secondIndex)
    {
        return secondIndex < parts.Count ? [parts[firstIndex], parts[secondIndex]] : PartAt(parts, firstIndex);
    }

    private static string? ElementAtOrDefault(IReadOnlyList<string> parts, int index)
    {
        return index < parts.Count ? parts[index] : null;
    }

    private static string ClassifyLane(TraceEventCategory category, IReadOnlyList<string> parts)
    {
        return category switch
        {
            TraceEventCategory.Node when parts.Count > 2 => $"node/{parts[2]}",
            TraceEventCategory.Process when parts.Count > 2 => $"process/{parts[2]}",
            TraceEventCategory.Messaging when parts.Count > 4 => $"endpoint/{parts[4]}",
            TraceEventCategory.AspNetCore when parts.Count > 3 => $"service/{parts[3]}",
            TraceEventCategory.Network => ClassifyNetworkLane(parts),
            TraceEventCategory.Storage when parts.Count > 1 => $"storage/{parts[1]}",
            TraceEventCategory.Linearizability when parts.Count > 1 => $"history/{parts[1]}",
            TraceEventCategory.Consistency when parts.Count > 1 => $"consistency/{parts[1]}",
            TraceEventCategory.ModelBased when parts.Count > 1 => $"model/{parts[1]}",
            TraceEventCategory.Load => ClassifyLoadLane(parts),
            TraceEventCategory.Dapr => ClassifyDaprLane(parts),
            TraceEventCategory.Kafka => ClassifyKafkaLane(parts),
            TraceEventCategory.RabbitMQ => ClassifyRabbitLane(parts),
            TraceEventCategory.Grpc => ClassifyGrpcLane(parts),
            TraceEventCategory.ProductionReality => "production-reality",
            TraceEventCategory.Fault => "faults",
            TraceEventCategory.Invariant => "invariants",
            TraceEventCategory.Scheduler => "scheduler",
            _ => "scenario"
        };
    }

    private static string? ClassifyCorrelationId(TraceEventCategory category, IReadOnlyList<string> parts)
    {
        return category switch
        {
            TraceEventCategory.Messaging => FindMessagingCorrelationId(parts),
            TraceEventCategory.Network => FindNetworkRequestId(parts),
            TraceEventCategory.Storage => FindStorageOperationId(parts),
            TraceEventCategory.Linearizability when parts.Count > 3 => $"linearizability:{parts[1]}:{parts[3]}",
            TraceEventCategory.Consistency => FindConsistencyCorrelationId(parts),
            TraceEventCategory.ModelBased => FindModelCorrelationId(parts),
            TraceEventCategory.Fault => FindFaultCorrelationId(parts),
            TraceEventCategory.Load => FindLoadCorrelationId(parts),
            TraceEventCategory.Dapr => FindDaprCorrelationId(parts),
            TraceEventCategory.Kafka => FindKafkaCorrelationId(parts),
            TraceEventCategory.RabbitMQ => FindRabbitCorrelationId(parts),
            TraceEventCategory.Grpc => FindGrpcCorrelationId(parts),
            TraceEventCategory.ProductionReality when parts.Count > 3 => $"reality:{parts[3]}",
            _ => null
        };
    }

    private static string ClassifyDaprLane(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 ? $"dapr/{parts[3]}" : "dapr";
    }

    private static string ClassifyKafkaLane(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 ? $"kafka/{parts[3]}" : "kafka";
    }

    private static string ClassifyRabbitLane(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 ? $"rabbitmq/{parts[3]}" : "rabbitmq";
    }

    private static string ClassifyGrpcLane(IReadOnlyList<string> parts)
    {
        if (parts.Count > 4 && parts[1] == "call")
        {
            var route = parts.FirstOrDefault(part => part.Contains("->", StringComparison.Ordinal));
            return route is null ? "grpc" : $"grpc/{route}";
        }

        return "grpc";
    }

    private static string? FindDaprCorrelationId(IReadOnlyList<string> parts)
    {
        if (parts.Count < 3 || parts[1] != "pubsub")
        {
            return null;
        }

        foreach (var part in parts.Skip(4))
        {
            if (long.TryParse(part, out var id))
            {
                return $"dapr:{id}";
            }
        }

        return null;
    }

    private static string? FindKafkaCorrelationId(IReadOnlyList<string> parts)
    {
        if (parts.Count >= 8 && parts[1] == "record" && long.TryParse(parts[^1], out var offset))
        {
            return $"kafka:{parts[^3]}:{parts[^2]}:{offset}";
        }

        return null;
    }

    private static string? FindRabbitCorrelationId(IReadOnlyList<string> parts)
    {
        if (parts.Count < 5)
        {
            return null;
        }

        foreach (var part in parts.Skip(4))
        {
            if (long.TryParse(part, out var id))
            {
                return $"rabbitmq:{id}";
            }
        }

        return null;
    }

    private static string? FindGrpcCorrelationId(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 && parts[1] == "call" && long.TryParse(parts[3], out var id)
            ? $"grpc:{id}"
            : null;
    }

    private static string ClassifyLoadLane(IReadOnlyList<string> parts)
    {
        if (parts.Count > 1 && parts[1] == "iteration")
        {
            for (var index = 0; index < parts.Count - 1; index++)
            {
                if (parts[index] == "actor")
                {
                    return $"load/actor/{parts[index + 1]}";
                }
            }

            return "load";
        }

        return parts.Count > 3 && parts[1] == "run" ? $"load/{parts[3]}" : "load";
    }

    private static string? FindConsistencyCorrelationId(IReadOnlyList<string> parts)
    {
        if (parts.Count > 3 && parts[2] == "write" && long.TryParse(parts[3], out var versionId))
        {
            return $"consistency:{parts[1]}:version:{versionId}";
        }

        return parts.Count > 4 && parts[2] == "convergence"
            ? $"consistency:{parts[1]}:convergence:{parts[4]}"
            : null;
    }

    private static string? FindModelCorrelationId(IReadOnlyList<string> parts)
    {
        return parts.Count > 4 && parts[2] == "command"
            ? $"model:{parts[1]}:command:{parts[3]}"
            : null;
    }

    private static string? FindLoadCorrelationId(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 && parts[1] == "iteration" && long.TryParse(parts[3], out var iterationId)
            ? $"load:{iterationId}"
            : null;
    }

    private static string ClassifyNetworkLane(IReadOnlyList<string> parts)
    {
        if (parts.Count > 4 && parts[1] == "request")
        {
            var route = parts.FirstOrDefault(part => part.Contains("->", StringComparison.Ordinal));
            return route is null ? "network" : $"network/{route}";
        }

        if (parts.Count > 3 && parts[1] == "link")
        {
            return $"network/{parts[3]}";
        }

        return "network";
    }

    private static string? FindNetworkRequestId(IReadOnlyList<string> parts)
    {
        return parts.Count > 3 && parts[1] == "request" ? $"http:{parts[3]}" : null;
    }

    private static string? FindStorageOperationId(IReadOnlyList<string> parts)
    {
        for (var index = 0; index < parts.Count - 1; index++)
        {
            if (parts[index] == "operation")
            {
                return $"storage:{parts[1]}:{parts[index + 1]}";
            }
        }

        return null;
    }

    private static string? FindFaultCorrelationId(IReadOnlyList<string> parts)
    {
        if (parts.Count < 4 || parts[1] != "messaging")
        {
            return null;
        }

        var candidate = parts[^2];
        return long.TryParse(candidate, out var messageId) ? $"message:{messageId}" : null;
    }

    private static string? FindMessagingCorrelationId(IReadOnlyList<string> parts)
    {
        return parts.Count > 2 && long.TryParse(parts[2], out var id) ? $"message:{id}" : null;
    }

    private static bool ContainsAnyStatus(IReadOnlyList<string> values, params string[] statuses)
    {
        return values
            .SelectMany(value => value.Split('-'))
            .Any(value => statuses.Contains(value, StringComparer.OrdinalIgnoreCase));
    }
}
