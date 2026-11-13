using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Policies;

public static class PayoutPreflightValidation
{
    public static void Validate(PayoutPreflightRequest request)
    {
        if (request is null || request.Amount <= 0 || request.Amount > 9007199254740991m || request.Amount != decimal.Truncate(request.Amount))
            throw new PayOSPayoutValidationException("Payout amount must be a positive whole VND amount within the supported range.");
        if (!Text(request.ReferenceId, 128) || !request.ReferenceId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            throw new PayOSPayoutValidationException("A valid payout reference is required.");
        if (!Text(request.RecipientBankBin, 6) || request.RecipientBankBin.Length != 6 || !request.RecipientBankBin.All(char.IsAsciiDigit))
            throw new PayOSPayoutValidationException("A six-digit destination bank BIN is required.");
        if (!Text(request.RecipientAccountNumber, 64) || !request.RecipientAccountNumber.All(char.IsAsciiDigit))
            throw new PayOSPayoutValidationException("A destination bank account number is required (digits only, up to 64 characters).");
        if (!Text(request.Description, 200)) throw new PayOSPayoutValidationException("A payout description is required (up to 200 characters).");
    }

    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit &&
        value == value.Trim() && !value.Any(char.IsControl);
}
