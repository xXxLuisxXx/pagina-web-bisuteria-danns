using System.ComponentModel.DataAnnotations;
using Bisuteria.Web.Models;
using Xunit;

public sealed class AccountValidationTests
{
    [Theory]
    [InlineData(12, true)]
    [InlineData(11, false)]
    [InlineData(8, false)]
    public void RegistrationPasswordRequiresAtLeastTwelveCharacters(int length, bool expected)
    {
        var password = new string('A', length);
        var model = new RegisterViewModel
        {
            FirstName = "Cliente",
            LastName = "Ensayo",
            Email = "validation@example.test",
            Password = password,
            ConfirmPassword = password
        };

        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void LoginPasswordHasAnExplicitLengthLimit(int length, bool expected)
    {
        var model = new LoginViewModel { Email = "validation@example.test", Password = new string('x', length) };
        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }

    [Fact]
    public void ResetRejectsMalformedEmailBeforeIdentityLookup()
    {
        var model = new ResetPasswordViewModel { Email = "not-an-email", Token = "test", Password = new string('x', 12), ConfirmPassword = new string('x', 12) };
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    public void LoginReturnUrlHasAnExplicitLengthLimit(int length, bool expected)
    {
        var model = new LoginViewModel { Email = "validation@example.test", Password = "test", ReturnUrl = "/" + new string('a', length - 1) };
        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }
}
