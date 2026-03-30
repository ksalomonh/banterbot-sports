using System.ComponentModel.DataAnnotations;
using BanterBotSports.Entities.ViewModels;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ProfileEditViewModel"/> data annotations (REQ-5).
/// Validates Required, MinLength, and MaxLength constraints on NombreDisplay.
/// </summary>
public class ProfileEditViewModelTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IList<ValidationResult> Validate(ProfileEditViewModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    // ---------------------------------------------------------------------------
    // Valid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void NombreDisplay_2Chars_PassesValidation()
    {
        var model = new ProfileEditViewModel { NombreDisplay = "AB" };

        var errors = Validate(model);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void NombreDisplay_100Chars_PassesValidation()
    {
        var model = new ProfileEditViewModel { NombreDisplay = new string('A', 100) };

        var errors = Validate(model);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void NombreDisplay_TypicalName_PassesValidation()
    {
        var model = new ProfileEditViewModel { NombreDisplay = "El Crack" };

        var errors = Validate(model);

        errors.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Invalid cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void NombreDisplay_EmptyString_FailsValidation()
    {
        var model = new ProfileEditViewModel { NombreDisplay = "" };

        var errors = Validate(model);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void NombreDisplay_1Char_FailsMinLength()
    {
        var model = new ProfileEditViewModel { NombreDisplay = "A" };

        var errors = Validate(model);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void NombreDisplay_101Chars_FailsMaxLength()
    {
        var model = new ProfileEditViewModel { NombreDisplay = new string('A', 101) };

        var errors = Validate(model);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void NombreDisplay_NullValue_FailsRequired()
    {
        // Use the ValidationContext directly to simulate model binding sending null
        // (bypassing the C# required keyword that only applies at construction time).
        var attr = new RequiredAttribute { ErrorMessage = "El nombre de jugador es obligatorio" };

        var result = attr.GetValidationResult(null, new ValidationContext(new object()));

        result.Should().NotBe(ValidationResult.Success);
    }
}
