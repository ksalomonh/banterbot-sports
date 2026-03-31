using System.ComponentModel.DataAnnotations;
using BanterBotSports.Entities.ViewModels;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LoginViewModel"/> data annotations (REQ-1, REQ-6).
/// Validates Required and Phone constraints on Telefono, and absence of Email property.
/// </summary>
public class LoginViewModelTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static LoginViewModel ValidModel() => new()
    {
        Telefono  = "+5491112345678",
        Password  = "Password1"
    };

    private static IList<ValidationResult> Validate(LoginViewModel model)
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
    public void Telefono_ValidPhone_PassesValidation()
    {
        // SCENARIO-1c: valid phone passes all constraints
        var model = ValidModel();

        var errors = Validate(model);

        errors.Should().BeEmpty("a well-formed phone number must pass validation");
    }

    // ---------------------------------------------------------------------------
    // Telefono — invalid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Telefono_EmptyString_TriggersRequiredError()
    {
        // SCENARIO-1c: empty Telefono must fail Required
        var model = ValidModel();
        model.Telefono = string.Empty;

        var errors = Validate(model);

        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(LoginViewModel.Telefono)),
            "empty Telefono must trigger the Required error");
    }

    // ---------------------------------------------------------------------------
    // Model shape — Email must not exist (SCENARIO-1c)
    // ---------------------------------------------------------------------------

    [Fact]
    public void LoginViewModel_DoesNotHaveEmailProperty()
    {
        // Email has been removed from the login flow — phone is the sole login identifier
        var type = typeof(LoginViewModel);

        type.GetProperty("Email").Should().BeNull(
            "Email was replaced by Telefono as the login identifier — the property must not exist on LoginViewModel");
    }
}
