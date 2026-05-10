namespace BuildingBlocks.Observability;

public static class TelemetryConstants
{
    public static class ServiceNames
    {
        public const string IncidentService = "incident-service";
        public const string NotificationService = "notification-service";
        public const string TelemetryIngestionService = "telemetry-ingestion-service";
        public const string AgentOrchestrator = "agent-orchestrator";
    }

    public static class ActivitySources
    {
        public const string IncidentService = "IncidentService";
        public const string NotificationService = "NotificationService";
        public const string TelemetryIngestionService = "TelemetryIngestionService";
        public const string AgentOrchestrator = "AgentOrchestrator";
    }

    public static class MetricNames
    {
        public const string IncidentsCreated = "incidents.created";
        public const string IncidentsResolved = "incidents.resolved";
        public const string IncidentResolutionDuration = "incidents.resolution_duration_seconds";
        public const string NotificationsSent = "notifications.sent";
        public const string AiRequestDuration = "ai.request_duration_seconds";
        public const string TelemetryEventsIngested = "telemetry.events_ingested";
    }

    public static class TagKeys
    {
        public const string IncidentSeverity = "incident.severity";
        public const string IncidentSource = "incident.source";
        public const string NotificationChannel = "notification.channel";
        public const string AiProvider = "ai.provider";
    }
}