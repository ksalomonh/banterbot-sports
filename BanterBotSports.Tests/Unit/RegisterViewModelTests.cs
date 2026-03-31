using System.ComponentModel.DataAnnotations;
using BanterBotSports.Entities.ViewModels;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RegisterViewModel"/> data annotations (REQ-3, REQ-5).
/// Validates Required and Phone constraints on Telefono, and Required+EmailAddress on Email.
/// </summary>
public class RegisterViewModelTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static RegisterViewModel ValidModel() => new()
    {
        NombreDisplay = "El Crack",
        Telefono      = "+5491112345678",
        Email         = "crack@arena.com",
        Password      = "Password1",
        ConfirmPassword = "Password1"
    };

    private static IList<ValidationResult> Validate(RegisterViewModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    // ---------------------------------------------------------------------------
    // Telefono — valid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Telefono_ValidInternationalFormat_PassesValidation()
    {
        // SCENARIO-3a: valid phone passes
        var model = ValidModel();

        var errors = Validate(model);

        errors.Should().BeEmpty("a well-formed international phone number must pass all constraints");
    }

    // ---------------------------------------------------------------------------
    // Telefono — invalid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Telefono_EmptyString_TriggersRequiredError()
    {
        // SCENARIO-5c: empty Telefono fails Required
        var model = ValidModel();
        model.Telefono = string.Empty;

        var errors = Validate(model);

        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.Telefono)),
            "empty Telefono must trigger the Required error");
    }

    [Fact]
    public void Telefono_InvalidFormat_TriggersPhoneError()
    {
        // SCENARIO-5c: non-phone string fails [Phone] annotation
        var model = ValidModel();
        model.Telefono = "not-a-phone!!";

        var errors = Validate(model);

        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.Telefono)),
            "an invalid phone string must trigger the Phone format error");
    }

    // ---------------------------------------------------------------------------
    // Email — invalid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Email_EmptyString_TriggersRequiredError()
    {
        // SCENARIO-3a: email is still required for recovery
        var model = ValidModel();
        model.Email = string.Empty;

        var errors = Validate(model);

        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(RegisterViewModel.Email)),
            "empty Email must trigger the Required error — email is required for password recovery");
    }
}
