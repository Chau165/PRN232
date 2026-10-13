namespace PRN232.Application.Services;

public static class MaintenanceRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";

    public static bool IsAllowedTransition(string current, string target) =>
        (current, target) is
            (Pending, Approved) or
            (Pending, Rejected) or
            (Approved, InProgress) or
            (InProgress, Completed);
}

public class MaintenanceBusinessRuleException(string message) : Exception(message);

public sealed class MaintenanceStatusTransitionException(string message)
    : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceNotFoundException(string message) : Exception(message);

public sealed class MaintenanceContractException(string message) : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceEquipmentException(string message) : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceAssigneeException(string message) : MaintenanceBusinessRuleException(message);
