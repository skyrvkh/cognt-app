namespace CogntApp.Models.Editor;

internal static class EditorWebMessageTypes
{
    public const string Load = "load";
    public const string Ready = "ready";
    public const string Loaded = "loaded";
    public const string ContentChanged = "contentChanged";
    public const string Diagnostic = "diagnostic";
    public const string CreateExtract = "createExtract";
    public const string SaveCloze = "saveCloze";
    public const string ExtractResult = "extractResult";
    public const string SaveResult = "saveResult";
    public const string SetMentionTargets = "setMentionTargets";
    public const string RequestMentionPreview = "requestMentionPreview";
    public const string MentionPreviewResult = "mentionPreviewResult";
    public const string OpenMention = "openMention";
    public const string MentionOpenResult = "mentionOpenResult";
}
