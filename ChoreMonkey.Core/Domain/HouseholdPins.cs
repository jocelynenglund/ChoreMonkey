using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Domain;

/// <summary>
/// For the store adapters of the Household, Members and Auth slices: finds a household's current PIN
/// hashes in its events and checks a PIN against them. The (slow, randomized) PBKDF2 check happens
/// here, in the adapter, and only its verdict is handed to a slice's Decide.
/// </summary>
public static class HouseholdPins
{
    /// <summary>The current admin PIN hash, or null when the household doesn't exist.</summary>
    public static string? AdminPinHash(IReadOnlyList<object> history)
    {
        var created = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (created == null) return null;
        return history.OfType<AdminPinChanged>().LastOrDefault()?.NewPinHash ?? created.PinHash;
    }

    /// <summary>True when the household exists and the PIN is its admin PIN.</summary>
    public static bool AdminPinMatches(IReadOnlyList<object> history, int pinCode)
    {
        var hash = AdminPinHash(history);
        return hash != null && PinHasher.VerifyPin(pinCode, hash);
    }
}
