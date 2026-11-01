using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Domain;

/// <summary>
/// Pure check of an admin PIN against a household's events (HouseholdCreated and any
/// AdminPinChanged). Shared by the Chores command slices that need the admin PIN, so they
/// don't each repeat the fold.
/// </summary>
public static class AdminAccess
{
    public enum Result { NoHousehold, WrongPin, Admin }

    public static Result Check(IReadOnlyList<object> history, int pinCode)
    {
        var householdCreated = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null) return Result.NoHousehold;

        var hash = history.OfType<AdminPinChanged>().LastOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        return PinHasher.VerifyPin(pinCode, hash) ? Result.Admin : Result.WrongPin;
    }
}
