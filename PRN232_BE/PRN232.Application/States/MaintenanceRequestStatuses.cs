namespace PRN232.Application.States;

public static class MaintenanceRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Completed = "Completed";

    public static bool IsValid(string status)
    {
        return status is Pending or Approved or Completed;
    }

    public static bool IsAllowedTransition(string current, string target)
    {
        return (current, target) is
            (Pending, Approved) or
            (Approved, Completed);
    }
}

public class MaintenanceBusinessRuleException(string message) : Exception(message);

public sealed class MaintenanceStatusTransitionException(string message)
    : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceNotFoundException(string message) : Exception(message);

public sealed class MaintenanceContractException(string message) : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceEquipmentException(string message) : MaintenanceBusinessRuleException(message);

public sealed class MaintenanceAssigneeException(string message) : MaintenanceBusinessRuleException(message);
