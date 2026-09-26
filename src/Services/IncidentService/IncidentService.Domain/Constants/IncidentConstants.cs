namespace IncidentService.Domain.Constants;

public static class IncidentConstants
{
    public const int TitleMaxLength = 256;
    public const int DescriptionMaxLength = 4096;
    public const int TeamMaxLength = 256;
    public const int AiCategoryMaxLength = 128;
    public const int AiReasoningMaxLength = 4096;

    // The sender's own name for the problem — an alert fingerprint, a check id (Adım 27).
    public const int ExternalIdMaxLength = 200;
}
